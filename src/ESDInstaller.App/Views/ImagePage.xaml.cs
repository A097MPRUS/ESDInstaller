using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using ESDInstaller.Core.Models;
using ESDInstaller.Services;

namespace ESDInstaller.Views;

public sealed partial class ImagePage : Page
{
    private readonly WizardCoordinator _coordinator;
    private readonly Localizer _text = App.Services.Localizer;
    private bool _pickerOpen;

    public ImagePage(WizardCoordinator coordinator)
    {
        InitializeComponent();
        _coordinator = coordinator;
        if (coordinator.Session.Image is { } image) ShowImage(image);
    }

    public async Task PickFileAsync()
    {
        if (_pickerOpen || _coordinator.IsInspectingImage) return;
        _pickerOpen = true;
        try
        {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Downloads };
        picker.FileTypeFilter.Add(".iso");
        picker.FileTypeFilter.Add(".wim");
        picker.FileTypeFilter.Add(".esd");
        var window = App.MainWindowInstance;
        if (window is null) return;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        var file = await picker.PickSingleFileAsync();
        if (file is not null) await InspectAsync(file.Path);
        }
        catch (Exception exception) { ShowError(_text.Get("ErrorUnexpected"), exception.Message); }
        finally { _pickerOpen = false; }
    }

    public void ShowImage(WindowsImage image)
    {
        InspectProgress.IsActive = false;
        InspectProgress.Visibility = Visibility.Collapsed;
        SelectImageButton.IsEnabled = true;
        ImageDetails.Visibility = Visibility.Visible;
        ErrorBar.IsOpen = false;
        FilenameText.Text = Path.GetFileName(image.SourcePath);
        VersionText.Text = image.DisplayVersion;
        ArchitectureText.Text = image.Architecture.ToString();
        TypeText.Text = image.Kind.ToString().ToUpperInvariant();
        FileSizeText.Text = FormatBytes(image.FileSizeBytes);
        EditionCountText.Text = image.Editions.Count.ToString();
        LegacyBar.IsOpen = image.RequiresLegacyEngine;
        LegacyBar.Title = _text.Get("LegacyEngineTitle");
        LegacyBar.Message = _text.Get(image.LegacyReason ?? "LegacyEngineUnavailable");
        NextButton.IsEnabled = !image.RequiresLegacyEngine && image.Editions.Count > 0;
    }

    public void ShowError(string message, string detail)
    {
        InspectProgress.IsActive = false;
        InspectProgress.Visibility = Visibility.Collapsed;
        SelectImageButton.IsEnabled = true;
        ErrorBar.Title = message;
        ErrorBar.Message = detail;
        ErrorBar.IsOpen = true;
        NextButton.IsEnabled = false;
    }

    private async Task InspectAsync(string path)
    {
        if (_coordinator.IsInspectingImage) return;
        ErrorBar.IsOpen = false;
        LegacyBar.IsOpen = false;
        InspectProgress.Visibility = Visibility.Visible;
        InspectProgress.IsActive = true;
        SelectImageButton.IsEnabled = false;
        NextButton.IsEnabled = false;
        await _coordinator.InspectImageAsync(path, this);
    }

    private async void SelectImageButton_Click(object sender, RoutedEventArgs e) => await PickFileAsync();
    private void NextButton_Click(object sender, RoutedEventArgs e) => _coordinator.ShowEditionPage();

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        if (_coordinator.IsInspectingImage) { e.AcceptedOperation = DataPackageOperation.None; return; }
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = _text.Get("ImageDropCaption");
        }
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (_coordinator.IsInspectingImage) return;
        try
        {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var items = await e.DataView.GetStorageItemsAsync();
        var file = items.OfType<StorageFile>().FirstOrDefault();
        if (file is null) return;
        var extension = Path.GetExtension(file.Path);
        if (extension.Equals(".iso", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".wim", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".esd", StringComparison.OrdinalIgnoreCase))
            await InspectAsync(file.Path);
        else ShowError(_text.Get("ErrorUnsupportedImageType"), extension);
        }
        catch (Exception exception) { ShowError(_text.Get("ErrorUnexpected"), exception.Message); }
    }

    internal static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }
}
