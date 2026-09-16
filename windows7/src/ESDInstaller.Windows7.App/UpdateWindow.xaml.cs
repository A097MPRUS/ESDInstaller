using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using ESDInstaller.Windows7.Services;

namespace ESDInstaller.Windows7;

public partial class UpdateWindow : Window
{
    private readonly UpdateManifest _manifest;
    private CancellationTokenSource? _downloadCancellation;
    private bool _downloading;

    public UpdateWindow(UpdateManifest manifest)
    {
        _manifest = manifest;
        InitializeComponent();
        FlowDirection = App.Services.Settings.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Title = App.Services.Localizer.Get("UpdateTitle");
        HeadingText.Text = App.Services.Localizer.Get("UpdateAvailableHeading");
        AvailableVersionText.Text = App.Services.Localizer.Format("UpdateVersionAvailable", manifest.Version);
        CurrentVersionText.Text = App.Services.Localizer.Format("UpdateCurrentVersion", App.Services.Updates.InstalledVersion);
        NotesText.Text = string.IsNullOrWhiteSpace(manifest.Notes) ? App.Services.Localizer.Get("UpdateDefaultNotes") : manifest.Notes;
        CloseButton.Content = App.Services.Localizer.Get("Close");
        UpdateButton.Content = App.Services.Localizer.Get("UpdateButton");
        CancelDownloadButton.Content = App.Services.Localizer.Get("Cancel");
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _downloadCancellation?.Cancel();
        base.OnClosing(e);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_downloading) return;
        _downloading = true;
        _downloadCancellation = new CancellationTokenSource();
        ErrorText.Visibility = Visibility.Collapsed;
        InformationPanel.Visibility = Visibility.Collapsed;
        DownloadPanel.Visibility = Visibility.Visible;
        CloseButton.Visibility = UpdateButton.Visibility = Visibility.Collapsed;
        CancelDownloadButton.Visibility = Visibility.Visible;
        DownloadStatusText.Text = App.Services.Localizer.Get("DownloadingUpdate");
        PercentageText.Text = string.Empty;
        var progress = new Progress<UpdateTransferProgress>(value =>
        {
            if (value.Verifying)
            {
                DownloadProgress.IsIndeterminate = true;
                DownloadStatusText.Text = App.Services.Localizer.Get("VerifyingUpdate");
                PercentageText.Text = string.Empty;
            }
            else if (value.Percentage.HasValue)
            {
                DownloadProgress.IsIndeterminate = false;
                DownloadProgress.Value = Math.Min(100, value.Percentage.Value);
                PercentageText.Text = Math.Floor(value.Percentage.Value) + "%";
            }
            else
            {
                DownloadProgress.IsIndeterminate = true;
                PercentageText.Text = string.Empty;
            }
        });
        try
        {
            var installer = await App.Services.Updates.DownloadAndVerifyAsync(_manifest, progress, _downloadCancellation.Token);
            await UpdateService.LaunchInstallerAsync(installer, _manifest.Sha256, _downloadCancellation.Token);
            Application.Current.Shutdown();
        }
        catch (OperationCanceledException) { if (IsVisible) Close(); }
        catch (Exception exception)
        {
            _downloading = false;
            DownloadPanel.Visibility = Visibility.Collapsed;
            InformationPanel.Visibility = Visibility.Visible;
            CloseButton.Visibility = UpdateButton.Visibility = Visibility.Visible;
            CancelDownloadButton.Visibility = Visibility.Collapsed;
            ErrorText.Text = exception is UpdateVerificationException
                ? App.Services.Localizer.Get("UpdateVerificationFailed")
                : App.Services.Localizer.Get("UpdateDownloadFailed");
            ErrorText.Visibility = Visibility.Visible;
        }
        finally { _downloadCancellation?.Dispose(); _downloadCancellation = null; }
    }

    private void CancelDownloadButton_Click(object sender, RoutedEventArgs e) { _downloadCancellation?.Cancel(); Close(); }
}
