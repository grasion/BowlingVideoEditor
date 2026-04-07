using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BowlingVideoEditor.Models;
using BowlingVideoEditor.Services;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using Microsoft.Win32;

namespace BowlingVideoEditor.Views
{
    public partial class MainWindow : Window
    {
        private LibVLC _libVLC;
        private LibVLCSharp.Shared.MediaPlayer _mp;
        private VideoView _vlcView;

        private readonly VideoService _videoService = new();
        private readonly ScoreImageService _scoreImgService = new();
        private readonly UpdateService _updateService = new("YOUR_GITHUB_USERNAME", "BowlingVideoEditor");
        private readonly ObservableCollection<VideoClip> _clips = new();
        private readonly DispatcherTimer _timer;
        private bool _isDraggingSlider;
        private string _currentFilePath;
        private ScoreTimeline _currentTimeline;
        private bool _scoreVisible;
        private int _logoX = 20, _logoY = 10;
        private int _scoreFontSize = 16;
        private int _scoreImgW = 900, _scoreImgH = 120;
        private string _currentScoreImgPath;
        private int _selectedClipIdx = -1;
        private readonly System.Collections.Generic.List<OverlayItem> _overlays = new();

        // 점수판 드래그
        private bool _logoDragging;
        private System.Drawing.Point _logoDragStart;

        private TextBox[] _t1 = new TextBox[10];
        private TextBox[] _t2 = new TextBox[10];
        private TextBox[] _sc = new TextBox[10];
        private TextBox[] _tm = new TextBox[10];

