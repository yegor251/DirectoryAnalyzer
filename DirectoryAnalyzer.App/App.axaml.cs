using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DirectoryAnalyzer.App.ViewModels;

namespace DirectoryAnalyzer.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var analyzer = new DirectoryAnalyzer.Core.Services.DirectoryAnalyzer();
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(analyzer)
            };

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}

