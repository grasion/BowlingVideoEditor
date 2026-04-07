using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BowlingVideoEditor.Models
{
    public class BowlingScore : INotifyPropertyChanged
    {
        private string _playerName = "Player";
        private ObservableCollection<FrameScore> _frames = new();
        private int _totalScore;

        public string PlayerName
        {
            get => _playerName;
            set { _playerName = value; OnPropertyChanged(); }
        }

        public ObservableCollection<FrameScore> Frames
        {
            get => _frames;
            set { _frames = value; OnPropertyChanged(); }
        }

        public int TotalScore
        {
            get => _totalScore;
            set { _totalScore = value; OnPropertyChanged(); }
        }

        public BowlingScore()
        {
            for (int i = 0; i < 10; i++)
                Frames.Add(new FrameScore { FrameNumber = i + 1 });
        }

        public void RecalculateTotal()
        {
            int total = 0;
            foreach (var frame in Frames)
                total += frame.Score;
            TotalScore = total;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class FrameScore : INotifyPropertyChanged
    {
        private int _frameNumber;
        private string _throw1 = "";
        private string _throw2 = "";
        private string _throw3 = "";
        private int _score;

        public int FrameNumber
        {
            get => _frameNumber;
            set { _frameNumber = value; OnPropertyChanged(); }
        }

        public string Throw1
        {
            get => _throw1;
            set { _throw1 = value; OnPropertyChanged(); }
        }

        public string Throw2
        {
            get => _throw2;
            set { _throw2 = value; OnPropertyChanged(); }
        }

        public string Throw3
        {
            get => _throw3;
            set { _throw3 = value; OnPropertyChanged(); }
        }

        public int Score
        {
            get => _score;
            set { _score = value; OnPropertyChanged(); }
        }

        public bool IsTenthFrame => FrameNumber == 10;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 시간대별 점수 항목 - 특정 시간에 특정 프레임의 점수가 표시됨
    /// </summary>
    public class ScoreTimeline : INotifyPropertyChanged
    {
        private ObservableCollection<ScoreEntry> _entries = new();
        private BowlingScore _score = new();

        public ObservableCollection<ScoreEntry> Entries
        {
            get => _entries;
            set { _entries = value; OnPropertyChanged(); }
        }

        public BowlingScore Score
        {
            get => _score;
            set { _score = value; OnPropertyChanged(); }
        }

        /// <summary>점수판 X 위치 비율 (0.0~1.0)</summary>
        public double PositionX { get; set; } = 0.025;
        /// <summary>점수판 Y 위치 비율 (0.0~1.0)</summary>
        public double PositionY { get; set; } = 0.02;
        /// <summary>점수판 크기 비율 (0.5~2.0)</summary>
        public double ScalePercent { get; set; } = 1.0;
        /// <summary>점수판 폰트 크기 기준값</summary>
        public int FontSize { get; set; } = 28;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 특정 시간에 표시할 프레임 점수 정보
    /// </summary>
    public class ScoreEntry : INotifyPropertyChanged
    {
        private TimeSpan _timestamp;
        private int _frameIndex;
        private string _throw1 = "";
        private string _throw2 = "";
        private string _throw3 = "";
        private int _cumulativeScore;

        /// <summary>영상에서 이 점수가 나타나는 시간</summary>
        public TimeSpan Timestamp
        {
            get => _timestamp;
            set { _timestamp = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimestampText)); }
        }

        public string TimestampText => Timestamp.ToString(@"hh\:mm\:ss");

        /// <summary>프레임 번호 (1~10)</summary>
        public int FrameIndex
        {
            get => _frameIndex;
            set { _frameIndex = value; OnPropertyChanged(); }
        }

        public string Throw1
        {
            get => _throw1;
            set { _throw1 = value; OnPropertyChanged(); }
        }

        public string Throw2
        {
            get => _throw2;
            set { _throw2 = value; OnPropertyChanged(); }
        }

        public string Throw3
        {
            get => _throw3;
            set { _throw3 = value; OnPropertyChanged(); }
        }

        /// <summary>이 프레임까지의 누적 점수</summary>
        public int CumulativeScore
        {
            get => _cumulativeScore;
            set { _cumulativeScore = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>영상 위에 올릴 오버레이 항목 (이미지 또는 텍스트)</summary>
    public class OverlayItem
    {
        public string Type { get; set; } = "text"; // "text" or "image"
        public string Content { get; set; } = ""; // 텍스트 내용 또는 이미지 경로
        public double X { get; set; } = 50; // 위치 비율 (0~100)
        public double Y { get; set; } = 50;
        public int Size { get; set; } = 40; // 텍스트 폰트 크기 또는 이미지 크기
        public string Color { get; set; } = "white";
        public bool IsCircleCrop { get; set; } // 이미지를 원형으로 크롭
    }
}
