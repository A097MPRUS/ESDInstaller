using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ESDInstaller.Windows7.Core.Models;
using ESDInstaller.Windows7.Services;

namespace ESDInstaller.Windows7.Views;

public partial class DestinationPage : Page
{
    private readonly WizardCoordinator _coordinator;
    public DestinationPage(WizardCoordinator coordinator) { InitializeComponent(); _coordinator = coordinator; }
    public void ShowLoading(bool value)
    {
        Loading.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        // Old disk cards must not be selectable while they are being replaced.
        DiskList.IsEnabled = !value;
        if (value) Next.IsEnabled = false;
    }
    public void ShowError(string title, string detail) { Banner.SetError(title, detail); Banner.Visibility = Visibility.Visible; }
    public void ShowDisks(IReadOnlyList<DiskInfo> disks, CompatibilitySnapshot host)
    {
        DiskList.Children.Clear(); Banner.Visibility = Visibility.Collapsed; Next.IsEnabled = false;
        Firmware.Text = App.Services.Localizer.Format("FirmwareSummary", host.FirmwareMode);
        HostArchitecture.Text = App.Services.Localizer.Format("HostArchitectureSummary", host.HostArchitecture);
        if (disks.Count == 0) { ShowError(App.Services.Localizer.Get("ErrorNoDisks"), ""); return; }
        foreach (var disk in disks) DiskList.Children.Add(BuildDiskCard(disk));
    }
    private UIElement BuildDiskCard(DiskInfo disk)
    {
        var card = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(184,184,184)), BorderThickness = new Thickness(1), Background = Brushes.White, Margin = new Thickness(0,0,0,12), Padding = new Thickness(12) };
        var panel = new StackPanel(); card.Child = panel;
        var header = new DockPanel();
        header.Children.Add(new Image { Source = ShellIconService.Get(StockIconId.DriveFixed, true), Width = 34, Height = 34, Margin = new Thickness(0,0,10,0) });
        var labels = new StackPanel(); labels.Children.Add(new TextBlock { Text = App.Services.Localizer.Format("DiskHeader", disk.Number, disk.SafeDisplayName, FormatBytes(disk.SizeBytes)), FontWeight = FontWeights.SemiBold, FontSize = 14 });
        labels.Children.Add(new TextBlock { Text = App.Services.Localizer.Format("DiskDetails", disk.PartitionScheme, disk.BusType, string.IsNullOrWhiteSpace(disk.SerialNumber) ? App.Services.Localizer.Get("DiskSerialUnavailable") : disk.SerialNumber), Foreground = Brushes.DimGray, Margin = new Thickness(0,3,0,0) });
        header.Children.Add(labels); panel.Children.Add(header);
        var scroller = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0,12,0,0) };
        var partitions = new StackPanel { Orientation = Orientation.Horizontal }; scroller.Content = partitions;
        foreach (var partition in disk.Partitions.OrderBy(x => x.OffsetBytes)) partitions.Children.Add(BuildPartition(disk, partition));
        panel.Children.Add(scroller); return card;
    }
    private UIElement BuildPartition(DiskInfo disk, PartitionInfo partition)
    {
        var width = Math.Max(105, Math.Min(220, partition.LengthBytes * 620.0 / Math.Max(1, disk.SizeBytes)));
        var selectable = !partition.IsProtected && !partition.IsBitLocker && partition.Role == PartitionRole.BasicData && partition.PartitionNumber > 0;
        var button = new RadioButton
        {
            GroupName = "DestinationPartitions", Width = width, MinHeight = 76, Margin = new Thickness(0,0,5,0),
            IsEnabled = selectable, Tag = new Tuple<DiskInfo, PartitionInfo>(disk, partition), ToolTip = PartitionTooltip(partition)
        };
        var border = new Border { BorderBrush = RoleBrush(partition.Role), BorderThickness = new Thickness(1,5,1,1), Background = selectable ? Brushes.White : new SolidColorBrush(Color.FromRgb(238,238,238)), Padding = new Thickness(7) };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = PartitionTitle(partition), FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        stack.Children.Add(new TextBlock { Text = FormatBytes(partition.LengthBytes) + (string.IsNullOrWhiteSpace(partition.FileSystem) ? "" : "  " + partition.FileSystem), Foreground = Brushes.DimGray, Margin = new Thickness(0,4,0,0) });
        stack.Children.Add(new TextBlock { Text = RoleText(partition), Foreground = Brushes.DimGray, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis });
        border.Child = stack; button.Content = border; button.Checked += Partition_Checked;
        return button;
    }
    private void Partition_Checked(object sender, RoutedEventArgs e)
    {
        var pair = (Tuple<DiskInfo, PartitionInfo>)((RadioButton)sender).Tag;
        _coordinator.SelectDestination(pair.Item1, pair.Item2); Next.IsEnabled = true;
        Banner.SetInformation(App.Services.Localizer.Get("StatusSelectDestination"),
            App.Services.Localizer.Format("DestinationSelectionSummary", pair.Item1.Number, pair.Item1.SafeDisplayName,
                pair.Item2.PartitionNumber, pair.Item2.DriveDisplay, FormatBytes(pair.Item2.LengthBytes)));
        Banner.Visibility = Visibility.Visible;
    }
    private static Brush RoleBrush(PartitionRole role)
    {
        switch (role) { case PartitionRole.EfiSystem: return Brushes.DarkGreen; case PartitionRole.Recovery: return Brushes.DarkOrange; case PartitionRole.Unallocated: return Brushes.Black; case PartitionRole.BasicData: return new SolidColorBrush(Color.FromRgb(45,113,172)); default: return Brushes.Gray; }
    }
    private static string PartitionTitle(PartitionInfo p) => p.IsUnallocated ? App.Services.Localizer.Get("PartitionUnallocated") :
        (p.DriveLetter.HasValue ? p.DriveLetter + ": " : "") + (string.IsNullOrWhiteSpace(p.VolumeLabel) ? "Partition " + p.PartitionNumber : p.VolumeLabel);
    private static string RoleText(PartitionInfo p)
    {
        if (p.IsCurrentWindows) return App.Services.Localizer.Get("PartitionCurrentWindows");
        if (p.IsProtected) return App.Services.Localizer.Get("PartitionProtected");
        switch (p.Role) { case PartitionRole.EfiSystem: return App.Services.Localizer.Get("PartitionRoleEfi"); case PartitionRole.MicrosoftReserved: return App.Services.Localizer.Get("PartitionRoleMsr"); case PartitionRole.Recovery: return App.Services.Localizer.Get("PartitionRoleRecovery"); case PartitionRole.Oem: return App.Services.Localizer.Get("PartitionRoleOem"); case PartitionRole.BasicData: return App.Services.Localizer.Get("PartitionRoleBasic"); default: return App.Services.Localizer.Get("PartitionRoleUnknown"); }
    }
    private static string PartitionTooltip(PartitionInfo p) => App.Services.Localizer.Format("PartitionTooltip", p.PartitionNumber, p.DriveDisplay, p.VolumeLabel, FormatBytes(p.LengthBytes), RoleText(p));
    private static string FormatBytes(long bytes) { string[] units = { "B", "KB", "MB", "GB", "TB" }; double value = bytes; var i = 0; while (value >= 1024 && i < 4) { value /= 1024; i++; } return value.ToString(i == 0 ? "0" : "0.0") + " " + units[i]; }
    private void Back_Click(object sender, RoutedEventArgs e) => _coordinator.BackFrom(2);
    private void Next_Click(object sender, RoutedEventArgs e) => _coordinator.ShowBootPage();
}
