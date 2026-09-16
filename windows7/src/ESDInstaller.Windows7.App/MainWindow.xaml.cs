using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ESDInstaller.Windows7.Services;

namespace ESDInstaller.Windows7;

public partial class MainWindow : Window
{
    private readonly WizardCoordinator _coordinator;
    private bool _installLocked;
    private bool _startupUpdateChecked;
    public MainWindow()
    {
        InitializeComponent();
        FlowDirection = App.Services.Settings.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        _coordinator = new WizardCoordinator(this, App.Services);
        SourceInitialized += (sender, args) =>
        {
            if (AeroGlassService.TryExtend(this, 48))
            {
                Background = Brushes.Transparent;
                RootGrid.Background = Brushes.Transparent;
                ToolbarBorder.Background = Brushes.Transparent;
            }
        };
        Closing += (sender, args) => { if (_installLocked) args.Cancel = true; };
        Closed += (sender, args) => App.Services.Images.Dispose();
        Loaded += MainWindow_Loaded;
        UpdateModeText();
        _coordinator.ShowImagePage();
    }
    public Frame PageFrame => ContentFrame;
    public void SetStatus(string text) => StatusText.Text = text;
    public void SetStep(int index)
    {
        var steps = new[] { Step0, Step1, Step2, Step3, Step4, Step5 };
        for (var i = 0; i < steps.Length; i++)
        {
            steps[i].Background = i == index ? new SolidColorBrush(Color.FromRgb(220, 237, 249)) : Brushes.Transparent;
            steps[i].BorderBrush = i == index ? new SolidColorBrush(Color.FromRgb(0, 120, 215)) : Brushes.Transparent;
        }
    }
    public void SetInstallLock(bool locked)
    {
        _installLocked = locked; OpenImageButton.IsEnabled = RefreshDisksButton.IsEnabled = SettingsButton.IsEnabled = !locked;
    }
    private async void OpenImage_Click(object sender, RoutedEventArgs e) => await _coordinator.OpenImageAsync();
    private async void RefreshDisks_Click(object sender, RoutedEventArgs e) => await _coordinator.RefreshDisksAsync();
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;
        _coordinator.Session.AdvancedMode = App.Services.Settings.Current.AdvancedMode;
        if (!_coordinator.Session.AdvancedMode) _coordinator.Session.BypassWindows11Requirements = false;
        UpdateModeText();
        if (dialog.LanguageChanged)
        {
            App.Services.Localizer.Refresh();
            var replacement = new MainWindow(); replacement.Show(); Close();
        }
    }
    private void Help_Click(object sender, RoutedEventArgs e) => MessageBox.Show(this,
        App.Services.Localizer.Get("HelpText"), App.Services.Localizer.Get("HelpTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
    private void UpdateModeText() => ModeText.Text = App.Services.Localizer.Get(
        App.Services.Settings.Current.AdvancedMode ? "AdvancedModeEnabled" : "StandardMode");
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_startupUpdateChecked) return;
        _startupUpdateChecked = true;
        try
        {
            var result = await App.Services.Updates.CheckAsync(false);
            if (result.Status == UpdateCheckStatus.Available && result.Manifest != null && IsVisible && !_installLocked)
                new UpdateWindow(result.Manifest) { Owner = this }.ShowDialog();
        }
        catch { }
    }
}
