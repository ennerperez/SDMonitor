using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SDMonitor.Simulator.Services;
using SDMonitor.Simulator.ViewModels;
using SDMonitor.Simulator.Views;

namespace SDMonitor.Simulator;

public sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(new DashboardSimulationService())
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
