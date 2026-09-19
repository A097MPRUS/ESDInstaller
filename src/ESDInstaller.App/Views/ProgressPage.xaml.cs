using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using ESDInstaller.Core.Models;
using ESDInstaller.Core.Services;
using ESDInstaller.Services;

namespace ESDInstaller.Views;

public sealed partial class ProgressPage : Page
{
    private readonly WizardCoordinator _coordinator;
    private readonly Localizer _text = App.Services.Localizer;
    private readonly DateTime _started = DateTime.UtcNow;
    private readonly DispatcherQueueTimer _timer;
    private string? _logPath;
    private bool _succeeded;
    private bool _failed;
    private readonly bool _secondaryDisk;

    public ProgressPage(WizardCoordinator coordinator)
    {
        InitializeComponent();
        _coordinator = coordinator;
        _secondaryDisk = coordinator.Session.DestinationDisk?.IsSystem == false;
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => ElapsedText.Text = _text.Format("ElapsedTime", (DateTime.UtcNow - _started).ToString(@"hh\:mm\:ss"));
        _timer.Start();
    }

    public async Task ExecuteAsync(InstallationPlan plan)
    {
        var progress = new Progress<ProgressMessage>(HandleProgress);
        try
        {
            var result = await App.Services.Worker.ExecuteAsync(plan, progress);
            if (result.ElevationCancelled)
            {
                HandleFailure(_text.Get("AdministratorCancelled"), string.Empty);
                return;
            }
            _logPath = result.LogPath ?? _logPath;
            if (result.ExitCode != 0 && !_succeeded)
                ReportFailure(_text.Format("WorkerExitFailure", result.ExitCode), ExitHint(result.ExitCode));
        }
        catch (ESDInstallerException exception) { ReportFailure(_text.Get(exception.MessageKey), exception.TechnicalDetail); }
        catch (Exception exception) { ReportFailure(_text.Get("ErrorUnexpected"), exception.Message); }
        finally
        {
            _timer.Stop();
            _coordinator.InstallationFinished(_succeeded);
            LogButtons.Visibility = string.IsNullOrWhiteSpace(_logPath) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void HandleProgress(ProgressMessage message)
    {
        _logPath = message.LogPath ?? _logPath;
        OverallProgress.Value = message.OverallPercent;
        PercentText.Text = $"{message.OverallPercent}%";
        OperationText.Text = _text.Get(message.MessageKey);
        DetailText.Text = message.Detail;
        LogText.Text += $"{message.TimestampUtc.ToLocalTime():HH:mm:ss}  {_text.Get(message.MessageKey)}  {message.Detail}{Environment.NewLine}";
        if (message.Stage == InstallationStage.Completed)
        {
            _succeeded = true;
            OperationRing.IsActive = false;
            OperationText.Text = _text.Get("ProgressInstallationCompleted");
            ResultBar.Severity = InfoBarSeverity.Success;
            ResultBar.Title = _text.Get("InstallationSuccessTitle");
            ResultBar.Message = _text.Get(_secondaryDisk ? "InstallationSuccessSecondaryText" : "InstallationSuccessText");
            ResultBar.IsOpen = true;
            SuccessPanel.Visibility = Visibility.Visible;
        }
        else if (message.IsError || message.Stage == InstallationStage.Failed)
        {
            HandleFailure(_text.Get(message.MessageKey), message.Detail);
        }
    }

    private void HandleFailure(string message, string detail)
    {
        _succeeded = false;
        _failed = true;
        OperationRing.IsActive = false;
        OperationText.Text = _text.Get("ProgressInstallationFailed");
        ResultBar.Severity = InfoBarSeverity.Error;
        ResultBar.Title = message;
        ResultBar.Message = detail;
        ResultBar.IsOpen = true;
        LogText.Text += $"{DateTime.Now:HH:mm:ss}  {message}  {detail}{Environment.NewLine}";
    }

    /// <summary>
    /// The worker sends the real reason over the progress channel just before it exits, so that message
    /// normally arrives first. A later generic exit-code or exception message must not replace it, or the
    /// user is left without the explanation for a stopped destructive operation. The suppressed message is
    /// still appended to the log.
    /// </summary>
    private void ReportFailure(string message, string detail)
    {
        if (_failed)
        {
            LogText.Text += $"{DateTime.Now:HH:mm:ss}  {message}  {detail}{Environment.NewLine}";
            return;
        }
        HandleFailure(message, detail);
    }

    /// <summary>Explains the worker's documented exit codes using text that is already translated.</summary>
    private string ExitHint(int exitCode) => exitCode switch
    {
        740 => _text.Get("AdministratorRequired") + " ",
        64 or 65 or 66 => _text.Get("ErrorWorkerStart") + " ",
        _ => string.Empty
    };

    private void OpenLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_logPath) && File.Exists(_logPath))
            Process.Start(new ProcessStartInfo(_logPath) { UseShellExecute = true });
    }

    private async void SaveLogAsButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_logPath) || !File.Exists(_logPath)) return;
        var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(_logPath) };
        picker.FileTypeChoices.Add(_text.Get("LogFileType"), new List<string> { ".log" });
        var window = App.MainWindowInstance;
        if (window is null) return;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        var file = await picker.PickSaveFileAsync();
        if (file is not null) File.Copy(_logPath, file.Path, overwrite: true);
    }

    private async void RestartNowButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _text.Get("RestartConfirmTitle"),
            Content = _text.Get("RestartConfirmText"),
            PrimaryButtonText = _text.Get("RestartNow"),
            CloseButtonText = _text.Get("Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "/r", "/t", "0" }
        });
    }

    private void RestartLaterButton_Click(object sender, RoutedEventArgs e)
    {
        SuccessPanel.Visibility = Visibility.Collapsed;
        DetailText.Text = _text.Get("RestartLaterMessage");
    }
}
