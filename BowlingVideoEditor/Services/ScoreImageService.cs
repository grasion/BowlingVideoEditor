using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using BowlingVideoEditor.Models;

namespace BowlingVideoEditor.Services
{
    public class ScoreImageService
    {
        private readonly string _tempDir;

        public ScoreImageService()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "BowlingVideoEditor", "score_img");
            Directory.CreateDirectory(_tempDir);
        }

        /// <summary>
        /// 점수판을 PNG 이미지로 렌더링합니다.
        /// visibleFrames: 표시할 프레임 수 (1~10). 시간대별로 다르게 호출.
        /// </summary>
        public string RenderScoreImage(BowlingScore score, int visibleFrames = 10,
            int width = 900, int height = 120, int fontSize = 16)
        {
            using var bmp = new Bitmap(width, height);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 배경
            using var bgBrush = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
            g.FillRoundedRect(bgBrush, 0, 0, width, height, 8);

            // 외곽선
            using var borderPen = new Pen(Color.FromArgb(150, 255, 255, 255), 2);
            g.DrawRoundedRect(borderPen, 0, 0, width - 1, height - 1, 8);

            int cols = 11; // 10프레임 + Total
            int colW = width / cols;
            int headerH = (int)(height * 0.25);
            int throwH = (int)(height * 0.35);
            int scoreH = height - headerH - throwH;

            float fHeader = fontSize * 0.85f;
            float fThrow = fontSize * 1.0f;
            float fThrow2 = fontSize * 0.75f;
            float fScore = fontSize * 1.1f;
            float fTotal = fontSize * 1.4f;

            using var whiteBrush = new SolidBrush(Color.White);
            using var grayBrush = new SolidBrush(Color.FromArgb(180, 180, 180));
            using var yellowBrush = new SolidBrush(Color.FromArgb(255, 220, 80));
            using var greenBrush = new SolidBrush(Color.FromArgb(0, 204, 106));
            using var linePen = new Pen(Color.FromArgb(80, 255, 255, 255), 1);
            using var headerFont = new Font("Segoe UI", fHeader, FontStyle.Bold);
            using var throwFont = new Font("Segoe UI", fThrow, FontStyle.Bold);
            using var throw2Font = new Font("Segoe UI", fThrow2);
            using var scoreFont = new Font("Segoe UI", fScore, FontStyle.Bold);
            using var totalFont = new Font("Segoe UI", fTotal, FontStyle.Bold);

            // 가로 구분선
            g.DrawLine(linePen, 0, headerH, width, headerH);
            g.DrawLine(linePen, 0, headerH + throwH, width, headerH + throwH);

            // 선수 이름 (왼쪽 상단)
            using var nameFont = new Font("Segoe UI", fHeader * 0.8f, FontStyle.Bold);
            g.DrawString(score.PlayerName, nameFont, whiteBrush, 8, 2);

            for (int f = 0; f < 10; f++)
            {
                int x = f * colW;

                // 세로 구분선
                if (f > 0) g.DrawLine(linePen, x, 0, x, height);

                // 프레임 번호
                var numStr = (f + 1).ToString();
                var numSize = g.MeasureString(numStr, headerFont);
                g.DrawString(numStr, headerFont, grayBrush, x + colW / 2 - numSize.Width / 2, headerH - numSize.Height - 1);

                if (f >= visibleFrames) continue;

                var frame = score.Frames[f];

                // 1투구
                if (!string.IsNullOrEmpty(frame.Throw1))
                    g.DrawString(frame.Throw1, throwFont, whiteBrush, x + 4, headerH + 4);

                if (f < 9)
                {
                    // 2투구 (오른쪽 상단 작은 칸)
                    int boxW = colW / 3;
                    int boxH = throwH / 2;
                    g.DrawRectangle(linePen, x + colW - boxW, headerH, boxW, boxH);
                    if (!string.IsNullOrEmpty(frame.Throw2))
                        g.DrawString(frame.Throw2, throw2Font, yellowBrush, x + colW - boxW + 3, headerH + 2);
                }
                else
                {
                    // 10프레임: 3칸
                    int third = colW / 3;
                    g.DrawLine(linePen, x + third, headerH, x + third, headerH + throwH);
                    g.DrawLine(linePen, x + third * 2, headerH, x + third * 2, headerH + throwH);
                    if (!string.IsNullOrEmpty(frame.Throw2))
                        g.DrawString(frame.Throw2, throwFont, whiteBrush, x + third + 4, headerH + 4);
                    if (!string.IsNullOrEmpty(frame.Throw3))
                        g.DrawString(frame.Throw3, throwFont, whiteBrush, x + third * 2 + 4, headerH + 4);
                }

                // 누적 점수
                if (frame.Score > 0)
                {
                    var sStr = frame.Score.ToString();
                    var sSize = g.MeasureString(sStr, scoreFont);
                    g.DrawString(sStr, scoreFont, greenBrush, x + colW / 2 - sSize.Width / 2, headerH + throwH + 2);
                }
            }

            // Total 열
            int totalX = 10 * colW;
            g.DrawLine(linePen, totalX, 0, totalX, height);
            g.DrawString("Total", headerFont, grayBrush, totalX + 4, headerH - g.MeasureString("Total", headerFont).Height - 1);

            int lastScore = 0;
            for (int f = Math.Min(visibleFrames, 10) - 1; f >= 0; f--)
                if (score.Frames[f].Score > 0) { lastScore = score.Frames[f].Score; break; }

            if (lastScore > 0)
            {
                var tStr = lastScore.ToString();
                var tSize = g.MeasureString(tStr, totalFont);
                g.DrawString(tStr, totalFont, greenBrush, totalX + (width - totalX) / 2 - tSize.Width / 2, headerH + throwH + 2);
            }

            var path = Path.Combine(_tempDir, $"score_{Guid.NewGuid():N}.png");
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return path;
        }

        public void Cleanup()
        {
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
        }

        /// <summary>
        /// 점수판 + 오버레이(이미지/텍스트)를 하나의 투명 PNG로 합성.
        /// canvasW/canvasH: VLC 표시 영역 크기 (영상 비율에 맞춤)
        /// </summary>
        public string RenderCompositeOverlay(BowlingScore score, int visibleFrames,
            int scoreW, int scoreH, int scoreFontSize, int scoreX, int scoreY,
            List<OverlayItem> overlays, int canvasW, int canvasH)
        {
            using var bmp = new Bitmap(canvasW, canvasH);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Transparent);

            // 1) 점수판 그리기
            if (score != null && scoreW > 0 && scoreH > 0)
            {
                var scoreImgPath = RenderScoreImage(score, visibleFrames, scoreW, scoreH, scoreFontSize);
                using var scoreImg = Image.FromFile(scoreImgPath);
                g.DrawImage(scoreImg, scoreX, scoreY, scoreW, scoreH);
            }

            // 2) 오버레이 항목들
            if (overlays != null)
            {
                foreach (var ov in overlays)
                {
                    int ox = (int)(canvasW * ov.X / 100.0);
                    int oy = (int)(canvasH * ov.Y / 100.0);

                    if (ov.Type == "image" && File.Exists(ov.Content))
                    {
                        try
                        {
                            // 캔버스 내에 맞추기
                            int drawSize = ov.Size;
                            ox = Math.Clamp(ox, 0, Math.Max(0, canvasW - drawSize));
                            oy = Math.Clamp(oy, 0, Math.Max(0, canvasH - drawSize));
                            using var img = Image.FromFile(ov.Content);
                            g.DrawImage(img, ox, oy, drawSize, drawSize);
                        }
                        catch { }
                    }
                    else if (ov.Type == "text" && !string.IsNullOrEmpty(ov.Content))
                    {
                        var color = ov.Color switch
                        {
                            "yellow" => Color.Yellow,
                            "red" => Color.Red,
                            "green" => Color.LimeGreen,
                            "cyan" => Color.Cyan,
                            _ => Color.White
                        };
                        float fontSize2 = Math.Max(8, ov.Size * 0.75f);
                        using var brush = new SolidBrush(color);
                        using var font = new Font("Segoe UI", fontSize2, FontStyle.Bold);
                        var textSize = g.MeasureString(ov.Content, font);
                        // 캔버스 내에 맞추기
                        ox = Math.Clamp(ox, 0, Math.Max(0, canvasW - (int)textSize.Width));
                        oy = Math.Clamp(oy, 0, Math.Max(0, canvasH - (int)textSize.Height));
                        using var bgBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
                        g.FillRectangle(bgBrush, ox - 2, oy - 1, textSize.Width + 4, textSize.Height + 2);
                        g.DrawString(ov.Content, font, brush, ox, oy);
                    }
                }
            }

            var path = Path.Combine(_tempDir, $"composite_{Guid.NewGuid():N}.png");
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return path;
        }
    }

    // Graphics 확장 메서드
    static class GraphicsExtensions
    {
        public static void FillRoundedRect(this Graphics g, Brush brush, int x, int y, int w, int h, int r)
        {
            using var path = RoundedRectPath(x, y, w, h, r);
            g.FillPath(brush, path);
        }

        public static void DrawRoundedRect(this Graphics g, Pen pen, int x, int y, int w, int h, int r)
        {
            using var path = RoundedRectPath(x, y, w, h, r);
            g.DrawPath(pen, path);
        }

        private static GraphicsPath RoundedRectPath(int x, int y, int w, int h, int r)
        {
            var p = new GraphicsPath();
            p.AddArc(x, y, r * 2, r * 2, 180, 90);
            p.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            p.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            p.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
