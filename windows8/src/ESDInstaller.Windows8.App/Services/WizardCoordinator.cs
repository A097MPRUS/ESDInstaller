using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ESDInstaller.Windows8.Core.Models;
using ESDInstaller.Windows8.Core.Services;
using ESDInstaller.Windows8.Views;

namespace ESDInstaller.Windows8.Services;

public sealed class WizardCoordinator
{
    private readonly MainWindow _window;
    private readonly AppServices _services;
    private readonly Localizer _text;
    private ImagePage? _imagePage;
    private DestinationPage? _destinationPage;
    // Ignore repeated clicks while the matching operation is still running.
    private bool _inspectingImage, _refreshingDisks, _installing;
    public WizardCoordinator(MainWindow window, AppServices services)
    {
        _window = window; _services = services; _text = services.Localizer;
        Session.AdvancedMode = services.Settings.Current.AdvancedMode;
    }
    public SessionState Session { get; } = new SessionState();
    public int CurrentStep { get; private set; }

    public void ShowImagePage()
    {
        CurrentStep = 0; _window.SetStep(0); _imagePage = new ImagePage(this);
        _window.PageFrame.Content = _imagePage; _window.SetStatus(_text.Get("StatusReady"));
    }
    public async Task OpenImageAsync()
    {
        if (_inspectingImage) return;
        if (CurrentStep != 0 || _imagePage == null) ShowImagePage();
        await _imagePage!.PickFileAsync();
    }
    public bool IsInspectingImage => _inspectingImage;
    public async Task InspectImageAsync(string path, ImagePage page)
    {
        if (_inspectingImage) return;
        _inspectingImage = true;
        // A plan approved for the previous image must never survive a replacement.
        Session.Plan = null;
        _window.SetStatus(_text.Get("StatusInspectingImage"));
        try
        {
            Session.Image = await _services.Images.InspectAsync(path, page.ShowExtractionProgress);
            Session.Edition = null; Session.DestinationDisk = null; Session.DestinationPartition = null;
            Session.BootPartition = null; Session.Plan = null; Session.BypassWindows11Requirements = false;
            page.ShowImage(Session.Image); _window.SetStatus(_text.Get("StatusImageReady"));
        }
        catch (ESDInstallerException exception)
        { page.ShowError(_text.Get(exception.MessageKey), exception.Detail); _window.SetStatus(_text.Get("StatusError")); }
        catch (Exception exception)
        { page.ShowError(_text.Get("ErrorUnexpected"), exception.Message); _window.SetStatus(_text.Get("StatusError")); }
        finally { _inspectingImage = false; }
    }
    public void ShowEditionPage()
    {
        if (Session.Image == null || Session.Image.Editions.Count == 0) return;
        CurrentStep = 1; _window.SetStep(1); _window.PageFrame.Content = new EditionPage(this, Session.Image.Editions);
        _window.SetStatus(_text.Get("StatusChooseEdition"));
    }
    public void SelectEdition(WindowsImageEdition edition) => Session.Edition = edition;
    public async Task ShowDestinationPageAsync()
    {
        if (Session.Edition == null) return;
        CurrentStep = 2; _window.SetStep(2); _destinationPage = new DestinationPage(this);
        _window.PageFrame.Content = _destinationPage; await RefreshDisksAsync();
    }
    public async Task RefreshDisksAsync()
    {
        if (CurrentStep != 2 || _destinationPage == null)
        {
            if (Session.Edition == null) { _window.SetStatus(_text.Get("StatusSelectImageFirst")); return; }
            await ShowDestinationPageAsync(); return;
        }
        if (_refreshingDisks) return;
        _refreshingDisks = true;
        var page = _destinationPage;
        _window.SetStatus(_text.Get("StatusReadingDisks")); page.ShowLoading(true);
        Session.DestinationDisk = null; Session.DestinationPartition = null; Session.BootPartition = null; Session.Plan = null;
        try
        {
            var disks = _services.Disks.GetDisksAsync(); var compatibility = _services.Compatibility.InspectHostAsync();
            await Task.WhenAll(disks, compatibility);
            // The user may have navigated away while disks were being read.
            if (CurrentStep != 2 || _destinationPage != page) return;
            Session.Disks = disks.Result; Session.Compatibility = compatibility.Result;
            Session.DestinationDisk = null; Session.DestinationPartition = null; Session.BootPartition = null;
            page.ShowDisks(Session.Disks, Session.Compatibility);
            _window.SetStatus(_text.Get("StatusSelectDestination"));
        }
        catch (ESDInstallerException exception)
        {
            if (_destinationPage == page) { page.ShowError(_text.Get(exception.MessageKey), exception.Detail); _window.SetStatus(_text.Get("StatusError")); }
        }
        catch (Exception exception)
        {
            if (_destinationPage == page) { page.ShowError(_text.Get("ErrorUnexpected"), exception.Message); _window.SetStatus(_text.Get("StatusError")); }
        }
        finally { page.ShowLoading(false); _refreshingDisks = false; }
    }
    public void SelectDestination(DiskInfo disk, PartitionInfo partition)
    {
        Session.DestinationDisk = disk; Session.DestinationPartition = partition;
        Session.BootPartition = Session.Compatibility == null ? null :
            CompatibilityService.FindBootPartition(disk, Session.Compatibility.FirmwareMode, partition);
        Session.Plan = null;
    }
    public void ShowBootPage()
    {
        if (Session.Image == null || Session.Edition == null || Session.DestinationDisk == null ||
            Session.DestinationPartition == null || Session.Compatibility == null) return;
        CurrentStep = 3; _window.SetStep(3);
        var result = _services.Compatibility.CheckImageCompatibility(Session.Image, Session.Edition,
            Session.DestinationDisk, Session.DestinationPartition, Session.BootPartition, Session.Compatibility,
            Session.BypassWindows11Requirements);
        _window.PageFrame.Content = new BootPage(this, result);
        _window.SetStatus(_text.Get(result.IsValid ? "StatusBootConfigurationReady" : "StatusCompatibilityProblem"));
    }
    public PlanValidationResult SetWindows11RequirementsBypass(bool enabled)
    {
        Session.BypassWindows11Requirements = enabled && Session.AdvancedMode && Session.Image?.Generation == WindowsGeneration.Windows11;
        Session.Plan = null;
        if (Session.Image == null || Session.Edition == null || Session.DestinationDisk == null ||
            Session.DestinationPartition == null || Session.Compatibility == null)
            return new PlanValidationResult(Array.Empty<PlanIssue>());
        return _services.Compatibility.CheckImageCompatibility(Session.Image, Session.Edition,
            Session.DestinationDisk, Session.DestinationPartition, Session.BootPartition, Session.Compatibility,
            Session.BypassWindows11Requirements);
    }
    public void ShowReviewPage()
    {
        if (Session.Image == null || Session.Edition == null || Session.DestinationDisk == null ||
            Session.DestinationPartition == null || Session.BootPartition == null || Session.Compatibility == null) return;
        var result = _services.Compatibility.CheckImageCompatibility(Session.Image, Session.Edition,
            Session.DestinationDisk, Session.DestinationPartition, Session.BootPartition, Session.Compatibility,
            Session.BypassWindows11Requirements);
        if (!result.IsValid) return;
        Session.Plan = _services.PlanFactory.Create(Session); CurrentStep = 4; _window.SetStep(4);
        _window.PageFrame.Content = new ReviewPage(this, result); _window.SetStatus(_text.Get("StatusReviewPlan"));
    }
    public async Task BeginInstallationAsync()
    {
        if (Session.Plan == null || _installing) return;
        _installing = true;
        CurrentStep = 5; _window.SetStep(5); _window.SetInstallLock(true);
        var page = new ProgressPage(this); _window.PageFrame.Content = page;
        _window.SetStatus(_text.Get("StatusWaitingForAdministrator")); await page.ExecuteAsync(Session.Plan);
    }
    public void InstallationFinished(bool succeeded)
    {
        _installing = false;
        _window.SetInstallLock(false);
        _window.SetStatus(_text.Get(succeeded ? "StatusInstallationComplete" : "StatusInstallationFailed"));
    }
    public void BackFrom(int step)
    {
        switch (step)
        {
            case 1: ShowImagePage(); break; case 2: ShowEditionPage(); break;
            case 3: _ = ShowDestinationPageAsync(); break; case 4: ShowBootPage(); break;
        }
    }
}
