
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.Services.Updates;
using WeberIT.Checkup.App.ViewModels;

namespace WeberIT.Checkup.App.Views.Dialogs;

/// <summary>
/// Ergänzt den bestehenden Programmupdate-Auswahldialog
/// um eine WinGet-Zwischenprüfung.
///
/// Der bestehende Installationsablauf bleibt erhalten.
/// Die Arbeitsliste wird nur nach einer zuverlässig
/// abgeschlossenen Zwischenprüfung aktualisiert.
///
/// Ein nicht zuverlässiger Scan wird dokumentiert,
/// berechtigt aber nicht zur Erstellung neuer
/// Installationspläne.
///
/// Diese Erweiterung benötigt keine Änderung am
/// bisherigen Konstruktor des Auswahldialogs.
/// </summary>
public partial class ProgramUpdateSelectionDialog
{
    private bool _intermediateScanInitialized;

    private bool _intermediateScanRunning;

    private bool _intermediateInstallationBlocked;

    private string _intermediateStatusText =
        string.Empty;

    private ICheckupTaskActionExecutionCoordinator?
        _intermediateExecutionCoordinator;

    // =============================================
    // DIALOG INITIALISIEREN
    // =============================================

    protected override void OnContentRendered(
        EventArgs e)
    {
        base.OnContentRendered(e);

        if (_intermediateScanInitialized)
        {
            return;
        }

        _intermediateScanInitialized = true;

        RefreshProgramUpdatesButton.Click +=
            RefreshProgramUpdatesButton_OnClick;

        UpdatesListBox.SelectionChanged +=
            IntermediateUpdatesListBox_OnSelectionChanged;

        if (Application.Current is App application)
        {
            _intermediateExecutionCoordinator =
                application.Services
                    .GetRequiredService<
                        ICheckupTaskActionExecutionCoordinator>();

            _intermediateExecutionCoordinator.StateChanged +=
                IntermediateExecutionCoordinator_OnStateChanged;
        }

        if (TryResolveIntermediateCustomerContext(
                out _,
                out _,
                out _,
                out var visit))
        {
            LoadPreviousIntermediateScans(
                visit);
        }

        UpdateIntermediateControls();
    }

