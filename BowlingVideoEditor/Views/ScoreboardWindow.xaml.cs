using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using BowlingVideoEditor.Models;

namespace BowlingVideoEditor.Views
{
    public partial class ScoreboardWindow : Window
    {
        public BowlingScore Score { get; private set; } = new();
        public ScoreTimeline Timeline { get; private set; } = new();

        private TextBox[] _t1Boxes;
        private TextBox[] _t2Boxes;
        private TextBox[] _scoreBoxes;
        private TextBox _f10T3Box;

        public ScoreboardWindow()
        {
            InitializeComponent();

            _t1Boxes = new[] { F1T1, F2T1, F3T1, F4T1, F5T1, F6T1, F7T1, F8T1, F9T1, F10T1 };
            _t2Boxes = new[] { F1T2, F2T2, F3T2, F4T2, F5T2, F6T2, F7T2, F8T2, F9T2, F10T2 };
            _scoreBoxes = new[] { F1Score, F2Score, F3Score, F4Score, F5Score, F6Score, F7Score, F8Score, F9Score, F10Score };
            _f10T3Box = F10T3;

            // 10프레임 제외 3투구 비활성화 (시간대별 입력에서도)
            EntryT3Box.IsEnabled = false;

            // 1투구 입력 시 자동 X/스페어 처리
            foreach (var box in _t1Boxes)
                box.LostFocus += ThrowBox_LostFocus;
            foreach (var box in _t2Boxes)
                box.LostFocus += ThrowBox_LostFocus;

            // 프레임 콤보 변경 시 3투구 활성화/비활성화
            EntryFrameCombo.SelectionChanged += (s, e) =>
            {
                EntryT3Box.IsEnabled = EntryFrameCombo.SelectedIndex == 9;
                if (!EntryT3Box.IsEnabled) EntryT3Box.Text = "";
            };

            Timeline.Entries = new ObservableCollection<ScoreEntry>();
            TimelineGrid.ItemsSource = Timeline.Entries;
        }

        private void ThrowBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var box = sender as TextBox;
            if (box == null) return;

            // 어떤 프레임의 어떤 투구인지 찾기
            for (int i = 0; i < 10; i++)
            {
                if (box == _t1Boxes[i])
                {
                    ApplyBowlingRules(i);
                    break;
                }
                if (box == _t2Boxes[i])
                {
                    ApplyBowlingRules(i);
                    break;
                }
            }
            RecalculateAllScores();
        }

        private void ApplyBowlingRules(int frameIdx)
        {
            var t1Text = _t1Boxes[frameIdx].Text.Trim().ToUpper();
            var t2Text = _t2Boxes[frameIdx].Text.Trim().ToUpper();

            // 1투구에 10 입력 → X 표기
            if (t1Text == "10")
            {
                _t1Boxes[frameIdx].Text = "X";
                t1Text = "X";
            }

            if (frameIdx < 9) // 1~9프레임
            {
                if (t1Text == "X")
                {
                    // 스트라이크면 2투구 비활성화
                    _t2Boxes[frameIdx].Text = "";
                    _t2Boxes[frameIdx].IsEnabled = false;
                }
                else
                {
                    _t2Boxes[frameIdx].IsEnabled = true;

                    // 1투구 + 2투구 합이 10이면 스페어(/)
                    if (int.TryParse(t1Text, out int v1) && int.TryParse(t2Text, out int v2))
                    {
                        if (v1 + v2 == 10)
                            _t2Boxes[frameIdx].Text = "/";
                        else if (v1 + v2 > 10)
                            _t2Boxes[frameIdx].Text = ""; // 잘못된 입력 초기화
                    }
                    else if (t2Text == "10" && (int.TryParse(t1Text, out int v1b) && v1b == 0))
                    {
                        _t2Boxes[frameIdx].Text = "/";
                    }
                }
            }
            else // 10프레임
            {
                // 10프레임은 특별 규칙
                if (t1Text == "X")
                {
                    _t2Boxes[frameIdx].IsEnabled = true;
                    _f10T3Box.IsEnabled = true;
                }
                else if (int.TryParse(t1Text, out int v1))
                {
                    _t2Boxes[frameIdx].IsEnabled = true;
                    if (int.TryParse(t2Text, out int v2) && v1 + v2 == 10)
                    {
                        _t2Boxes[frameIdx].Text = "/";
                        _f10T3Box.IsEnabled = true;
                    }
                    else if (t2Text == "/" || t2Text == "X")
                    {
                        _f10T3Box.IsEnabled = true;
                    }
                    else if (int.TryParse(t2Text, out int v2b) && v1 + v2b < 10)
                    {
                        _f10T3Box.IsEnabled = false;
                        _f10T3Box.Text = "";
                    }
                    else
                    {
                        _f10T3Box.IsEnabled = false;
                    }
                }

                // 10프레임 2투구가 10이면 X
                if (_t2Boxes[frameIdx].Text.Trim() == "10")
                    _t2Boxes[frameIdx].Text = "X";
                if (_f10T3Box.Text.Trim() == "10")
                    _f10T3Box.Text = "X";
            }
        }

        private int ParseThrow(string text)
        {
            text = text.Trim().ToUpper();
            if (text == "X") return 10;
            if (text == "/") return -1; // 스페어는 컨텍스트에 따라 다름
            if (int.TryParse(text, out int v)) return v;
            return 0;
        }

