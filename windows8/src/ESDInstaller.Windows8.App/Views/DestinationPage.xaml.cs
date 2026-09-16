using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ESDInstaller.Windows8.Controls;
using ESDInstaller.Windows8.Core.Models;
using ESDInstaller.Windows8.Services;

namespace ESDInstaller.Windows8.Views;

public partial class DestinationPage : Page
{
    private readonly WizardCoordinator _coordinator;
    private readonly Dictionary<string, Border> _partitionBorders = new();
    private readonly SolidColorBrush _normalBorder = new(Color.FromRgb(160, 160, 160));
    private readonly SolidColorBrush _selectedBorder = new(Color.FromRgb(0, 120, 215));

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
        DiskList.Children.Clear();
        _partitionBorders.Clear();
        Banner.Visibility = Visibility.Collapsed;
        SelectionText.Text = string.Empty;
        Next.IsEnabled = false;
        Firmware.Text = App.Services.Localizer.Format("FirmwareSummary", host.FirmwareMode);
        HostArchitecture.Text = App.Services.Localizer.Format("HostArchitectureSummary", host.HostArchitecture);
        if (disks.Count == 0) { ShowError(App.Services.Localizer.Get("ErrorNoDisks"), ""); return; }
        foreach (var disk in disks) DiskList.Children.Add(BuildDiskCard(disk));
    }

    private UIElement BuildDiskCard(DiskInfo disk)
    {
        var card = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(209, 209, 209)), BorderThickness = new Thickness(1),
            Background = Brushes.White, Margin = new Thickness(0, 0, 0, 14), Padding = new Thickness(14)
        };
        var panel = new StackPanel(); card.Child = panel;
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new SymbolIcon { Kind = SymbolKind.Disk, Width = 26, Height = 26, Foreground = _selectedBorder, Margin = new Thickness(0, 0, 10, 0) });
        var labels = new StackPanel();
        labels.Children.Add(new TextBlock { Text = App.Services.Localizer.Format("DiskHeader", disk.Number, disk.SafeDisplayName, FormatBytes(disk.SizeBytes)), FontWeight = FontWeights.SemiBold, FontSize = 15 });
        labels.Children.Add(new TextBlock { Text = App.Services.Localizer.Format("DiskDetails", disk.BusType, disk.PartitionScheme, SafeSerial(disk.SerialNumber)), Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, 3, 0, 0) });
        Grid.SetColumn(labels, 1); header.Children.Add(labels);
        var state = new TextBlock { Text = disk.IsOffline ? App.Services.Localizer.Get("DiskOffline") : disk.IsReadOnly ? App.Services.Localizer.Get("DiskReadOnly") : string.Empty, Foreground = Brushes.DarkRed, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(state, 2); header.Children.Add(state);
        panel.Children.Add(header);

        var map = new Grid { Height = 92, Margin = new Thickness(0, 11, 0, 0) };
        var total = Math.Max(1d, disk.SizeBytes);
        var column = 0;
        foreach (var partition in disk.Partitions.OrderBy(item => item.OffsetBytes))
        {
            var weight = Math.Max(0.035, partition.LengthBytes / total);
            map.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weight, GridUnitType.Star), MinWidth = 68 });
            var block = BuildPartition(disk, partition); block.Margin = new Thickness(column == 0 ? 0 : 3, 0, 0, 0);
            Grid.SetColumn(block, column++); map.Children.Add(block);
        }
        panel.Children.Add(map);
        return card;
    }

    private Border BuildPartition(DiskInfo disk, PartitionInfo partition)
    {
        var selectable = !partition.IsProtected && !partition.IsBitLocker && partition.Role == PartitionRole.BasicData &&
                         partition.PartitionNumber > 0 && !disk.IsOffline && !disk.IsReadOnly;
        var border = new Border
        {
            BorderBrush = _normalBorder, BorderThickness = new Thickness(1), Background = Brushes.White,
            Tag = Tuple.Create(disk, partition), Cursor = selectable ? Cursors.Hand : Cursors.Arrow,
            Opacity = selectable ? 1 : 0.82, ToolTip = PartitionTooltip(partition)
        };
        var stack = new StackPanel();
        stack.Children.Add(new Border { Height = 8, Background = RoleBrush(partition) });
        var label = partition.IsUnallocated ? App.Services.Localizer.Get("PartitionUnallocated") :
            !string.IsNullOrWhiteSpace(partition.DriveDisplay) ? (partition.DriveDisplay + " " + partition.VolumeLabel).Trim() : RoleText(partition);
        stack.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, Margin = new Thickness(6, 4, 6, 0), TextTrimming = TextTrimming.CharacterEllipsis });
        stack.Children.Add(new TextBlock { Text = FormatBytes(partition.LengthBytes), FontSize = 12, Margin = new Thickness(6, 0, 6, 0), TextTrimming = TextTrimming.CharacterEllipsis });
        var detail = partition.IsUnallocated ? App.Services.Localizer.Get("PartitionCreateVolumeRequired") :
            partition.IsCurrentWindows ? App.Services.Localizer.Get("PartitionCurrentWindows") :
            partition.IsProtected ? App.Services.Localizer.Get("PartitionProtected") :
            partition.IsBitLocker ? App.Services.Localizer.Get("ValidationBitLockerTarget") :
            string.IsNullOrWhiteSpace(partition.FileSystem) ? RoleText(partition) : partition.FileSystem;
        stack.Children.Add(new TextBlock { Text = detail, FontSize = 11, Foreground = partition.IsProtected || partition.IsBitLocker ? Brushes.DarkRed : Brushes.DimGray, Margin = new Thickness(6, 0, 6, 3), TextTrimming = TextTrimming.CharacterEllipsis });
        border.Child = stack;
        if (selectable)
        {
            border.MouseLeftButtonUp += Partition_Click;
            border.MouseEnter += (_, _) => { if (!ReferenceEquals(border.BorderBrush, _selectedBorder)) border.Background = new SolidColorBrush(Color.FromRgb(229, 243, 255)); };
            border.MouseLeave += (_, _) => { if (!ReferenceEquals(border.BorderBrush, _selectedBorder)) border.Background = Brushes.White; };
        }
        _partitionBorders[partition.StableKey] = border;
        return border;
    }

    private void Partition_Click(object sender, MouseButtonEventArgs e)
    {
        var selected = (Border)sender;
        var pair = (Tuple<DiskInfo, PartitionInfo>)selected.Tag;
        foreach (var border in _partitionBorders.Values)
        {
            border.BorderBrush = _normalBorder; border.BorderThickness = new Thickness(1); border.Background = Brushes.White;
        }
        selected.BorderBrush = _selectedBorder; selected.BorderThickness = new Thickness(3); selected.Background = new SolidColorBrush(Color.FromRgb(229, 243, 255));
        _coordinator.SelectDestination(pair.Item1, pair.Item2);
        SelectionText.Text = App.Services.Localizer.Format("DestinationSelectionSummary", pair.Item1.Number, pair.Item1.SafeDisplayName, pair.Item2.PartitionNumber, pair.Item2.DriveDisplay, FormatBytes(pair.Item2.LengthBytes));
        Next.IsEnabled = true;
    }

    private static Brush RoleBrush(PartitionInfo partition)
    {
        if (partition.IsCurrentWindows) return new SolidColorBrush(Color.FromRgb(196, 43, 28));
        return partition.Role switch
        {
            PartitionRole.EfiSystem => new SolidColorBrush(Color.FromRgb(255, 185, 0)),
            PartitionRole.MicrosoftReserved => new SolidColorBrush(Color.FromRgb(127, 127, 127)),
            PartitionRole.Recovery => new SolidColorBrush(Color.FromRgb(0, 153, 188)),
            PartitionRole.Oem => new SolidColorBrush(Color.FromRgb(135, 100, 184)),
            PartitionRole.Unallocated => new SolidColorBrush(Color.FromRgb(32, 32, 32)),
            _ => new SolidColorBrush(Color.FromRgb(0, 120, 215))
        };
    }

    private static string RoleText(PartitionInfo p)
    {
        if (p.IsCurrentWindows) return App.Services.Localizer.Get("PartitionCurrentWindows");
        if (p.IsProtected) return App.Services.Localizer.Get("PartitionProtected");
        if (p.IsBitLocker) return App.Services.Localizer.Get("ValidationBitLockerTarget");
        return p.Role switch
        {
            PartitionRole.EfiSystem => App.Services.Localizer.Get("PartitionRoleEfi"),
            PartitionRole.MicrosoftReserved => App.Services.Localizer.Get("PartitionRoleMsr"),
            PartitionRole.Recovery => App.Services.Localizer.Get("PartitionRoleRecovery"),
            PartitionRole.Oem => App.Services.Localizer.Get("PartitionRoleOem"),
            PartitionRole.BasicData => App.Services.Localizer.Get("PartitionRoleBasic"),
            _ => App.Services.Localizer.Get("PartitionRoleUnknown")
        };
    }

    private static string SafeSerial(string serial) => string.IsNullOrWhiteSpace(serial) ? App.Services.Localizer.Get("DiskSerialUnavailable") : serial;
    private static string PartitionTooltip(PartitionInfo p) => App.Services.Localizer.Format("PartitionTooltip", p.PartitionNumber, p.DriveDisplay, p.VolumeLabel, FormatBytes(p.LengthBytes), RoleText(p));
    private static string FormatBytes(long bytes) { string[] units = { "B", "KB", "MB", "GB", "TB" }; double value = bytes; var i = 0; while (value >= 1024 && i < 4) { value /= 1024; i++; } return value.ToString(i == 0 ? "0" : "0.0") + " " + units[i]; }
    private void Back_Click(object sender, RoutedEventArgs e) => _coordinator.BackFrom(2);
    private void Next_Click(object sender, RoutedEventArgs e) => _coordinator.ShowBootPage();
}