    protected override void OnClosing(
        CancelEventArgs e)
    {
        if (_intermediateScanRunning)
        {
            e.Cancel = true;

            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(
        EventArgs e)
    {
        if (_intermediateExecutionCoordinator is not null)
        {
            _intermediateExecutionCoordinator.StateChanged -=
                IntermediateExecutionCoordinator_OnStateChanged;
        }

        base.OnClosed(e);
    }

    // =============================================
    // KUNDENKONTEXT PRÜFEN
    // =============================================

    private bool TryResolveIntermediateCustomerContext(
        out CustomerDevicesViewModel viewModel,
        out Customer customer,
        out CustomerDevice device,
        out CustomerCheckupVisit visit)
    {
        viewModel = null!;
        customer = null!;
        device = null!;
        visit = null!;

        if (Owner?.DataContext
            is not CustomerDevicesViewModel
                customerDevicesViewModel)
        {
            return false;
        }

        var selectedCustomer =
            customerDevicesViewModel.SelectedCustomer;

        var selectedDevice =
            customerDevicesViewModel.SelectedDevice;

        if (selectedCustomer is null
            || selectedDevice is null
            || customerDevicesViewModel.IsScanRunning)
        {
            return false;
        }

        if (!selectedCustomer.Devices.Any(
                storedDevice =>
                    ReferenceEquals(
                        storedDevice,
                        selectedDevice)))
        {
            return false;
        }

        var session =
            selectedDevice.CheckupSession;

        if (!ReferenceEquals(
                session.TaskList,
                _taskList))
        {
            return false;
        }

        if (!_taskList.Tasks.Any(
                storedTask =>
                    ReferenceEquals(
                        storedTask,
                        _task)))
        {
            return false;
        }

        var currentVisit =
            session.CurrentCustomerCheckupVisit;

        if (currentVisit is null
            || !currentVisit.IsInProgress
            || currentVisit.IsCompletionPrepared)
        {
            return false;
        }

        viewModel =
            customerDevicesViewModel;

        customer =
            selectedCustomer;

        device =
            selectedDevice;

        visit =
            currentVisit;

        return true;
    }

    // =============================================
    // VORHANDENE ZWISCHENPRÜFUNGEN
    // =============================================

    private void LoadPreviousIntermediateScans(
        CustomerCheckupVisit visit)
    {
        var latest =
            visit.ProgramUpdateIntermediateScans
                .OrderByDescending(
                    scan => scan.RecordedAt)
                .FirstOrDefault();

        if (latest is null)
        {
            return;
        }

        /*
         * Ein älterer zuverlässiger Befund kann
         * weiterhin angezeigt werden.
         *
         * Falls die jüngste Prüfung fehlgeschlagen
         * ist, darf daraus jedoch kein neuer
         * Installationsplan entstehen.
         */
        var latestReliable =
            visit.ProgramUpdateIntermediateScans
                .Where(scan => scan.IsReliable)
                .OrderByDescending(
                    scan => scan.RecordedAt)
                .FirstOrDefault();

        if (latestReliable is not null)
        {
            ApplyIntermediateWorkingList(
                latestReliable
                    .CreateIndependentInformationCopy(),
                visit);
        }

        if (!latest.IsReliable)
        {
            BlockInstallationAfterUnreliableScan();

            return;
        }

        SetIntermediateResultText(
            latest);
    }

    // =============================================
    // ZWISCHENPRÜFUNG AUSFÜHREN
    // =============================================

    private async void RefreshProgramUpdatesButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_intermediateScanRunning)
        {
            return;
        }

        if (!TryResolveIntermediateCustomerContext(
                out _,
                out var customer,
                out var device,
                out _))
        {
            ShowMessage(
                "WinGet-Zwischenprüfung nicht verfügbar",
                "Der aktuelle Kundencheckup konnte "
                + "nicht eindeutig zugeordnet werden.");

            return;
        }

        _intermediateScanRunning = true;

        _intermediateStatusText =
            "Die WinGet-Zwischenprüfung läuft. "
            + "Bitte warten Sie auf das Ergebnis.";

        UpdateIntermediateControls();

        try
        {
            var application =
                Application.Current as App
                ?? throw new InvalidOperationException(
                    "Der Anwendungsdienst "
                    + "ist nicht verfügbar.");

            var service =
                application.Services
                    .GetRequiredService<
                        ProgramUpdateIntermediateScanService>();

            var result =
                await service.RunAsync(
                    customer,
                    device);

            /*
             * Der Scan-Dienst speichert eine
             * unabhängige Vorgangskopie.
             *
             * Daher muss der aktuelle Kundencheckup
             * anschließend erneut aufgelöst werden.
             */
            if (!TryResolveIntermediateCustomerContext(
                    out _,
                    out _,
                    out _,
                    out var updatedVisit))
            {
                _intermediateInstallationBlocked = true;

                _intermediateStatusText =
                    "Der Zwischenbefund wurde verarbeitet, "
                    + "der aktuelle Kundencheckup konnte "
                    + "anschließend jedoch nicht mehr "
                    + "eindeutig zugeordnet werden.";

                return;
            }

            if (!result.IsReliable)
            {
                BlockInstallationAfterUnreliableScan();

                return;
            }

            ApplyIntermediateWorkingList(
                result.CreateIndependentInformationCopy(),
                updatedVisit);

            _intermediateInstallationBlocked = false;

            SetIntermediateResultText(
                result);
        }
        catch (Exception exception)
        {
            _intermediateInstallationBlocked = true;

            _intermediateStatusText =
                "Die Zwischenprüfung konnte nicht "
                + "erfolgreich abgeschlossen werden. "
                + "Die bisherige Arbeitsliste ist "
                + "nicht als aktueller Befund bestätigt.";

            ShowMessage(
                "WinGet-Zwischenprüfung fehlgeschlagen",
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Die WinGet-Zwischenprüfung "
                      + "konnte nicht ausgeführt werden."
                    : exception.Message);
        }
        finally
        {
            _intermediateScanRunning = false;

            UpdateIntermediateControls();
        }
    }