        private void RecalculateAllScores()
        {
            // 간단한 누적 점수 계산 (스트라이크/스페어 보너스 없이 기본 합산)
            int cumulative = 0;
            for (int i = 0; i < 10; i++)
            {
                int t1 = ParseThrow(_t1Boxes[i].Text);
                int t2 = ParseThrow(_t2Boxes[i].Text);

                if (t1 == 10 && i < 9) // 스트라이크 (1~9프레임)
                {
                    cumulative += 10;
                }
                else if (t2 == -1) // 스페어
                {
                    cumulative += 10;
                }
                else
                {
                    cumulative += t1 + Math.Max(0, t2);
                }

                if (i == 9) // 10프레임 3투구
                {
                    int t3 = ParseThrow(_f10T3Box.Text);
                    if (t3 > 0) cumulative += t3;
                }

                if (t1 > 0 || t2 != 0 || !string.IsNullOrWhiteSpace(_t1Boxes[i].Text))
                    _scoreBoxes[i].Text = cumulative.ToString();
            }
            UpdateTotal();
        }

        private void AddEntry_Click(object sender, RoutedEventArgs e)
        {
            if (!TimeSpan.TryParse(EntryTimeBox.Text, out var timestamp))
            {
                MessageBox.Show("시간 형식이 올바르지 않습니다. (hh:mm:ss)", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int frameIndex = EntryFrameCombo.SelectedIndex + 1;
            int.TryParse(EntryCumBox.Text, out int cumScore);

            string t1 = EntryT1Box.Text.Trim();
            string t2 = EntryT2Box.Text.Trim();
            string t3 = EntryT3Box.Text.Trim();

            // 볼링 규칙 자동 적용
            if (t1 == "10") t1 = "X";
            if (frameIndex < 10 && t1 != "X" && int.TryParse(t1, out int v1) && int.TryParse(t2, out int v2) && v1 + v2 == 10)
                t2 = "/";

            var entry = new ScoreEntry
            {
                Timestamp = timestamp,
                FrameIndex = frameIndex,
                Throw1 = t1,
                Throw2 = t2,
                Throw3 = frameIndex == 10 ? t3 : "",
                CumulativeScore = cumScore
            };

            int insertIdx = 0;
            for (int i = 0; i < Timeline.Entries.Count; i++)
            {
                if (Timeline.Entries[i].Timestamp <= timestamp)
                    insertIdx = i + 1;
                else break;
            }
            Timeline.Entries.Insert(insertIdx, entry);

            EntryT1Box.Text = "";
            EntryT2Box.Text = "";
            EntryT3Box.Text = "";
            EntryCumBox.Text = "";
        }

        private void RemoveEntry_Click(object sender, RoutedEventArgs e)
        {
            if (TimelineGrid.SelectedItem is ScoreEntry entry)
                Timeline.Entries.Remove(entry);
        }

        private void ApplyToBoard_Click(object sender, RoutedEventArgs e)
        {
            foreach (var entry in Timeline.Entries)
            {
                int idx = entry.FrameIndex - 1;
                if (idx < 0 || idx > 9) continue;

                _t1Boxes[idx].Text = entry.Throw1;
                _t2Boxes[idx].Text = entry.Throw2;
                if (idx == 9)
                    _f10T3Box.Text = entry.Throw3;
                _scoreBoxes[idx].Text = entry.CumulativeScore > 0 ? entry.CumulativeScore.ToString() : "";
            }
            UpdateTotal();
        }

        private void UpdateTotal()
        {
            int total = 0;
            for (int i = 9; i >= 0; i--)
            {
                if (int.TryParse(_scoreBoxes[i].Text, out int val) && val > 0)
                {
                    total = val;
                    break;
                }
            }
            TotalScoreText.Text = total.ToString();
        }

        private void SyncScoreFromBoard()
        {
            Score.PlayerName = PlayerNameBox.Text;
            for (int i = 0; i < 10; i++)
            {
                Score.Frames[i].Throw1 = _t1Boxes[i].Text;
                Score.Frames[i].Throw2 = _t2Boxes[i].Text;
                if (i == 9)
                    Score.Frames[i].Throw3 = _f10T3Box.Text;
                int.TryParse(_scoreBoxes[i].Text, out int s);
                Score.Frames[i].Score = s;
            }
            UpdateTotal();
            int.TryParse(TotalScoreText.Text, out int t);
            Score.TotalScore = t;
            Timeline.Score = Score;

            if (Timeline.Entries.Count == 0)
                GenerateEntriesFromBoard();
        }

        private void GenerateEntriesFromBoard()
        {
            for (int i = 0; i < 10; i++)
            {
                var t1 = _t1Boxes[i].Text.Trim();
                var t2 = _t2Boxes[i].Text.Trim();
                var t3 = (i == 9) ? _f10T3Box.Text.Trim() : "";
                int.TryParse(_scoreBoxes[i].Text, out int cumScore);

                if (!string.IsNullOrEmpty(t1) || !string.IsNullOrEmpty(t2) || cumScore > 0)
                {
                    Timeline.Entries.Add(new ScoreEntry
                    {
                        Timestamp = TimeSpan.Zero,
                        FrameIndex = i + 1,
                        Throw1 = t1,
                        Throw2 = t2,
                        Throw3 = t3,
                        CumulativeScore = cumScore
                    });
                }
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            SyncScoreFromBoard();

            if (Timeline.Entries.Count == 0)
            {
                MessageBox.Show("점수를 입력해주세요.\n\n" +
                    "방법 1: 점수판에 직접 투구/점수 입력\n" +
                    "방법 2: 아래 시간대별 입력으로 프레임별 추가",
                    "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
