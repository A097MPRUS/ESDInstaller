using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using ESDInstaller.Core.Models;
using ESDInstaller.Services;

namespace ESDInstaller.Views;

public sealed partial class DestinationPage : Page
{
    private readonly WizardCoordinator _coordinator;
    private readonly Localizer _text = App.Services.Localizer;
    private readonly Dictionary<string, Border> _partitionBorders = new();
    private readonly SolidColorBrush _normalBorder = new(ColorHelper.FromArgb(255, 160, 160, 160));
    private readonly SolidColorBrush _selectedBorder = new(ColorHelper.FromArgb(255, 0, 120, 215));

    public DestinationPage(WizardCoordinator coordinator)
    {
        InitializeComponent();
        _coordinator = coordinator;
    }

    public void ShowLoading(bool loading)
    {
        DiskPanel.IsHitTestVisible = !loading;
        if (loading)
        {
            NextButton.IsEnabled = false;
            SelectionText.Text = string.Empty;
            DiskPanel.Children.Clear();
            _partitionBorders.Clear();
        }
        LoadingRing.IsActive = loading;
        LoadingRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ShowDisks(IReadOnlyList<DiskInfo> disks, CompatibilitySnapshot compatibility)
    {
        ErrorBar.IsOpen = false;
        DiskPanel.Children.Clear();
        _partitionBorders.Clear();
        FirmwareText.Text = _text.Format("FirmwareSummary", compatibility.FirmwareMode);
        ArchitectureText.Text = _text.Format("HostArchitectureSummary", compatibility.HostArchitecture);
        if (disks.Count == 0)
        {
            ShowError(_text.Get("ErrorNoDisks"), string.Empty);
            return;
        }

        foreach (var disk in disks) DiskPanel.Children.Add(CreateDiskCard(disk));
    }

    public void ShowError(string message, string detail)
    {
        ErrorBar.Title = message;
        ErrorBar.Message = detail;
        ErrorBar.IsOpen = true;
    }

    private UIElement CreateDiskCard(DiskInfo disk)
    {
        var outer = new Border
        {
            Background = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 209, 209, 209)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14)
        };
        var stack = new StackPanel { Spacing = 11 };
        var header = new Grid { ColumnSpacing = 10 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new FontIcon { FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = "\uEDA2", FontSize = 26, Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 0, 120, 215)) };
        var title = new StackPanel();
        title.Children.Add(new TextBlock { Text = _text.Format("DiskHeader", disk.Number, disk.SafeDisplayName, ImagePage.FormatBytes(disk.SizeBytes)), FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        title.Children.Add(new TextBlock { Text = _text.Format("DiskDetails", disk.BusType, disk.PartitionScheme, SafeSerial(disk.SerialNumber)), FontSize = 12, Foreground = new SolidColorBrush(Colors.DimGray) });
        var state = new TextBlock { Text = disk.IsOffline ? _text.Get("DiskOffline") : disk.IsReadOnly ? _text.Get("DiskReadOnly") : string.Empty, Foreground = new SolidColorBrush(Colors.DarkRed), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(title, 1); Grid.SetColumn(state, 2);
        header.Children.Add(icon); header.Children.Add(title); header.Children.Add(state);
        stack.Children.Add(header);

        var map = new Grid { Height = 92, ColumnSpacing = 3 };
        var total = Math.Max(1d, disk.SizeBytes);
        foreach (var partition in disk.Partitions.OrderBy(item => item.OffsetBytes))
        {
            var weight = Math.Max(0.035, partition.LengthBytes / total);
            map.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weight, GridUnitType.Star), MinWidth = 68 });
            var block = CreatePartitionBlock(disk, partition);
            Grid.SetColumn(block, map.Children.Count);
            map.Children.Add(block);
        }
        stack.Children.Add(map);
        outer.Child = stack;
        return outer;
    }

    private Border CreatePartitionBlock(DiskInfo disk, PartitionInfo partition)
    {
        var background = RoleColor(partition);
        var border = new Border
        {
            BorderBrush = _normalBorder,
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Colors.White),
            Tag = new PartitionSelection(disk, partition),
            IsHitTestVisible = !partition.IsProtected && !disk.IsOffline && !disk.IsReadOnly
        };
        var stack = new StackPanel { Spacing = 3 };
        stack.Children.Add(new Border { Height = 8, Background = new SolidColorBrush(background) });
        var label = partition.IsUnallocated ? _text.Get("PartitionUnallocated") :
            !string.IsNullOrWhiteSpace(partition.DriveDisplay) ? $"{partition.DriveDisplay} {partition.VolumeLabel}".Trim() : RoleName(partition.Role);
        stack.Children.Add(new TextBlock { Text = label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(6, 4, 6, 0), TextTrimming = TextTrimming.CharacterEllipsis });
        stack.Children.Add(new TextBlock { Text = ImagePage.FormatBytes(partition.LengthBytes), FontSize = 12, Margin = new Thickness(6, 0, 6, 0), TextTrimming = TextTrimming.CharacterEllipsis });
        var detail = partition.IsUnallocated ? _text.Get("PartitionCreateVolumeRequired") :
            partition.IsCurrentWindows ? _text.Get("PartitionCurrentWindows") :
            partition.IsProtected ? _text.Get("PartitionProtected") :
            string.IsNullOrWhiteSpace(partition.FileSystem) ? RoleName(partition.Role) : partition.FileSystem;
        stack.Children.Add(new TextBlock { Text = detail, FontSize = 11, Foreground = new SolidColorBrush(partition.IsProtected ? Colors.DarkRed : Colors.DimGray), Margin = new Thickness(6, 0, 6, 3), TextTrimming = TextTrimming.CharacterEllipsis });
        border.Child = stack;
        if (!partition.IsProtected && !disk.IsOffline && !disk.IsReadOnly)
        {
            border.Tapped += Partition_Tapped;
            border.PointerEntered += (_, _) => { if (border.BorderBrush != _selectedBorder) border.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 229, 243, 255)); };
            border.PointerExited += (_, _) => { if (border.BorderBrush != _selectedBorder) border.Background = new SolidColorBrush(Colors.White); };
        }
        _partitionBorders[partition.StableKey] = border;
        ToolTipService.SetToolTip(border, _text.Format("PartitionTooltip", partition.PartitionNumber, partition.DriveDisplay, partition.VolumeLabel, ImagePage.FormatBytes(partition.LengthBytes), RoleName(partition.Role)));
        return border;
    }

    private void Partition_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not Border { Tag: PartitionSelection selection } selectedBorder) return;
        foreach (var border in _partitionBorders.Values)
        {
            border.BorderBrush = _normalBorder;
            border.BorderThickness = new Thickness(1);
            border.Background = new SolidColorBrush(Colors.White);
        }
        selectedBorder.BorderBrush = _selectedBorder;
        selectedBorder.BorderThickness = new Thickness(3);
        selectedBorder.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 229, 243, 255));
        _coordinator.SelectDestination(selection.Disk, selection.Partition);
        SelectionText.Text = _text.Format("DestinationSelectionSummary", selection.Disk.Number, selection.Disk.SafeDisplayName,
            selection.Partition.PartitionNumber, selection.Partition.DriveDisplay, ImagePage.FormatBytes(selection.Partition.LengthBytes));
        NextButton.IsEnabled = true;
    }

    private string RoleName(PartitionRole role) => _text.Get(role switch
    {
        PartitionRole.EfiSystem => "PartitionRoleEfi",
        PartitionRole.MicrosoftReserved => "PartitionRoleMsr",
        PartitionRole.Recovery => "PartitionRoleRecovery",
        PartitionRole.Oem => "PartitionRoleOem",
        PartitionRole.Unallocated => "PartitionUnallocated",
        PartitionRole.BasicData => "PartitionRoleBasic",
        _ => "PartitionRoleUnknown"
    });

    private static Color RoleColor(PartitionInfo partition) => partition.Role switch
    {
        PartitionRole.EfiSystem => ColorHelper.FromArgb(255, 255, 185, 0),
        PartitionRole.MicrosoftReserved => ColorHelper.FromArgb(255, 127, 127, 127),
        PartitionRole.Recovery => ColorHelper.FromArgb(255, 0, 153, 188),
        PartitionRole.Oem => ColorHelper.FromArgb(255, 135, 100, 184),
        PartitionRole.Unallocated => ColorHelper.FromArgb(255, 32, 32, 32),
        _ => partition.IsCurrentWindows ? ColorHelper.FromArgb(255, 196, 43, 28) : ColorHelper.FromArgb(255, 0, 120, 215)
    };

    private string SafeSerial(string serial) => string.IsNullOrWhiteSpace(serial) ? _text.Get("DiskSerialUnavailable") : serial;
    private void BackButton_Click(object sender, RoutedEventArgs e) => _coordinator.BackFrom(2);
    private void NextButton_Click(object sender, RoutedEventArgs e) => _coordinator.ShowBootPage();
    private sealed record PartitionSelection(DiskInfo Disk, PartitionInfo Partition);
}