    // =============================================
    // AKTUELLE ARBEITSLISTE ERZEUGEN
    // =============================================

    private void ApplyIntermediateWorkingList(
        ProgramUpdateInformation information,
        CustomerCheckupVisit visit)
    {
        ArgumentNullException.ThrowIfNull(
            information);

        ArgumentNullException.ThrowIfNull(
            visit);

        /*
         * Nur vollständig zuverlässige
         * Scanergebnisse werden in eine neue
         * Installationsarbeitsliste übernommen.
         */
        if (information.IsWingetAvailable != true
            || !information.IsAnalysisPerformed
            || !information.IsAnalysisSuccessful
            || information.IsResultTruncated
            || !information.AnalysisDate.HasValue
            || information.AvailableUpdates is null
            || information.AvailableUpdateCount
                != information.AvailableUpdates.Count)
        {
            throw new InvalidOperationException(
                "Die neue WinGet-Arbeitsliste "
                + "konnte nicht zuverlässig "
                + "ermittelt werden.");
        }

        var statuses =
            ProgramUpdatePackageWorkStatusService
                .Evaluate(
                    information,
                    _taskList);

        /*
         * Vorhandene Technikerentscheidungen
         * werden anhand der vollständigen
         * Paket- und Versionsidentität
         * erneut berücksichtigt.
         */
        var applicableExceptions =
            visit.ProgramUpdateExceptions
                .Where(approval =>
                    approval is not null
                    && approval.CheckupVisitId == visit.Id)
                .ToList();

        var selectableUpdates =
            statuses
                .Where(status =>
                    status.CanOfferInstallation)
                .Select(status =>
                    status.Update)
                .Where(update =>
                    update is not null
                    && !applicableExceptions.Any(
                        approval =>
                            approval.AppliesTo(
                                visit.Id,
                                update)))
                .Select(CloneUpdate)
                .OrderBy(
                    update => update.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(
                    update => update.PackageId,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        /*
         * Der bisherige Auswahldialog verwendet
         * sowohl für die sichtbare Liste als auch
         * für den Aktionsplan dieselben Daten.
         *
         * Deshalb aktualisieren wir ausdrücklich
         * beides.
         */
        if (AvailableUpdates
            is not List<AvailableProgramUpdate>
                displayedUpdates)
        {
            throw new InvalidOperationException(
                "Die bestehende Programmupdate-Liste "
                + "kann nicht sicher aktualisiert werden.");
        }

        UpdatesListBox.UnselectAll();

        displayedUpdates.Clear();

        displayedUpdates.AddRange(
            selectableUpdates);

        _programUpdateInformation.IsWingetAvailable =
            information.IsWingetAvailable;

        _programUpdateInformation.WingetVersion =
            information.WingetVersion;

        _programUpdateInformation.IsAnalysisPerformed =
            information.IsAnalysisPerformed;

        _programUpdateInformation.IsAnalysisSuccessful =
            information.IsAnalysisSuccessful;

        _programUpdateInformation.AnalysisDate =
            information.AnalysisDate;

        _programUpdateInformation.AnalysisDetails =
            information.AnalysisDetails;

        _programUpdateInformation.IsResultTruncated =
            information.IsResultTruncated;

        _programUpdateInformation.AvailableUpdateCount =
            selectableUpdates.Count;

        _programUpdateInformation.AvailableUpdates =
            selectableUpdates
                .Select(CloneUpdate)
                .ToList();

        /*
         * Die vorhandene Listenbindung verweist
         * auf eine normale List<T>.
         *
         * Wir behalten deshalb die ursprüngliche
         * Listeninstanz bei und aktualisieren
         * anschließend die WPF-Ansicht.
         */
        UpdatesListBox.Items.Refresh();

        OnPropertyChanged(
            nameof(AvailableUpdateCountText));

        PreviewPlanButton.IsEnabled = false;
    }

    // =============================================
    // ERGEBNISSTATUS
    // =============================================

    private void SetIntermediateResultText(
        ProgramUpdateIntermediateScan result)
    {
        var availableCount =
            result.ConfirmedAvailableUpdateCount;

        if (!availableCount.HasValue)
        {
            BlockInstallationAfterUnreliableScan();

            return;
        }

        var availableText =
            availableCount.Value switch
            {
                0 =>
                    "Keine weiteren WinGet-Updates erkannt.",

                1 =>
                    "Ein weiteres WinGet-Update erkannt.",

                _ =>
                    $"{availableCount.Value} weitere "
                    + "WinGet-Updates erkannt."
            };

        _intermediateStatusText =
            "WinGet-Zwischenprüfung vom "
            + result.RecordedAt
                .ToLocalTime()
                .ToString("dd.MM.yyyy HH:mm")
            + " Uhr"
            + Environment.NewLine
            + availableText
            + Environment.NewLine
            + "Die Installationsarbeitsliste "
            + "wurde anhand des aktuellen "
            + "WinGet-Befunds neu berechnet.";

        UpdateIntermediateSelectionStatus();
    }

    private void BlockInstallationAfterUnreliableScan()
    {
        _intermediateInstallationBlocked = true;

        _intermediateStatusText =
            "Die letzte WinGet-Zwischenprüfung "
            + "war nicht vollständig zuverlässig."
            + Environment.NewLine
            + "Es liegt kein bestätigter "
            + "aktueller Updatebestand vor."
            + Environment.NewLine
            + "Neue Installationspläne bleiben "
            + "bis zu einer zuverlässigen "
            + "erneuten Prüfung gesperrt.";

        UpdateIntermediateSelectionStatus();
    }

    // =============================================
    // SCHALTFLÄCHENSTATUS
    // =============================================

    private void UpdateIntermediateControls()
    {
        var hasCustomerContext =
            TryResolveIntermediateCustomerContext(
                out _,
                out _,
                out _,
                out _);

        RefreshProgramUpdatesButton.Visibility =
            hasCustomerContext
                ? Visibility.Visible
                : Visibility.Collapsed;

        var otherExecutionRunning =
            _intermediateExecutionCoordinator?
                .IsExecutionRunning
            ?? false;

        RefreshProgramUpdatesButton.IsEnabled =
            hasCustomerContext
            && !_intermediateScanRunning
            && !otherExecutionRunning;

        RefreshProgramUpdatesButton.Content =
            _intermediateScanRunning
                ? "WinGet-Prüfung läuft …"
                : "Programmupdates erneut prüfen";

        UpdatesListBox.IsEnabled =
            !_intermediateScanRunning
            && !_intermediateInstallationBlocked;

        PreviewPlanButton.IsEnabled =
            !_intermediateScanRunning
            && !_intermediateInstallationBlocked
            && UpdatesListBox.SelectedItems.Count > 0
            && (Owner?.DataContext
                    is not CustomerDevicesViewModel
                || hasCustomerContext);

        UpdateIntermediateSelectionStatus();
    }

    private void IntermediateUpdatesListBox_OnSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_intermediateScanRunning
            || _intermediateInstallationBlocked)
        {
            PreviewPlanButton.IsEnabled = false;
        }

        UpdateIntermediateSelectionStatus();
    }

    private void UpdateIntermediateSelectionStatus()
    {
        if (string.IsNullOrWhiteSpace(
                _intermediateStatusText))
        {
            return;
        }

        var selectedCount =
            UpdatesListBox.SelectedItems.Count;

        var selectionText =
            selectedCount switch
            {
                0 =>
                    "Keine Programme ausgewählt.",

                1 =>
                    "Ein Programm zur Installation ausgewählt.",

                _ =>
                    $"{selectedCount} Programme "
                    + "zur Installation ausgewählt."
            };

        SelectionStatusText =
            _intermediateStatusText
            + Environment.NewLine
            + Environment.NewLine
            + selectionText;
    }

    private void IntermediateExecutionCoordinator_OnStateChanged(
        object? sender,
        EventArgs e)
    {
        if (Dispatcher.CheckAccess())
        {
            UpdateIntermediateControls();

            return;
        }

        Dispatcher.BeginInvoke(
            new Action(UpdateIntermediateControls));
    }
}
