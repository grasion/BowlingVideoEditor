using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BowlingVideoEditor.Models;
using FFMpegCore;
using FFMpegCore.Enums;

namespace BowlingVideoEditor.Services
{
    public class VideoService
    {
        private readonly string _tempDir;
        private readonly ScoreImageService _scoreImg = new();

        public VideoService()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "BowlingVideoEditor");
            Directory.CreateDirectory(_tempDir);
        }

        public async Task<string> TrimVideoAsync(string inputPath, TimeSpan start, TimeSpan end, IProgress<double> progress = null)
        {
            var output = Path.Combine(_tempDir, $"trim_{Guid.NewGuid():N}.mp4");
            await FFMpegArguments
                .FromFileInput(inputPath, true, o => o.Seek(start))
                .OutputToFile(output, true, o => o.WithDuration(end - start)
                    .WithVideoCodec(VideoCodec.LibX264).WithAudioCodec(AudioCodec.Aac).WithFastStart())
                .ProcessAsynchronously();
            return output;
        }

        public async Task<string> ZoomCropAsync(string inputPath, int zoomPercent,
            double cx, double cy, string outputPath, IProgress<double> progress = null)
        {
            var info = await FFProbe.AnalyseAsync(inputPath);
            int srcW = info.PrimaryVideoStream?.Width ?? 1920;
            int srcH = info.PrimaryVideoStream?.Height ?? 1080;
            double zoom = zoomPercent / 100.0;
            int cropW = (int)(srcW / zoom) / 2 * 2;
            int cropH = (int)(srcH / zoom) / 2 * 2;
            int cropX = Math.Clamp((int)((srcW - cropW) * cx), 0, srcW - cropW);
            int cropY = Math.Clamp((int)((srcH - cropH) * cy), 0, srcH - cropH);
            string filter = $"crop={cropW}:{cropH}:{cropX}:{cropY},scale={srcW}:{srcH}:flags=lanczos";

            await FFMpegArguments
                .FromFileInput(inputPath)
                .OutputToFile(outputPath, true, o => o
                    .WithVideoCodec(VideoCodec.LibX264).WithAudioCodec(AudioCodec.Aac)
                    .WithCustomArgument($"-vf \"{filter}\"").WithFastStart())
                .ProcessAsynchronously();
            progress?.Report(100);
            return outputPath;
        }

        public async Task<string> ConcatenateVideosAsync(List<VideoClip> clips, IProgress<double> progress = null)
        {
            if (clips.Count == 0) throw new ArgumentException("클립이 없습니다.");
            if (clips.Count == 1) return clips[0].FilePath;
            var trimmed = new List<string>();
            for (int i = 0; i < clips.Count; i++)
            {
                trimmed.Add(await TrimVideoAsync(clips[i].FilePath, clips[i].StartTime, clips[i].EndTime));
                progress?.Report((double)(i + 1) / clips.Count * 50);
            }
            var listFile = Path.Combine(_tempDir, $"concat_{Guid.NewGuid():N}.txt");
            await File.WriteAllLinesAsync(listFile, trimmed.Select(f => $"file '{f.Replace("'", "'\\''")}'"));
            var output = Path.Combine(_tempDir, $"output_{Guid.NewGuid():N}.mp4");
            await FFMpegArguments
                .FromFileInput(listFile, true, o => o.WithCustomArgument("-f concat -safe 0"))
                .OutputToFile(output, true, o => o.CopyChannel().WithFastStart())
                .ProcessAsynchronously();
            progress?.Report(100);
            return output;
        }

        /// <summary>
        /// 점수판 + 오버레이(이미지/텍스트) + 확대를 모두 포함한 내보내기.
        /// FFmpeg를 직접 실행하여 복합 필터 지원.
        /// </summary>
        public async Task<string> ExportFullAsync(string inputPath, ScoreTimeline timeline,
            List<OverlayItem> overlays, int zoomPercent, double zoomCx, double zoomCy,
            string outputPath, IProgress<double> progress = null)
        {
            int videoW = 1920, videoH = 1080;
            try
            {
                var info = await FFProbe.AnalyseAsync(inputPath);
                if (info.PrimaryVideoStream != null)
                { videoW = info.PrimaryVideoStream.Width; videoH = info.PrimaryVideoStream.Height; }
            }
            catch { }

            var inputs = new List<string> { $"-i \"{inputPath}\"" };
            var filterParts = new List<string>();
            string currentLabel = "0:v";
            int inputIdx = 1;

            // 1) 확대 (crop + scale)
            if (zoomPercent > 100)
            {
                double zoom = zoomPercent / 100.0;
                int cw = (int)(videoW / zoom) / 2 * 2;
                int ch = (int)(videoH / zoom) / 2 * 2;
                int cx = Math.Clamp((int)((videoW - cw) * zoomCx), 0, videoW - cw);
                int cy = Math.Clamp((int)((videoH - ch) * zoomCy), 0, videoH - ch);
                filterParts.Add($"[{currentLabel}]crop={cw}:{ch}:{cx}:{cy},scale={videoW}:{videoH}:flags=lanczos[zoomed]");
                currentLabel = "zoomed";
            }

            // 2) 점수판 이미지 오버레이
            if (timeline != null && timeline.Entries.Count > 0)
            {
                var score = timeline.Score ?? new BowlingScore();
                // 편집 시 사용한 크기를 그대로 사용
                int imgW = timeline.ImageWidth > 0 ? timeline.ImageWidth : (int)(900 * timeline.ScalePercent);
                int imgH = timeline.ImageHeight > 0 ? timeline.ImageHeight : (int)(120 * timeline.ScalePercent);
                int fontSize = timeline.FontSize;
                int posX = (int)(videoW * timeline.PositionX);
                int posY = (int)(videoH * timeline.PositionY);

                var entries = timeline.Entries.OrderBy(e => e.Timestamp).ThenBy(e => e.FrameIndex).ToList();

                // 각 엔트리를 개별 phase로 생성 (1투/2투 순차 표시)
                var phases = new List<(double tStart, double tEnd, string img)>();

                double firstTime = entries[0].Timestamp.TotalSeconds;
                if (firstTime > 0.1)
                    phases.Add((0, firstTime, _scoreImg.RenderScoreImage(score, 0, imgW, imgH, fontSize)));

                for (int ei = 0; ei < entries.Count; ei++)
                {
                    double ts = entries[ei].Timestamp.TotalSeconds;
                    double te = ei + 1 < entries.Count ? entries[ei + 1].Timestamp.TotalSeconds : 86400;
                    if (Math.Abs(te - ts) < 0.01) te = 86400;

                    // 이 시점까지 보이는 프레임 수 계산
                    int vis = 0;
                    int lastThrows = 2;
                    for (int pi = 0; pi <= ei; pi++)
                    {
                        if (entries[pi].FrameIndex > vis)
                        {
                            vis = entries[pi].FrameIndex;
                            lastThrows = string.IsNullOrEmpty(entries[pi].Throw2) ? 1 : 2;
                        }
                        else if (entries[pi].FrameIndex == vis && !string.IsNullOrEmpty(entries[pi].Throw2))
                        {
                            lastThrows = 2;
                        }
                    }
                    phases.Add((ts, te, _scoreImg.RenderScoreImage(score, vis, imgW, imgH, fontSize, lastThrows)));
                }

                foreach (var p in phases)
                {
                    inputs.Add($"-i \"{p.img}\"");
                    string outLbl = $"[s{inputIdx}]";
                    string enable = $"enable='between(t,{Fd(p.tStart)},{Fd(p.tEnd)})'";
                    filterParts.Add($"[{currentLabel}][{inputIdx}:v]overlay={posX}:{posY}:{enable}{outLbl}");
                    currentLabel = $"s{inputIdx}";
                    inputIdx++;
                }
            }

            // 3) 오버레이 이미지/텍스트
            if (overlays != null)
            {
                foreach (var ov in overlays)
                {
                    if (ov.Type == "image" && File.Exists(ov.Content))
                    {
                        inputs.Add($"-i \"{ov.Content}\"");
                        int ox = (int)(videoW * ov.X / 100.0);
                        int oy = (int)(videoH * ov.Y / 100.0);
                        string scalePart = $"[{inputIdx}:v]scale={ov.Size}:{ov.Size}[ov{inputIdx}]";
                        filterParts.Add(scalePart);
                        string outLbl = $"[o{inputIdx}]";
                        filterParts.Add($"[{currentLabel}][ov{inputIdx}]overlay={ox}:{oy}{outLbl}");
                        currentLabel = $"o{inputIdx}";
                        inputIdx++;
                    }
                    else if (ov.Type == "text")
                    {
                        int tx = (int)(videoW * ov.X / 100.0);
                        int ty = (int)(videoH * ov.Y / 100.0);
                        string color = ov.Color ?? "white";
                        string text = ov.Content.Replace("'", "\u2019").Replace(":", "\\:");
                        string outLbl = $"[txt{inputIdx}]";
                        filterParts.Add($"[{currentLabel}]drawtext=text='{text}':fontsize={ov.Size}:fontcolor={color}:x={tx}:y={ty}{outLbl}");
                        currentLabel = $"txt{inputIdx}";
                        inputIdx++;
                    }
                }
            }

            // FFmpeg 실행
            string ffmpegPath = GetFFmpegPath();
            string filterArg;

            if (filterParts.Count > 0)
            {
                // 마지막 라벨을 [out]으로 변경
                var last = filterParts[filterParts.Count - 1];
                var lastLabel = $"[{currentLabel.TrimStart('[')}";
                // 마지막 필터의 출력 라벨 교체
                filterParts[filterParts.Count - 1] = last.Substring(0, last.LastIndexOf('[')) + "[out]";

                var filterFile = Path.Combine(_tempDir, $"filter_{Guid.NewGuid():N}.txt");
                await File.WriteAllTextAsync(filterFile, string.Join(";", filterParts));
                filterArg = $"-filter_complex_script \"{filterFile}\" -map \"[out]\" -map 0:a?";
            }
            else
            {
                filterArg = "-c copy";
            }

            var allInputs = string.Join(" ", inputs);
            var args = $"-y {allInputs} {filterArg} -c:v libx264 -c:a aac -movflags +faststart \"{outputPath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath, Arguments = args,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) throw new Exception("FFmpeg 실행 실패");
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
                throw new Exception($"FFmpeg 오류 (코드 {proc.ExitCode}):\n{stderr[..Math.Min(stderr.Length, 800)]}");

            progress?.Report(100);
            return outputPath;
        }

        // 하위 호환
        public async Task<string> ExportWithTimelineScoreAsync(string inputPath, ScoreTimeline timeline,
            string outputPath, IProgress<double> progress = null)
        {
            return await ExportFullAsync(inputPath, timeline, null, 100, 0.5, 0.5, outputPath, progress);
        }

        private string GetFFmpegPath()
        {
            try
            {
                var opts = GlobalFFOptions.Current;
                if (!string.IsNullOrEmpty(opts.BinaryFolder))
                {
                    var p = Path.Combine(opts.BinaryFolder, "ffmpeg.exe");
                    if (File.Exists(p)) return p;
                    p = Path.Combine(opts.BinaryFolder, "ffmpeg");
                    if (File.Exists(p)) return p;
                }
            }
            catch { }
            // PATH에서 찾기
            return "ffmpeg";
        }

        private static string Fd(double val) => val.ToString("F2", CultureInfo.InvariantCulture);

        public void CleanupTemp()
        {
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
        }
    }
}
