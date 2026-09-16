using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using ESDInstaller.Services;

namespace ESDInstaller;

public sealed partial class UpdateWindow : Window
{
    private readonly UpdateManifest _manifest;
    private readonly Localizer _text = App.Services.Localizer;
    private CancellationTokenSource? _downloadCancellation;
    private bool _downloading;

    public UpdateWindow(UpdateManifest manifest)
    {
        _manifest = manifest;
        InitializeComponent();
        RootGrid.FlowDirection = App.Services.Settings.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Title = _text.Get("UpdateTitle");
        AppWindow.Resize(new SizeInt32(470, 320));
        HeadingText.Text = _text.Get("UpdateAvailableHeading");
        AvailableVersionText.Text = _text.Format("UpdateVersionAvailable", manifest.Version);
        CurrentVersionText.Text = _text.Format("UpdateCurrentVersion", App.Services.Updates.InstalledVersion);
        NotesText.Text = string.IsNullOrWhiteSpace(manifest.Notes) ? _text.Get("UpdateDefaultNotes") : manifest.Notes;
        CloseButton.Content = _text.Get("Close");
        UpdateButton.Content = _text.Get("UpdateButton");
        CancelDownloadButton.Content = _text.Get("Cancel");
        AppWindow.Closing += (_, _) => _downloadCancellation?.Cancel();
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
        CloseButton.Visibility = Visibility.Collapsed;
        UpdateButton.Visibility = Visibility.Collapsed;
        CancelDownloadButton.Visibility = Visibility.Visible;
        DownloadStatusText.Text = _text.Get("DownloadingUpdate");
        PercentageText.Text = string.Empty;

        var progress = new Progress<UpdateTransferProgress>(value =>
        {
            if (value.Verifying)
            {
                DownloadProgress.IsIndeterminate = true;
                DownloadStatusText.Text = _text.Get("VerifyingUpdate");
                PercentageText.Text = string.Empty;
            }
            else if (value.Percentage.HasValue)
            {
                DownloadProgress.IsIndeterminate = false;
                DownloadProgress.Value = Math.Min(100, value.Percentage.Value);
                PercentageText.Text = $"{Math.Floor(value.Percentage.Value)}%";
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
            App.MainWindowInstance?.Close();
            Close();
        }
        catch (OperationCanceledException)
        {
            Close();
        }
        catch (Exception exception)
        {
            _downloading = false;
            DownloadPanel.Visibility = Visibility.Collapsed;
            InformationPanel.Visibility = Visibility.Visible;
            CloseButton.Visibility = Visibility.Visible;
            UpdateButton.Visibility = Visibility.Visible;
            CancelDownloadButton.Visibility = Visibility.Collapsed;
            ErrorText.Text = exception is UpdateVerificationException
                ? _text.Get("UpdateVerificationFailed")
                : _text.Get("UpdateDownloadFailed");
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            _downloadCancellation?.Dispose();
            _downloadCancellation = null;
        }
    }

    private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        _downloadCancellation?.Cancel();
        Close();
    }
}
