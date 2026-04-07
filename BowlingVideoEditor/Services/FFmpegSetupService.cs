using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace BowlingVideoEditor.Services
{
    public class FFmpegSetupService
    {
        private static readonly string AppDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string FFmpegDir = Path.Combine(AppDir, "ffmpeg");
        private static readonly string FFmpegExe = Path.Combine(FFmpegDir, "ffmpeg.exe");
        private static readonly string FFprobeExe = Path.Combine(FFmpegDir, "ffprobe.exe");

        // gyan.dev에서 제공하는 FFmpeg essentials 빌드 (무료, 재배포 가능)
        private const string DownloadUrl =
            "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

        /// <summary>
        /// FFmpeg가 사용 가능한지 확인하고, 없으면 자동 설치합니다.
        /// </summary>
        public async Task<bool> EnsureFFmpegAsync(IProgress<(string Status, double Percent)>? progress = null)
        {
            // 1) 앱 내장 ffmpeg 확인
            if (File.Exists(FFmpegExe) && File.Exists(FFprobeExe))
            {
                ConfigureFFmpegPath();
                return true;
            }

            // 2) 시스템 PATH에 있는지 확인
            if (IsFFmpegInPath())
            {
                return true;
            }

            // 3) 없으면 다운로드 & 설치
            progress?.Report(("FFmpeg 다운로드 준비 중...", 0));

            try
            {
                await DownloadAndInstallAsync(progress);
                ConfigureFFmpegPath();
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report(($"FFmpeg 설치 실패: {ex.Message}", 0));
                return false;
            }
        }

        private bool IsFFmpegInPath()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit(5000);
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task DownloadAndInstallAsync(IProgress<(string Status, double Percent)>? progress)
        {
            Directory.CreateDirectory(FFmpegDir);

            var zipPath = Path.Combine(Path.GetTempPath(), "ffmpeg_download.zip");

            // 다운로드
            progress?.Report(("FFmpeg 다운로드 중...", 5));

            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromMinutes(10);

                using var response = await httpClient.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[65536];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var percent = (double)totalRead / totalBytes * 80; // 0~80%
                        var mb = totalRead / 1024.0 / 1024.0;
                        var totalMb = totalBytes / 1024.0 / 1024.0;
                        progress?.Report(($"FFmpeg 다운로드 중... {mb:F1}/{totalMb:F1} MB", percent));
                    }
                }
            }

            // 압축 해제
            progress?.Report(("FFmpeg 압축 해제 중...", 85));

            var extractDir = Path.Combine(Path.GetTempPath(), "ffmpeg_extract");
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, true);

            ZipFile.ExtractToDirectory(zipPath, extractDir);

            // ffmpeg-*-essentials_build/bin/ 안에 exe가 있음
            var binDir = Directory.GetDirectories(extractDir, "ffmpeg-*", SearchOption.TopDirectoryOnly)
                .SelectMany(d => Directory.GetDirectories(d, "bin"))
                .FirstOrDefault();

            if (binDir == null)
            {
                // bin 폴더가 없으면 exe 직접 검색
                var ffmpegFile = Directory.GetFiles(extractDir, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (ffmpegFile != null)
                    binDir = Path.GetDirectoryName(ffmpegFile);
            }

            if (binDir == null)
                throw new FileNotFoundException("다운로드된 FFmpeg에서 실행 파일을 찾을 수 없습니다.");

            progress?.Report(("FFmpeg 설치 중...", 92));

            // 필요한 exe만 복사
            foreach (var fileName in new[] { "ffmpeg.exe", "ffprobe.exe", "ffplay.exe" })
            {
                var src = Path.Combine(binDir, fileName);
                if (File.Exists(src))
                    File.Copy(src, Path.Combine(FFmpegDir, fileName), true);
            }

            // 정리
            try
            {
                File.Delete(zipPath);
                Directory.Delete(extractDir, true);
            }
            catch { }

            progress?.Report(("FFmpeg 설치 완료", 100));
        }

        /// <summary>
        /// FFMpegCore가 내장 ffmpeg를 사용하도록 경로를 설정합니다.
        /// </summary>
        private void ConfigureFFmpegPath()
        {
            FFMpegCore.GlobalFFOptions.Configure(new FFMpegCore.FFOptions
            {
                BinaryFolder = FFmpegDir
            });

            // 환경변수 PATH에도 추가 (현재 프로세스 한정)
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (!currentPath.Contains(FFmpegDir, StringComparison.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("PATH", FFmpegDir + ";" + currentPath);
            }
        }
    }
}
