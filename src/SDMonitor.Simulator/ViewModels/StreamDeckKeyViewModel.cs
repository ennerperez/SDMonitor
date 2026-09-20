using Avalonia.Media;

namespace SDMonitor.Simulator.ViewModels
{
    public sealed class StreamDeckKeyViewModel : ViewModelBase
    {
        public const double TrackWidth = 92;

        private string _title = "";
        private string _value = "";
        private double _percent;
        private double _progressWidth;
        private double _progressHeight = 7;
        private double _titleFontSize = 13;
        private double _valueFontSize = 24;
        private IBrush _backgroundBrush = Brush("#080A0E");
        private IBrush _borderBrush = Brush("#00C8FF");
        private IBrush _progressBrush = Brush("#00C8FF");
        private IBrush _titleBrush = Brush("#AAB4BE");
        private IBrush _valueBrush = Brush("#F5F8FA");

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public double Percent
        {
            get => _percent;
            set
            {
                if (SetProperty(ref _percent, value))
                {
                    ProgressWidth = TrackWidth * value / 100.0;
                }
            }
        }

        public double ProgressWidth
        {
            get => _progressWidth;
            private set => SetProperty(ref _progressWidth, value);
        }

        public double ProgressHeight
        {
            get => _progressHeight;
            set => SetProperty(ref _progressHeight, value);
        }

        public double TitleFontSize
        {
            get => _titleFontSize;
            set => SetProperty(ref _titleFontSize, value);
        }

        public double ValueFontSize
        {
            get => _valueFontSize;
            set => SetProperty(ref _valueFontSize, value);
        }

        public IBrush BackgroundBrush
        {
            get => _backgroundBrush;
            set => SetProperty(ref _backgroundBrush, value);
        }

        public IBrush BorderBrush
        {
            get => _borderBrush;
            set => SetProperty(ref _borderBrush, value);
        }

        public IBrush ProgressBrush
        {
            get => _progressBrush;
            set => SetProperty(ref _progressBrush, value);
        }

        public IBrush TitleBrush
        {
            get => _titleBrush;
            set => SetProperty(ref _titleBrush, value);
        }

        public IBrush ValueBrush
        {
            get => _valueBrush;
            set => SetProperty(ref _valueBrush, value);
        }

        public static IBrush Brush(string color)
        {
            return SolidColorBrush.Parse(color);
        }
    }
}