        public MainWindow()
        {
            InitializeComponent();
            ClipListBox.ItemsSource = _clips;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _timer.Tick += Timer_Tick;
            BuildScoreGrid();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Core.Initialize();
                _libVLC = new LibVLC("--sub-source=logo");
                _mp = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
                _vlcView = new VideoView { MediaPlayer = _mp, BackColor = System.Drawing.Color.Black };
                WfHost.Child = _vlcView;
                _mp.Volume = 100;
                _timer.Start();

                // 영상 끝 도달 시 버튼 상태 업데이트
                _mp.EndReached += (s2, ev) =>
                {
                    Dispatcher.InvokeAsync(() => PlayPauseButton.Content = "▶");
                };

                // 점수판 마우스 드래그
                _vlcView.MouseDown += (s2, me) => {
                    if (_scoreVisible && me.Button == System.Windows.Forms.MouseButtons.Left)
                    { _logoDragging = true; _logoDragStart = me.Location; }
                };
                _vlcView.MouseMove += (s2, me) => {
                    if (!_logoDragging) return;
                    _logoX += me.X - _logoDragStart.X;
                    _logoY += me.Y - _logoDragStart.Y;
                    _logoX = Math.Max(0, _logoX);
                    _logoY = Math.Max(0, _logoY);
                    _logoDragStart = me.Location;
                    ApplyLogoPosition();
                    Dispatcher.Invoke(() => {
                        if (ScorePosXSlider != null && _vlcView.Width > 0)
                            ScorePosXSlider.Value = (double)_logoX / _vlcView.Width * 100;
                        if (ScorePosYSlider != null && _vlcView.Height > 0)
                            ScorePosYSlider.Value = (double)_logoY / _vlcView.Height * 100;
                    });
                };
                _vlcView.MouseUp += (s2, me) => { _logoDragging = false; };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"플레이어 초기화 실패:\n{ex.Message}", "오류");
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_mp == null || !_mp.IsPlaying || _isDraggingSlider) return;
            var len = _mp.Length;
            if (len <= 0) return;
            TimelineSlider.Value = (double)_mp.Time / len * 1000;
            CurrentTimeText.Text = TimeSpan.FromMilliseconds(_mp.Time).ToString(@"hh\:mm\:ss");
            TotalTimeText.Text = TimeSpan.FromMilliseconds(len).ToString(@"hh\:mm\:ss");
            UpdatePlayhead();
            // 시간대별 점수판 갱신 (1초마다)
            if (_scoreVisible && _mp.Time % 1000 < 300)
                RefreshScoreOverlay();
        }

        private void PlayFile(string path)
        {
            if (_mp == null || _libVLC == null) return;
            var media = new Media(_libVLC, path, FromType.FromPath);
            _mp.Play(media);
            Dispatcher.InvokeAsync(() =>
            {
                PlayPauseButton.Content = "⏸";
                if (_scoreVisible) RefreshScoreOverlay();
                var delayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                delayTimer.Tick += (s, ev) => { delayTimer.Stop(); UpdateTimeline(); };
                delayTimer.Start();
            }, DispatcherPriority.Background);
        }

        // ===== 점수판 이미지 오버레이 =====

        private void RefreshScoreOverlay()
        {
            if (_mp == null || !_scoreVisible) return;

            SyncScoreData();
            var score = _currentTimeline?.Score ?? new BowlingScore();

            // 시간대별 표시할 프레임 수
            int visibleFrames = 0;
            if (_currentTimeline != null && _currentTimeline.Entries.Count > 0 && _mp.Length > 0)
            {
                var currentTime = TimeSpan.FromMilliseconds(_mp.Time);
                foreach (var entry in _currentTimeline.Entries)
                    if (entry.Timestamp <= currentTime)
                        visibleFrames = Math.Max(visibleFrames, entry.FrameIndex);
            }
            else if (_currentTimeline == null || _currentTimeline.Entries.Count == 0)
                visibleFrames = 10;

            // 영상 실제 해상도 사용 (VLC Logo는 영상 해상도 기준)
            uint vw = 0, vh = 0;
            _mp.Size(0, ref vw, ref vh);
            int canvasW = vw > 0 ? (int)vw : (_vlcView?.Width ?? 800);
            int canvasH = vh > 0 ? (int)vh : (_vlcView?.Height ?? 600);
            if (canvasW < 100) canvasW = 800;
            if (canvasH < 100) canvasH = 600;

            // 점수판 위치도 영상 해상도 기준으로 변환
            int actualLogoX = (int)(canvasW * (ScorePosXSlider?.Value ?? 3) / 100.0);
            int actualLogoY = (int)(canvasH * (ScorePosYSlider?.Value ?? 2) / 100.0);

            // 합성 이미지 생성 (점수판 + 볼링공 + 텍스트)
            var imgPath = _scoreImgService.RenderCompositeOverlay(
                score, visibleFrames,
                _scoreImgW, _scoreImgH, _scoreFontSize, actualLogoX, actualLogoY,
                _overlays.Count > 0 ? _overlays : null,
                canvasW, canvasH);

            _currentScoreImgPath = imgPath;

            // VLC Logo로 표시 (위치 0,0 - 이미지 자체가 전체 캔버스)
            _mp.SetLogoString(VideoLogoOption.File, imgPath);
            _mp.SetLogoInt(VideoLogoOption.Enable, 1);
            _mp.SetLogoInt(VideoLogoOption.Opacity, 255);
            _mp.SetLogoInt(VideoLogoOption.X, 0);
            _mp.SetLogoInt(VideoLogoOption.Y, 0);
        }

        private void ApplyLogoPosition()
        {
            if (_mp == null) return;
            _mp.SetLogoInt(VideoLogoOption.X, _logoX);
            _mp.SetLogoInt(VideoLogoOption.Y, _logoY);
        }

        private void HideScoreOverlay()
        {
            if (_mp == null) return;
            _mp.SetLogoInt(VideoLogoOption.Enable, 0);
        }

        /// <summary>점수판 없이 오버레이(볼링공/텍스트)만 표시</summary>
        private void ShowOverlaysOnly()
        {
            if (_mp == null || _overlays.Count == 0) return;

            uint vw = 0, vh = 0;
            _mp.Size(0, ref vw, ref vh);
            int canvasW = vw > 0 ? (int)vw : (_vlcView?.Width ?? 800);
            int canvasH = vh > 0 ? (int)vh : (_vlcView?.Height ?? 600);
            if (canvasW < 100) canvasW = 800;
            if (canvasH < 100) canvasH = 600;

            var imgPath = _scoreImgService.RenderCompositeOverlay(
                null, 0, 0, 0, 0, 0, 0,
                _overlays, canvasW, canvasH);

            _mp.SetLogoString(VideoLogoOption.File, imgPath);
            _mp.SetLogoInt(VideoLogoOption.Enable, 1);
            _mp.SetLogoInt(VideoLogoOption.Opacity, 255);
            _mp.SetLogoInt(VideoLogoOption.X, 0);
            _mp.SetLogoInt(VideoLogoOption.Y, 0);
        }

        private void SyncScoreData()
        {
            var tl = new ScoreTimeline();
            var score = new BowlingScore { PlayerName = PlayerNameBox.Text };
            for (int i = 0; i < 10; i++)
            {
                var a = _t1[i].Text.Trim(); var b = _t2[i].Text.Trim();
                var c = i == 9 ? F10T3Box.Text.Trim() : "";
                int.TryParse(_sc[i].Text, out int cs); TimeSpan.TryParse(_tm[i].Text, out var ts);
                score.Frames[i].Throw1 = a; score.Frames[i].Throw2 = b;
                score.Frames[i].Throw3 = c; score.Frames[i].Score = cs;
                if (!string.IsNullOrEmpty(a) || cs > 0)
                    tl.Entries.Add(new ScoreEntry { Timestamp = ts, FrameIndex = i + 1,
                        Throw1 = a, Throw2 = b, Throw3 = c, CumulativeScore = cs });
            }
            int.TryParse(TotalScoreText.Text, out int tot);
            score.TotalScore = tot; tl.Score = score;
            if (ScorePosXSlider != null) tl.PositionX = ScorePosXSlider.Value / 100.0;
            if (ScorePosYSlider != null) tl.PositionY = ScorePosYSlider.Value / 100.0;
            tl.ScalePercent = _scoreImgW / 900.0;
            tl.FontSize = _scoreFontSize;
            _currentTimeline = tl;
        }

        private void OpenVideo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "영상 파일|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.ts;*.webm|모든 파일|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true) return;

            foreach (var file in dlg.FileNames)
            {
                if (_clips.Count == 0 && string.IsNullOrEmpty(_currentFilePath))
                {
                    // 첫 영상 - 바로 재생하고 클립으로 추가
                    _currentFilePath = file;
                    // FFProbe로 길이 가져오기 (비동기)
                    AddFileAsClipAsync(file);
                    PlayFile(file);
                }
                else
                {
                    // 기존 영상 뒤에 추가
                    AddFileAsClipAsync(file);
                }
            }
            StatusText.Text = $"클립 {_clips.Count}개 로드됨";
        }

        private async void AddFileAsClipAsync(string filePath)
        {
            try
            {
                var info = await FFMpegCore.FFProbe.AnalyseAsync(filePath);
                var duration = info.Duration;
                _clips.Add(new VideoClip
                {
                    FilePath = filePath,
                    StartTime = TimeSpan.Zero,
                    EndTime = duration,
                    Order = _clips.Count
                });
                UpdateTimeline();
            }
            catch
            {
                // FFProbe 실패 시 기본 길이
                _clips.Add(new VideoClip
                {
                    FilePath = filePath,
                    StartTime = TimeSpan.Zero,
                    EndTime = TimeSpan.FromMinutes(1),
                    Order = _clips.Count
                });
                UpdateTimeline();
            }
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_mp == null) return;
            if (_mp.IsPlaying)
            {
                _mp.Pause();
                PlayPauseButton.Content = "▶";
            }
            else if (!string.IsNullOrEmpty(_currentFilePath))
            {
                // EndReached 상태이면 Stop 후 다시 Play
                if (_mp.State == VLCState.Ended || _mp.State == VLCState.Stopped)
                {
                    var media = new Media(_libVLC, _currentFilePath, FromType.FromPath);
                    _mp.Play(media);
                }
                else
                {
                    _mp.Play();
                }
                PlayPauseButton.Content = "⏸";
            }
        }

        private void SeekBack_Click(object sender, RoutedEventArgs e)
        {
            if (_mp == null || _mp.Length <= 0) return;
            _mp.Time = Math.Max(0, _mp.Time - 5000);
        }

        private void SeekForward_Click(object sender, RoutedEventArgs e)
        {
            if (_mp == null || _mp.Length <= 0) return;
            _mp.Time = Math.Min(_mp.Length, _mp.Time + 5000);
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_mp != null) _mp.Volume = (int)VolumeSlider.Value;
            if (VolumeText != null) VolumeText.Text = $"{(int)VolumeSlider.Value}%";
        }

        private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isDraggingSlider || _mp == null || _mp.Length <= 0) return;
            _mp.Time = (long)(TimelineSlider.Value / 1000 * _mp.Length);
        }
        private void TimelineSlider_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { _isDraggingSlider = true; }
        private void TimelineSlider_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDraggingSlider = false;
            if (_mp == null || _mp.Length <= 0) return;
            _mp.Time = (long)(TimelineSlider.Value / 1000 * _mp.Length);
        }

        // ===== 점수판 패널 =====

        private void BuildScoreGrid()
        {
            for (int i = 0; i < 10; i++)
            {
                int row = i + 1;
                var lbl = new TextBlock { Text = (i+1).ToString(),
                    Foreground = new SolidColorBrush(Color.FromRgb(0,122,204)),
                    FontWeight = FontWeights.Bold, FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(lbl, row); Grid.SetColumn(lbl, 0); ScoreGrid.Children.Add(lbl);
                _t1[i] = MkCell(row, 1); _t1[i].LostFocus += ThrowBox_LostFocus; _t1[i].FontSize = 14;
                _t2[i] = MkCell(row, 2); _t2[i].LostFocus += ThrowBox_LostFocus; _t2[i].FontSize = 14;
                _sc[i] = MkCell(row, 3); _sc[i].Foreground = new SolidColorBrush(Color.FromRgb(0,204,106));
                _sc[i].FontWeight = FontWeights.Bold; _sc[i].IsReadOnly = true; _sc[i].FontSize = 15;
                _tm[i] = MkCell(row, 4); _tm[i].Text = "00:00:00"; _tm[i].FontSize = 11;
                _tm[i].MaxLength = 8; // hh:mm:ss
                _tm[i].LostFocus += TimeBox_LostFocus;
                _tm[i].Foreground = new SolidColorBrush(Color.FromRgb(170,170,170));
            }
        }

        private TextBox MkCell(int r, int c)
        {
            var t = new TextBox { Background = Brushes.Transparent, Foreground = Brushes.White,
                BorderThickness = new Thickness(0,0,0,1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(85,85,85)),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 14, MaxLength = 3, Padding = new Thickness(2) };
            Grid.SetRow(t, r); Grid.SetColumn(t, c); ScoreGrid.Children.Add(t); return t;
        }

        private void AddScoreboard_Click(object sender, RoutedEventArgs e)
        {
            if (ScorePanel.Visibility == Visibility.Visible) { CloseScorePanel_Click(sender, e); return; }
            ScorePanel.Visibility = Visibility.Visible;
            ScorePanelColumn.Width = new GridLength(290);
            _scoreVisible = true;
            RefreshScoreOverlay();
            StatusText.Text = "점수판 표시됨. 영상 위에서 마우스 드래그로 위치 이동 가능.";
        }

        private void CloseScorePanel_Click(object sender, RoutedEventArgs e)
        {
            SyncScoreData();
            ScorePanel.Visibility = Visibility.Collapsed;
            ScorePanelColumn.Width = new GridLength(0);
            _scoreVisible = false;
            HideScoreOverlay();
        }

        private void ScorePos_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || ScorePosXSlider == null || ScorePosYSlider == null) return;
            _logoX = (int)(ScorePosXSlider.Value / 100.0 * (_vlcView.Width > 0 ? _vlcView.Width : 800));
            _logoY = (int)(ScorePosYSlider.Value / 100.0 * (_vlcView.Height > 0 ? _vlcView.Height : 600));
            if (ScorePosXText != null) ScorePosXText.Text = $"{(int)ScorePosXSlider.Value}%";
            if (ScorePosYText != null) ScorePosYText.Text = $"{(int)ScorePosYSlider.Value}%";
            if (_scoreVisible) ApplyLogoPosition();
        }

        private void ScoreSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || ScoreSizeSlider == null) return;
            _scoreImgW = (int)(900 * ScoreSizeSlider.Value / 100.0);
            _scoreImgH = (int)(120 * ScoreSizeSlider.Value / 100.0);
            if (ScoreSizeText != null) ScoreSizeText.Text = $"{(int)ScoreSizeSlider.Value}%";
            if (_scoreVisible) RefreshScoreOverlay();
        }

        private void ScoreFont_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || ScoreFontSlider == null) return;
            _scoreFontSize = (int)ScoreFontSlider.Value;
            if (ScoreFontText != null) ScoreFontText.Text = $"{_scoreFontSize}";
            if (_scoreVisible) RefreshScoreOverlay();
        }

        private void ThrowBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var box = sender as TextBox; if (box == null) return;
            for (int i = 0; i < 10; i++) if (box == _t1[i] || box == _t2[i]) { ApplyRules(i); break; }
            if (box == F10T3Box && F10T3Box.Text.Trim().ToUpper() == "10") F10T3Box.Text = "X";
            Recalc(); SyncScoreData();
            if (_scoreVisible) RefreshScoreOverlay();
        }

        private void TimeBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // 시간 형식 검증
            var box = sender as TextBox; if (box == null) return;
            if (!TimeSpan.TryParse(box.Text.Trim(), out _))
            {
                box.Text = "00:00:00";
            }
            SyncScoreData();
            if (_scoreVisible) RefreshScoreOverlay();
            StatusText.Text = "프레임 시간이 업데이트되었습니다.";
        }

        private void ApplyRules(int idx)
        {
            var v1 = _t1[idx].Text.Trim().ToUpper(); var v2 = _t2[idx].Text.Trim().ToUpper();
            if (v1 == "10") { _t1[idx].Text = "X"; v1 = "X"; }
            if (idx < 9)
            {
                if (v1 == "X") { _t2[idx].Text = ""; _t2[idx].IsEnabled = false; }
                else
                {
                    _t2[idx].IsEnabled = true;
                    if (int.TryParse(v1, out int a) && int.TryParse(v2, out int b))
                    { if (a+b == 10) _t2[idx].Text = "/"; else if (a+b > 10) _t2[idx].Text = ""; }
                    if (v2 == "10") _t2[idx].Text = "/";
                }
            }
            else
            {
                if (v2 == "10") { _t2[idx].Text = "X"; v2 = "X"; }
                bool bonus = v1 == "X" || v2 == "X" || v2 == "/";
                if (!bonus && int.TryParse(v1, out int a) && int.TryParse(v2, out int b) && a+b == 10)
                { _t2[idx].Text = "/"; bonus = true; }
                F10T3Box.IsEnabled = bonus; if (!bonus) F10T3Box.Text = "";
            }
        }

        private int Pins(int f, int th)
        {
            string s = th == 0 ? _t1[f].Text : th == 1 ? _t2[f].Text : (f == 9 ? F10T3Box.Text : "");
            s = s.Trim().ToUpper();
            if (s == "X") return 10;
            if (s == "/") return 10 - Pins(f, 0);
            return int.TryParse(s, out int v) ? v : 0;
        }

        private void Recalc()
        {
            var rolls = new System.Collections.Generic.List<int>();
            for (int f = 0; f < 9; f++)
            {
                int p1 = Pins(f, 0);
                if (p1 == 10) rolls.Add(10);
                else { rolls.Add(p1); rolls.Add(Pins(f, 1)); }
            }
            rolls.Add(Pins(9, 0)); rolls.Add(Pins(9, 1));
            if (F10T3Box.IsEnabled) rolls.Add(Pins(9, 2));

            int ri = 0, cum = 0;
            for (int f = 0; f < 10; f++)
            {
                if (ri >= rolls.Count || string.IsNullOrWhiteSpace(_t1[f].Text)) break;
                if (f < 9)
                {
                    if (rolls[ri] == 10)
                    { cum += 10 + (ri+1 < rolls.Count ? rolls[ri+1] : 0) + (ri+2 < rolls.Count ? rolls[ri+2] : 0); ri++; }
                    else if (ri+1 < rolls.Count && rolls[ri]+rolls[ri+1] == 10)
                    { cum += 10 + (ri+2 < rolls.Count ? rolls[ri+2] : 0); ri += 2; }
                    else { cum += rolls[ri] + (ri+1 < rolls.Count ? rolls[ri+1] : 0); ri += 2; }
                }
                else { for (int r = ri; r < rolls.Count; r++) cum += rolls[r]; }
                _sc[f].Text = cum.ToString();
            }
            TotalScoreText.Text = cum.ToString();
        }

        // ===== 편집/내보내기 =====

        private async void TrimVideo_Click(object sender, RoutedEventArgs e)
        {
            if (_mp == null || _mp.Length <= 0 || string.IsNullOrEmpty(_currentFilePath))
            { MessageBox.Show("먼저 영상을 열어주세요."); return; }

            // 현재 재생 위치에서 클립 분할
            var currentPos = TimeSpan.FromMilliseconds(_mp.Time);

            if (_clips.Count == 0)
            {
                // 클립이 없으면 현재 영상을 기준으로 2개로 분할
                var totalDuration = TimeSpan.FromMilliseconds(_mp.Length);
                if (currentPos <= TimeSpan.Zero || currentPos >= totalDuration)
                { MessageBox.Show("분할할 위치로 재생 헤드를 이동하세요."); return; }

                _clips.Add(new VideoClip
                {
                    FilePath = _currentFilePath,
                    StartTime = TimeSpan.Zero,
                    EndTime = currentPos,
                    Order = 0
                });
                _clips.Add(new VideoClip
                {
                    FilePath = _currentFilePath,
                    StartTime = currentPos,
                    EndTime = totalDuration,
                    Order = 1
                });
            }
            else
            {
                // 클립 목록에서 현재 위치에 해당하는 클립을 찾아 분할
                long posMs = _mp.Time;
                long accMs = 0;
                int splitIdx = -1;
                TimeSpan splitAt = TimeSpan.Zero;

                for (int i = 0; i < _clips.Count; i++)
                {
                    long clipMs = (long)_clips[i].Duration.TotalMilliseconds;
                    if (posMs >= accMs && posMs < accMs + clipMs)
                    {
                        splitIdx = i;
                        splitAt = TimeSpan.FromMilliseconds(posMs - accMs) + _clips[i].StartTime;
                        break;
                    }
                    accMs += clipMs;
                }

                if (splitIdx < 0) { MessageBox.Show("분할할 위치를 찾을 수 없습니다."); return; }

                var clip = _clips[splitIdx];
                if (splitAt <= clip.StartTime || splitAt >= clip.EndTime)
                { MessageBox.Show("클립 경계에서는 분할할 수 없습니다."); return; }

                var clip1 = new VideoClip { FilePath = clip.FilePath, StartTime = clip.StartTime, EndTime = splitAt, Order = splitIdx };
                var clip2 = new VideoClip { FilePath = clip.FilePath, StartTime = splitAt, EndTime = clip.EndTime, Order = splitIdx + 1 };

                _clips.RemoveAt(splitIdx);
                _clips.Insert(splitIdx, clip2);
                _clips.Insert(splitIdx, clip1);
            }

            UpdateTimeline();
            StatusText.Text = $"재생 위치에서 분할 완료 (클립 {_clips.Count}개)";
        }

        private async void ConcatenateClips_Click(object sender, RoutedEventArgs e)
        {
            if (_clips.Count < 2) { MessageBox.Show("클립 2개 이상 필요"); return; }
            StatusText.Text = "합치는 중...";
            try
            {
                var p = new Progress<double>(v => ProgressBar.Value = v);
                var o = await _videoService.ConcatenateVideosAsync(new System.Collections.Generic.List<VideoClip>(_clips), p);
                _currentFilePath = o; PlayFile(o); StatusText.Text = "완료"; ProgressBar.Value = 0;
            }
            catch (Exception ex) { MessageBox.Show($"오류: {ex.Message}"); StatusText.Text = "오류"; }
        }

        // ===== 볼링공 이미지 & 텍스트 =====

        private void AddBallImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif|모든 파일|*.*",
                Title = "볼링공 이미지 선택"
            };
            if (dlg.ShowDialog() != true) return;

            // 원형 크롭 영역 선택 창
            var cropWin = new Window
            {
                Title = "🎱 원형 영역 선택 - 마우스로 드래그",
                Width = 700, Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                ResizeMode = ResizeMode.CanResize
            };

            var mainPanel = new Grid();
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 이미지 + 원형 선택 캔버스
            var canvas = new Canvas { Background = Brushes.Black, ClipToBounds = true };
            var bitmapImage = new System.Windows.Media.Imaging.BitmapImage(new Uri(dlg.FileName));
            var imgControl = new Image
            {
                Source = bitmapImage,
                Stretch = System.Windows.Media.Stretch.Uniform
            };
            canvas.Children.Add(imgControl);

            // 원형 선택 표시
            var circleOverlay = new System.Windows.Shapes.Ellipse
            {
                Stroke = Brushes.Cyan, StrokeThickness = 3,
                Fill = new SolidColorBrush(Color.FromArgb(40, 0, 200, 255)),
                Width = 150, Height = 150,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(circleOverlay, 100);
            Canvas.SetTop(circleOverlay, 100);
            canvas.Children.Add(circleOverlay);

            // 마우스 드래그로 원형 영역 지정
            bool isDragging = false;
            Point dragStart = new Point();

            canvas.MouseLeftButtonDown += (s2, me) =>
            {
                isDragging = true;
                dragStart = me.GetPosition(canvas);
                Canvas.SetLeft(circleOverlay, dragStart.X);
                Canvas.SetTop(circleOverlay, dragStart.Y);
                circleOverlay.Width = 0;
                circleOverlay.Height = 0;
                canvas.CaptureMouse();
            };

            canvas.MouseMove += (s2, me) =>
            {
                if (!isDragging) return;
                var pos = me.GetPosition(canvas);
                double dx = pos.X - dragStart.X;
                double dy = pos.Y - dragStart.Y;
                double size = Math.Max(Math.Abs(dx), Math.Abs(dy));
                double left = dx >= 0 ? dragStart.X : dragStart.X - size;
                double top = dy >= 0 ? dragStart.Y : dragStart.Y - size;
                Canvas.SetLeft(circleOverlay, left);
                Canvas.SetTop(circleOverlay, top);
                circleOverlay.Width = size;
                circleOverlay.Height = size;
            };

            canvas.MouseLeftButtonUp += (s2, me) =>
            {
                isDragging = false;
                canvas.ReleaseMouseCapture();
            };

            // 이미지를 캔버스에 맞추기
            canvas.SizeChanged += (s2, se) =>
            {
                imgControl.Width = canvas.ActualWidth;
                imgControl.Height = canvas.ActualHeight;
            };

            Grid.SetRow(canvas, 0);
            mainPanel.Children.Add(canvas);

            // 하단 컨트롤
            var bottomPanel = new StackPanel { Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10) };

            bottomPanel.Children.Add(new TextBlock { Text = "영상 X위치(%):", Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            var xSlider = new Slider { Minimum = 0, Maximum = 100, Value = 80, Width = 80, VerticalAlignment = VerticalAlignment.Center };
            bottomPanel.Children.Add(xSlider);

            bottomPanel.Children.Add(new TextBlock { Text = "Y위치(%):", Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 4, 0) });
            var ySlider = new Slider { Minimum = 0, Maximum = 100, Value = 80, Width = 80, VerticalAlignment = VerticalAlignment.Center };
            bottomPanel.Children.Add(ySlider);

            bottomPanel.Children.Add(new TextBlock { Text = "표시크기:", Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 4, 0) });
            var sizeSlider = new Slider { Minimum = 30, Maximum = 300, Value = 100, Width = 80, VerticalAlignment = VerticalAlignment.Center };
            bottomPanel.Children.Add(sizeSlider);

            bool confirmed = false;
            var okBtn = new Button { Content = "✔ 확인", Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(16, 0, 0, 0) };
            okBtn.Click += (s2, e2) => { confirmed = true; cropWin.Close(); };
            bottomPanel.Children.Add(okBtn);

            Grid.SetRow(bottomPanel, 1);
            mainPanel.Children.Add(bottomPanel);
            cropWin.Content = mainPanel;
            cropWin.ShowDialog();

            if (!confirmed) return;

            // 원형 크롭 영역 계산 (캔버스 좌표 → 원본 이미지 좌표)
            double circleLeft = Canvas.GetLeft(circleOverlay);
            double circleTop = Canvas.GetTop(circleOverlay);
            double circleSize = circleOverlay.Width;

            if (circleSize < 10) { MessageBox.Show("영역을 드래그해서 선택해주세요."); return; }

            double scaleX = bitmapImage.PixelWidth / imgControl.ActualWidth;
            double scaleY = bitmapImage.PixelHeight / imgControl.ActualHeight;
            double scale = Math.Max(scaleX, scaleY);

            int srcX = Math.Max(0, (int)(circleLeft * scale));
            int srcY = Math.Max(0, (int)(circleTop * scale));
            int srcSize = Math.Max(10, (int)(circleSize * scale));
            srcSize = Math.Min(srcSize, Math.Min(bitmapImage.PixelWidth - srcX, bitmapImage.PixelHeight - srcY));

            var circleImg = CreateCircleCroppedImage(dlg.FileName, srcX, srcY, srcSize);

            _overlays.Add(new OverlayItem
            {
                Type = "image",
                Content = circleImg,
                X = xSlider.Value,
                Y = ySlider.Value,
                Size = (int)sizeSlider.Value,
                IsCircleCrop = true
            });

            StatusText.Text = $"볼링공 이미지 추가됨 (오버레이 {_overlays.Count}개)";
            ShowOverlayPanel();
            if (_scoreVisible) RefreshScoreOverlay();
            else if (_mp != null) ShowOverlaysOnly();
        }

        private string CreateCircleCroppedImage(string srcPath, int cropX = 0, int cropY = 0, int cropSize = 0)
        {
            using var src = new System.Drawing.Bitmap(srcPath);
            if (cropSize <= 0) cropSize = Math.Min(src.Width, src.Height);
            cropX = Math.Clamp(cropX, 0, src.Width - cropSize);
            cropY = Math.Clamp(cropY, 0, src.Height - cropSize);

            using var result = new System.Drawing.Bitmap(cropSize, cropSize);
            using var g = System.Drawing.Graphics.FromImage(result);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);

            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(0, 0, cropSize, cropSize);
            g.SetClip(path);

            g.DrawImage(src, new System.Drawing.Rectangle(0, 0, cropSize, cropSize),
                new System.Drawing.Rectangle(cropX, cropY, cropSize, cropSize), System.Drawing.GraphicsUnit.Pixel);

            var outPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BowlingVideoEditor",
                $"ball_{Guid.NewGuid():N}.png");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath));
            result.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
            return outPath;
        }

        private void AddText_Click(object sender, RoutedEventArgs e)
        {
            var win = new Window
            {
                Title = "📝 텍스트 추가", Width = 360, Height = 340,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)), ResizeMode = ResizeMode.NoResize
            };
            var sp = new StackPanel { Margin = new Thickness(16) };

            sp.Children.Add(new TextBlock { Text = "텍스트 내용", Foreground = Brushes.White, FontSize = 12 });
            var textBox = new TextBox { Background = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
                Foreground = Brushes.White, FontSize = 14, Padding = new Thickness(6, 4, 6, 4) };
            sp.Children.Add(textBox);

            sp.Children.Add(new TextBlock { Text = "X 위치 (%)", Foreground = Brushes.White, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) });
            var xSlider = new Slider { Minimum = 0, Maximum = 100, Value = 50 };
            sp.Children.Add(xSlider);

            sp.Children.Add(new TextBlock { Text = "Y 위치 (%)", Foreground = Brushes.White, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) });
            var ySlider = new Slider { Minimum = 0, Maximum = 100, Value = 90 };
            sp.Children.Add(ySlider);

            sp.Children.Add(new TextBlock { Text = "글씨 크기", Foreground = Brushes.White, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) });
            var sizeSlider = new Slider { Minimum = 16, Maximum = 80, Value = 36 };
            sp.Children.Add(sizeSlider);

            sp.Children.Add(new TextBlock { Text = "색상", Foreground = Brushes.White, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) });
            var colorCombo = new ComboBox { FontSize = 12 };
            colorCombo.Items.Add("white"); colorCombo.Items.Add("yellow"); colorCombo.Items.Add("red");
            colorCombo.Items.Add("green"); colorCombo.Items.Add("cyan"); colorCombo.SelectedIndex = 0;
            sp.Children.Add(colorCombo);

            bool ok = false;
            var btn = new Button { Content = "추가", Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            btn.Click += (s2, e2) => { ok = true; win.Close(); };
            sp.Children.Add(btn);
            win.Content = sp;
            win.ShowDialog();

            if (!ok || string.IsNullOrWhiteSpace(textBox.Text)) return;

            _overlays.Add(new OverlayItem
            {
                Type = "text",
                Content = textBox.Text.Trim(),
                X = xSlider.Value,
                Y = ySlider.Value,
                Size = (int)sizeSlider.Value,
                Color = colorCombo.SelectedItem?.ToString() ?? "white"
            });

            StatusText.Text = $"텍스트 추가됨: \"{textBox.Text.Trim()}\" (오버레이 {_overlays.Count}개)";
            ShowOverlayPanel();
            if (_scoreVisible) RefreshScoreOverlay();
            else if (_mp != null) ShowOverlaysOnly();
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath)) { MessageBox.Show("먼저 영상을 열어주세요."); return; }
            SyncScoreData();
            var dlg = new SaveFileDialog { Filter = "MP4|*.mp4", FileName = "bowling_output.mp4" };
            if (dlg.ShowDialog() != true) return;
            _mp?.Pause();
            StatusText.Text = "내보내는 중...";
            try
            {
                var p = new Progress<double>(v => ProgressBar.Value = v);
                var hasScore = _currentTimeline != null && _currentTimeline.Entries.Count > 0;
                var hasOverlays = _overlays.Count > 0;

                if (hasScore || hasOverlays)
                {
                    await _videoService.ExportFullAsync(_currentFilePath,
                        hasScore ? _currentTimeline : null,
                        hasOverlays ? _overlays : null,
                        100, 0.5, 0.5,
                        dlg.FileName, p);
                }
                else
                {
                    System.IO.File.Copy(_currentFilePath, dlg.FileName, true);
                }
                StatusText.Text = "내보내기 완료"; ProgressBar.Value = 0;
                MessageBox.Show("완료!", "내보내기", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"내보내기 오류:\n{ex.Message}\n\n{ex.InnerException?.Message}", "오류");
                StatusText.Text = "오류";
            }
        }

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "업데이트 확인...";
            try
            {
                var r = await _updateService.CheckForUpdateAsync();
                if (r == null) { MessageBox.Show("서버 연결 실패"); StatusText.Text = "준비"; return; }
                if (r.Value.Available)
                { if (MessageBox.Show($"새 버전: {r.Value.TagName}\n업데이트?", "업데이트", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                  { var p = new Progress<double>(v => ProgressBar.Value = v); await _updateService.DownloadAndApplyUpdateAsync(r.Value.DownloadUrl, p); } }
                else MessageBox.Show("최신 버전입니다.");
                StatusText.Text = "준비";
            }
            catch (Exception ex) { MessageBox.Show($"오류: {ex.Message}"); StatusText.Text = "준비"; }
        }

        private void ClipListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { if (ClipListBox.SelectedItem is VideoClip clip) { _currentFilePath = clip.FilePath; PlayFile(clip.FilePath); } }
        private void MoveClipUp_Click(object sender, RoutedEventArgs e)
        { var i = ClipListBox.SelectedIndex; if (i > 0) { _clips.Move(i, i-1); ClipListBox.SelectedIndex = i-1; } }
        private void MoveClipDown_Click(object sender, RoutedEventArgs e)
        { var i = ClipListBox.SelectedIndex; if (i >= 0 && i < _clips.Count-1) { _clips.Move(i, i+1); ClipListBox.SelectedIndex = i+1; } }
        private void RemoveClip_Click(object sender, RoutedEventArgs e)
        { if (ClipListBox.SelectedItem is VideoClip) _clips.RemoveAt(ClipListBox.SelectedIndex); }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _timer?.Stop(); _mp?.Stop(); _mp?.Dispose(); _libVLC?.Dispose(); _videoService?.CleanupTemp();

            // 종료 시 블로그 링크 열기
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://1st-life-2nd.tistory.com/",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        // ===== 타임라인 =====

        private void UpdateTimeline()
        {
            VideoTrack.Children.Clear();
            ScoreTrack.Children.Clear();
            AudioTrack.Children.Clear();
            TimeRuler.Children.Clear();

            double trackW = VideoTrack.ActualWidth;
            if (trackW <= 0) trackW = 800;

            long totalMs = _mp?.Length ?? 0;
            if (totalMs <= 0 && _clips.Count > 0)
            {
                // 클립 기반 총 길이 추정
                foreach (var c in _clips)
                    totalMs += (long)c.Duration.TotalMilliseconds;
            }
            if (totalMs <= 0) totalMs = 60000; // 기본 1분

            double pxPerMs = trackW / totalMs;

            // 시간 눈금자
            DrawTimeRuler(trackW, totalMs);

            // 영상 트랙 - 클립 블록
            if (_clips.Count > 0)
            {
                long offset = 0;
                for (int i = 0; i < _clips.Count; i++)
                {
                    var clip = _clips[i];
                    double x = offset * pxPerMs;
                    double w = clip.Duration.TotalMilliseconds * pxPerMs;
                    if (w < 2) w = 2;

                    var block = new System.Windows.Shapes.Rectangle
                    {
                        Width = w, Height = 40,
                        Fill = new SolidColorBrush(i == _selectedClipIdx
                            ? Color.FromRgb(80, 160, 255)
                            : Color.FromRgb(0, (byte)(120 + i * 15 % 80), (byte)(180 + i * 20 % 75))),
                        RadiusX = 3, RadiusY = 3,
                        Stroke = new SolidColorBrush(i == _selectedClipIdx ? Colors.Yellow : Color.FromRgb(80, 180, 255)),
                        StrokeThickness = i == _selectedClipIdx ? 2 : 1
                    };
                    Canvas.SetLeft(block, x);
                    Canvas.SetTop(block, 4);
                    VideoTrack.Children.Add(block);

                    // 클립 이름
                    var label = new TextBlock
                    {
                        Text = clip.FileName,
                        Foreground = Brushes.White,
                        FontSize = 9,
                        MaxWidth = w - 4
                    };
                    label.TextTrimming = TextTrimming.CharacterEllipsis;
                    Canvas.SetLeft(label, x + 4);
                    Canvas.SetTop(label, 8);
                    VideoTrack.Children.Add(label);

                    // 시간 표시
                    var timeLabel = new TextBlock
                    {
                        Text = $"{clip.StartTime:mm\\:ss} - {clip.EndTime:mm\\:ss}",
                        Foreground = new SolidColorBrush(Color.FromRgb(150, 200, 255)),
                        FontSize = 8
                    };
                    Canvas.SetLeft(timeLabel, x + 4);
                    Canvas.SetTop(timeLabel, 24);
                    VideoTrack.Children.Add(timeLabel);

                    offset += (long)clip.Duration.TotalMilliseconds;
                }
            }
            else if (!string.IsNullOrEmpty(_currentFilePath))
            {
                // 단일 영상 블록
                var block = new System.Windows.Shapes.Rectangle
                {
                    Width = trackW - 4, Height = 40,
                    Fill = new SolidColorBrush(Color.FromRgb(30, 100, 180)),
                    RadiusX = 3, RadiusY = 3,
                    Stroke = new SolidColorBrush(Color.FromRgb(80, 180, 255)),
                    StrokeThickness = 1
                };
                Canvas.SetLeft(block, 2); Canvas.SetTop(block, 4);
                VideoTrack.Children.Add(block);

                var label = new TextBlock
                {
                    Text = System.IO.Path.GetFileName(_currentFilePath),
                    Foreground = Brushes.White, FontSize = 9
                };
                Canvas.SetLeft(label, 6); Canvas.SetTop(label, 8);
                VideoTrack.Children.Add(label);
            }

            // 점수판 트랙
            if (_currentTimeline != null && _currentTimeline.Entries.Count > 0)
            {
                foreach (var entry in _currentTimeline.Entries)
                {
                    double x = entry.Timestamp.TotalMilliseconds * pxPerMs;
                    var marker = new System.Windows.Shapes.Rectangle
                    {
                        Width = Math.Max(20, 40 * pxPerMs * 1000),
                        Height = 30,
                        Fill = new SolidColorBrush(Color.FromArgb(180, 0, 180, 80)),
                        RadiusX = 2, RadiusY = 2
                    };
                    Canvas.SetLeft(marker, x); Canvas.SetTop(marker, 4);
                    ScoreTrack.Children.Add(marker);

                    var lbl = new TextBlock
                    {
                        Text = $"F{entry.FrameIndex}",
                        Foreground = Brushes.White, FontSize = 9
                    };
                    Canvas.SetLeft(lbl, x + 3); Canvas.SetTop(lbl, 8);
                    ScoreTrack.Children.Add(lbl);
                }
            }

            // 오디오 트랙 - 가짜 파형 (시각적 표현)
            DrawFakeWaveform(trackW);

            // 재생 헤드 업데이트
            UpdatePlayhead();
        }

        private void DrawTimeRuler(double trackW, long totalMs)
        {
            double rulerW = TimeRuler.ActualWidth > 0 ? TimeRuler.ActualWidth : trackW;
            double pxPerMs = rulerW / totalMs;

            // 적절한 간격 계산
            double intervalMs = 1000; // 1초
            if (totalMs > 600000) intervalMs = 60000; // 1분
            else if (totalMs > 60000) intervalMs = 10000; // 10초
            else if (totalMs > 10000) intervalMs = 5000; // 5초

            for (double ms = 0; ms <= totalMs; ms += intervalMs)
            {
                double x = ms * pxPerMs;
                var tick = new System.Windows.Shapes.Line
                {
                    X1 = x, X2 = x, Y1 = 14, Y2 = 24,
                    Stroke = new SolidColorBrush(Color.FromRgb(100, 100, 130)),
                    StrokeThickness = 1
                };
                TimeRuler.Children.Add(tick);

                var ts = TimeSpan.FromMilliseconds(ms);
                string fmt = totalMs > 600000 ? ts.ToString(@"mm\:ss") : ts.ToString(@"m\:ss");
                var lbl = new TextBlock
                {
                    Text = fmt, FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 160))
                };
                Canvas.SetLeft(lbl, x + 2); Canvas.SetTop(lbl, 2);
                TimeRuler.Children.Add(lbl);
            }
        }

        private void DrawFakeWaveform(double trackW)
        {
            var rng = new Random(42);
            double h = 50;
            int bars = (int)(trackW / 3);
            for (int i = 0; i < bars; i++)
            {
                double barH = rng.NextDouble() * h * 0.8 + h * 0.1;
                var bar = new System.Windows.Shapes.Rectangle
                {
                    Width = 2, Height = barH,
                    Fill = new SolidColorBrush(Color.FromArgb(180, 40, (byte)(160 + rng.Next(60)), 40))
                };
                Canvas.SetLeft(bar, i * 3);
                Canvas.SetTop(bar, (h - barH) / 2 + 4);
                AudioTrack.Children.Add(bar);
            }
        }

        private void UpdatePlayhead()
        {
            if (_mp == null || _mp.Length <= 0) return;
            double trackW = VideoTrack.ActualWidth;
            if (trackW <= 0) return;
            double pos = (double)_mp.Time / _mp.Length * trackW;
            Canvas.SetLeft(PlayheadLine, pos);
            PlayheadLine.Y2 = 160;
        }

        private void Track_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(VideoTrack);
            double trackW = VideoTrack.ActualWidth;
            if (trackW <= 0) return;

            // 클립 선택
            if (_clips.Count > 0)
            {
                long totalMs = 0;
                foreach (var c in _clips) totalMs += (long)c.Duration.TotalMilliseconds;
                if (totalMs <= 0) return;
                double pxPerMs = trackW / totalMs;
                long accMs = 0;

                for (int i = 0; i < _clips.Count; i++)
                {
                    long clipMs = (long)_clips[i].Duration.TotalMilliseconds;
                    double leftEdge = accMs * pxPerMs;
                    double rightEdge = (accMs + clipMs) * pxPerMs;

                    if (pos.X >= leftEdge && pos.X < rightEdge)
                    {
                        _selectedClipIdx = i;
                        UpdateTimeline(); // 선택 표시 갱신
                        break;
                    }
                    accMs += clipMs;
                }
            }

            // 재생 위치 이동
            if (_mp != null && _mp.Length > 0)
            {
                double ratio = pos.X / trackW;
                _mp.Time = (long)(ratio * _mp.Length);
            }
        }

        private void Track_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) { }
        private void Track_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e) { }

        private void DeleteSelectedClip_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedClipIdx < 0 || _selectedClipIdx >= _clips.Count)
            {
                MessageBox.Show("타임라인에서 삭제할 클립을 먼저 클릭하세요.");
                return;
            }

            var clip = _clips[_selectedClipIdx];
            if (MessageBox.Show($"클립 '{clip.FileName}' ({clip.StartTime:mm\\:ss} ~ {clip.EndTime:mm\\:ss})을 삭제하시겠습니까?",
                "클립 삭제", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _clips.RemoveAt(_selectedClipIdx);
                _selectedClipIdx = -1;
                UpdateTimeline();
                StatusText.Text = $"클립 삭제됨 (남은 클립: {_clips.Count}개)";
            }
        }

        // ===== 오버레이 편집 패널 =====

        private bool _suppressOvSlider;

        private void ShowOverlayPanel()
        {
            OverlayPanel.Visibility = Visibility.Visible;
            OverlayListBox.ItemsSource = null;
            OverlayListBox.ItemsSource = _overlays;
            if (_overlays.Count > 0)
                OverlayListBox.SelectedIndex = _overlays.Count - 1;
        }

        private void CloseOverlayPanel_Click(object sender, RoutedEventArgs e)
        {
            OverlayPanel.Visibility = Visibility.Collapsed;
        }

        private void OverlayList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OverlayListBox.SelectedItem is not OverlayItem ov) return;
            _suppressOvSlider = true;
            OvXSlider.Value = ov.X;
            OvYSlider.Value = ov.Y;
            OvSizeSlider.Value = ov.Size;
            _suppressOvSlider = false;
        }

        private void OvSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded || _suppressOvSlider) return;
            if (OverlayListBox.SelectedItem is not OverlayItem ov) return;

            ov.X = OvXSlider.Value;
            ov.Y = OvYSlider.Value;
            ov.Size = (int)OvSizeSlider.Value;

            // 실시간 갱신
            if (_scoreVisible) RefreshScoreOverlay();
            else if (_mp != null) ShowOverlaysOnly();
        }

        private void DeleteOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (OverlayListBox.SelectedItem is not OverlayItem ov) return;
            _overlays.Remove(ov);
            OverlayListBox.ItemsSource = null;
            OverlayListBox.ItemsSource = _overlays;

            if (_overlays.Count == 0)
            {
                OverlayPanel.Visibility = Visibility.Collapsed;
                if (_mp != null && !_scoreVisible)
                    _mp.SetLogoInt(VideoLogoOption.Enable, 0);
            }
            else
            {
                if (_scoreVisible) RefreshScoreOverlay();
                else if (_mp != null) ShowOverlaysOnly();
            }
            StatusText.Text = $"오버레이 삭제됨 (남은: {_overlays.Count}개)";
        }

        // ===== 개발자에게 커피사주기 =====

        private void BuyCoffee_Click(object sender, RoutedEventArgs e)
        {
            var win = new Window
            {
                Title = "☕ 개발자에게 커피사주기",
                Width = 400, Height = 480,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                ResizeMode = ResizeMode.NoResize
            };

            var sp = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20)
            };

            sp.Children.Add(new TextBlock
            {
                Text = "☕ 개발자에게 커피 한 잔 사주세요!",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 100)),
                FontSize = 18, FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });

            sp.Children.Add(new TextBlock
            {
                Text = "아래 QR코드를 스캔해주세요",
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            });

            // QR 이미지 로드 (리소스 파일)
            var qrPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "coffee_qr.png");
            if (!File.Exists(qrPath))
                qrPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "coffee_qr.png");

            if (File.Exists(qrPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(qrPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                var img = new Image
                {
                    Source = bitmap,
                    Width = 280, Height = 280,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                sp.Children.Add(img);
            }
            else
            {
                sp.Children.Add(new TextBlock
                {
                    Text = "QR 이미지를 찾을 수 없습니다.\n(Resources/coffee_qr.png)",
                    Foreground = Brushes.Red,
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 20)
                });
            }

            sp.Children.Add(new TextBlock
            {
                Text = "감사합니다! 🙏",
                Foreground = new SolidColorBrush(Color.FromRgb(0, 204, 106)),
                FontSize = 16, FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 16, 0, 0)
            });

            var closeBtn = new Button
            {
                Content = "닫기",
                Padding = new Thickness(20, 8, 20, 8),
                Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            closeBtn.Click += (s2, e2) => win.Close();
            sp.Children.Add(closeBtn);

            win.Content = sp;
            win.ShowDialog();
        }
    }
}
