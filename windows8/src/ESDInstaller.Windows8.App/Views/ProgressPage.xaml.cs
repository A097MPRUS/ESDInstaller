using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ESDInstaller.Windows8.Core.Models;
using ESDInstaller.Windows8.Services;

namespace ESDInstaller.Windows8.Views;

public partial class ProgressPage : Page
{
    private readonly WizardCoordinator _coordinator;
    private readonly DateTime _started = DateTime.UtcNow;
    private readonly DispatcherTimer _timer;
    private string? _logPath;
    private bool _failed;
    public ProgressPage(WizardCoordinator coordinator)
    {
        InitializeComponent(); _coordinator = coordinator;
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
            (sender, args) => Elapsed.Text = App.Services.Localizer.Format("ElapsedTime", (DateTime.UtcNow - _started).ToString(@"hh\:mm\:ss")), Dispatcher);
        _timer.Start();
    }
    public async System.Threading.Tasks.Task ExecuteAsync(InstallationPlan plan)
    {
        var progress = new Progress<ProgressMessage>(UpdateProgress);
        try
        {
            var result = await App.Services.Worker.ExecuteAsync(plan, progress);
            _logPath = result.LogPath ?? _logPath; EnableLogButtons();
            if (result.ElevationCancelled)
            {
                OperationRing.Visibility = Visibility.Collapsed;
                ResultBanner.SetWarning(App.Services.Localizer.Get("AdministratorRequired"), App.Services.Localizer.Get("AdministratorCancelled"));
                ResultBanner.Visibility = Visibility.Visible; _coordinator.InstallationFinished(false); return;
            }
            if (result.ExitCode != 0)
            {
                OperationRing.Visibility = Visibility.Collapsed;
                ReportFailure(App.Services.Localizer.Get("ProgressInstallationFailed"), ExitHint(result.ExitCode) + App.Services.Localizer.Format("WorkerExitFailure", result.ExitCode));
                ResultBanner.Visibility = Visibility.Visible; _coordinator.InstallationFinished(false); return;
            }
            Overall.Value = 100; Percent.Text = "100%"; OperationRing.Visibility = Visibility.Collapsed;
            ResultBanner.SetSuccess(App.Services.Localizer.Get("InstallationSuccessTitle"), App.Services.Localizer.Get("InstallationSuccessText"));
            ResultBanner.Visibility = Visibility.Visible; SuccessPanel.Visibility = Visibility.Visible;
            _coordinator.InstallationFinished(true);
        }
        catch (Exception exception)
        {
            OperationRing.Visibility = Visibility.Collapsed;
            ReportFailure(App.Services.Localizer.Get("ErrorUnexpected"), exception.Message);
            ResultBanner.Visibility = Visibility.Visible; _coordinator.InstallationFinished(false);
        }
        finally { _timer.Stop(); }
    }
    private void UpdateProgress(ProgressMessage message)
    {
        Overall.Value = message.OverallPercent; Percent.Text = message.OverallPercent + "%";
        Operation.Text = App.Services.Localizer.Get(message.MessageKey); Detail.Text = message.Detail;
        _logPath = message.LogPath ?? _logPath; EnableLogButtons();
        Log.AppendText(message.TimestampUtc.ToLocalTime().ToString("HH:mm:ss") + "  " + Operation.Text +
                       (string.IsNullOrWhiteSpace(message.Detail) ? "" : " — " + message.Detail) + Environment.NewLine);
        Log.ScrollToEnd();
        if (message.IsError)
        {
            _failed = true; ResultBanner.SetError(Operation.Text, message.Detail); ResultBanner.Visibility = Visibility.Visible;
        }
    }
    /// <summary>
    /// The worker reports the real reason over the progress channel before it exits, so that message is
    /// shown first. A later generic exit-code or exception message must not replace it, or the user loses
    /// the only explanation for why a destructive operation stopped.
    /// </summary>
    private void ReportFailure(string message, string detail)
    {
        if (_failed)
        {
            Log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message +
                           (string.IsNullOrWhiteSpace(detail) ? "" : " — " + detail) + Environment.NewLine);
            Log.ScrollToEnd();
            return;
        }
        _failed = true;
        ResultBanner.SetError(message, detail);
        ResultBanner.Visibility = Visibility.Visible;
    }
    /// <summary>Explains the worker's documented exit codes using text that is already translated.</summary>
    private static string ExitHint(int exitCode)
    {
        switch (exitCode)
        {
            case 740: return App.Services.Localizer.Get("AdministratorRequired") + " ";
            case 64:
            case 65:
            case 66: return App.Services.Localizer.Get("ErrorWorkerStart") + " ";
            default: return string.Empty;
        }
    }
    private void EnableLogButtons() { var exists = !string.IsNullOrWhiteSpace(_logPath) && File.Exists(_logPath); OpenLog.IsEnabled = SaveLog.IsEnabled = exists; LogButtons.Visibility = exists ? Visibility.Visible : Visibility.Collapsed; }
    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        if (_logPath != null && File.Exists(_logPath)) Process.Start(new ProcessStartInfo(_logPath) { UseShellExecute = true });
    }
    private void SaveLog_Click(object sender, RoutedEventArgs e)
    {
        if (_logPath == null || !File.Exists(_logPath)) return;
        var dialog = new SaveFileDialog { FileName = Path.GetFileName(_logPath), Filter = "Log files (*.log)|*.log|All files (*.*)|*.*" };
        if (dialog.ShowDialog() == true) File.Copy(_logPath, dialog.FileName, true);
    }
    private void RestartLater_Click(object sender, RoutedEventArgs e) => MessageBox.Show(Window.GetWindow(this), App.Services.Localizer.Get("RestartLaterMessage"), "ESD Installer", MessageBoxButton.OK, MessageBoxImage.Information);
    private void RestartNow_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Window.GetWindow(this), App.Services.Localizer.Get("RestartConfirmText"), App.Services.Localizer.Get("RestartConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), "/r /t 0") { UseShellExecute = false, CreateNoWindow = true });
    }
}
