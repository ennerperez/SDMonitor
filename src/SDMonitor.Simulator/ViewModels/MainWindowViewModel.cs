using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using SDMonitor.Simulator.Services;

namespace SDMonitor.Simulator.ViewModels
{
    public sealed class MainWindowViewModel : ViewModelBase
    {
        private readonly DashboardSimulationService _dashboard;
        private readonly DispatcherTimer _timer;
        private string _statusText = "Dashboard mirror ready";
        private string _refreshText = "Live";

        public MainWindowViewModel()
            : this(new DashboardSimulationService())
        {
        }

        public MainWindowViewModel(DashboardSimulationService dashboard)
        {
            _dashboard = dashboard;
            Keys = new ObservableCollection<StreamDeckKeyViewModel>(_dashboard.CreateKeys());
            RefreshSettingsCommand = new DelegateCommand(RefreshSettings);

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_dashboard.RefreshIntervalMilliseconds)
            };
            _timer.Tick += (_, _) => Refresh();
            Refresh();
            _timer.Start();
        }

        public string Title { get; } = "Stream Deck Mini";

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public string RefreshText
        {
            get => _refreshText;
            private set => SetProperty(ref _refreshText, value);
        }

        public ObservableCollection<StreamDeckKeyViewModel> Keys { get; }

        public ICommand RefreshSettingsCommand { get; }

        private void Refresh()
        {
            var update = _dashboard.Update(Keys);
            StatusText = update.StatusText;
            RefreshText = update.RefreshText;
        }

        private void RefreshSettings()
        {
            try
            {
                var update = _dashboard.ReloadPreferences(Keys);
                _timer.Interval = TimeSpan.FromMilliseconds(_dashboard.RefreshIntervalMilliseconds);
                StatusText = update.StatusText;
                RefreshText = update.RefreshText;
            }
            catch (Exception ex)
            {
                StatusText = $"Preferences reload failed: {ex.Message}";
                RefreshText = $"{DateTimeOffset.Now:HH:mm:ss}";
            }
        }
    }
}
