using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using PoeEditor.Core.Services;

namespace PoeEditor.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handlers
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        FileLogService.Instance.LogInfo("Application startup complete");
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        FileLogService.Instance.LogError("Unhandled UI exception", e.Exception);

        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nDetails have been logged.",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true; // Prevent app crash
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        FileLogService.Instance.LogError($"Unhandled domain exception (IsTerminating={e.IsTerminating})", ex);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        FileLogService.Instance.LogError("Unobserved task exception", e.Exception);
        e.SetObserved(); // Prevent app crash
    }

    protected override void OnExit(ExitEventArgs e)
    {
        FileLogService.Instance.LogInfo($"Application exit (code={e.ApplicationExitCode})");
        base.OnExit(e);
    }
}

