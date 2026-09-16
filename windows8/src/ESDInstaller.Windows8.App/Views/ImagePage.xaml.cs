using Microsoft.Win32;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ESDInstaller.Windows8.Core.Models;
using ESDInstaller.Windows8.Services;

namespace ESDInstaller.Windows8.Views;

public partial class ImagePage : Page
{
    private readonly WizardCoordinator _coordinator;
    public ImagePage(WizardCoordinator coordinator) { InitializeComponent(); _coordinator = coordinator; }
    public async Task PickFileAsync()
    {
        var dialog = new OpenFileDialog { Filter = "Windows images (*.iso;*.wim;*.esd)|*.iso;*.wim;*.esd|All files (*.*)|*.*", CheckFileExists = true };
        if (!_coordinator.IsInspectingImage && dialog.ShowDialog() == true) await InspectAsync(dialog.FileName);
    }
    private async Task InspectAsync(string path)
    {
        if (_coordinator.IsInspectingImage) return;
        Next.IsEnabled = false; Banner.Visibility = Visibility.Collapsed; Details.Visibility = Visibility.Collapsed;
        ExtractionProgress.Value = 0; ExtractionProgress.IsIndeterminate = true;
        ExtractionProgress.Visibility = Path.GetExtension(path).ToLowerInvariant() == ".iso" ? Visibility.Visible : Visibility.Collapsed;
        await _coordinator.InspectImageAsync(path, this);
    }
    public void ShowExtractionProgress(int percent) => Dispatcher.BeginInvoke(new System.Action(() =>
    { ExtractionProgress.Visibility = Visibility.Visible; ExtractionProgress.IsIndeterminate = false; ExtractionProgress.Value = percent; }));
    public void ShowImage(WindowsImage image)
    {
        ExtractionProgress.Visibility = Visibility.Collapsed; Details.Visibility = Visibility.Visible;
        Filename.Text = Path.GetFileName(image.SourcePath); Version.Text = image.DisplayVersion;
        Architecture.Text = image.Architecture.ToString(); Type.Text = image.Kind.ToString().ToUpperInvariant();
        Size.Text = FormatBytes(image.FileSizeBytes); EditionCount.Text = image.Editions.Count.ToString();
        if (image.RequiresLegacyEngine)
        {
            Banner.SetWarning(App.Services.Localizer.Get("LegacyEngineTitle"), App.Services.Localizer.Get(image.LegacyReason ?? "LegacyEngineUnavailable"));
            Banner.Visibility = Visibility.Visible; Next.IsEnabled = false;
        }
        else Next.IsEnabled = image.Editions.Count > 0;
    }
    public void ShowError(string title, string detail)
    {
        ExtractionProgress.Visibility = Visibility.Collapsed; Banner.SetError(title, detail);
        Banner.Visibility = Visibility.Visible;
    }
    private async void Select_Click(object sender, RoutedEventArgs e) => await PickFileAsync();
    private void Next_Click(object sender, RoutedEventArgs e) => _coordinator.ShowEditionPage();
    private async void DropArea_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length > 0) await InspectAsync(files[0]);
    }
    private void DropArea_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" }; double value = bytes; var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        return value.ToString(index == 0 ? "0" : "0.0") + " " + units[index];
    }
}
