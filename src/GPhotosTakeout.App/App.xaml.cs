using GPhotosTakeout.App.Services;
using Microsoft.UI.Xaml;

namespace GPhotosTakeout.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        // No telemetry: a crash is captured to a local dump the user can attach to an issue.
        UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        => CrashLogger.Write(e.Exception);

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
