using System.IO;
using WeberIT.Checkup.App.Infrastructure.Commands;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.ViewModels;

public class CustomerDevicesViewModel : BaseViewModel
{
    private readonly ICustomerService _customerService;
    private readonly ICheckupScanner _checkupScanner;
    private readonly ICheckupAssessmentService _checkupAssessmentService;
    private readonly IDeviceIdentityService _deviceIdentityService;

    private readonly ICustomerCheckupComparisonService
        _customerCheckupComparisonService;

    private readonly IFileDialogService _fileDialogService;

    private readonly ICustomerCheckupPdfReportService
        _customerCheckupPdfReportService;

    private readonly IDialogService _dialogService;

    private Customer? _selectedCustomer;
    private CustomerDevice? _selectedDevice;
    private CheckupTaskList? _subscribedTaskList;

    public CustomerDevicesViewModel(
        ICustomerService customerService,
        ICheckupScanner checkupScanner,
        ICheckupAssessmentService checkupAssessmentService,
        IDeviceIdentityService deviceIdentityService,
        ICustomerCheckupComparisonService
            customerCheckupComparisonService,
        IFileDialogService fileDialogService,
        ICustomerCheckupPdfReportService
            customerCheckupPdfReportService,
        IDialogService dialogService)
    {
        ArgumentNullException.ThrowIfNull(
            customerService);

        ArgumentNullException.ThrowIfNull(
            checkupScanner);

        ArgumentNullException.ThrowIfNull(
            checkupAssessmentService);

        ArgumentNullException.ThrowIfNull(
            deviceIdentityService);

        ArgumentNullException.ThrowIfNull(
            customerCheckupComparisonService);

        ArgumentNullException.ThrowIfNull(
            fileDialogService);

        ArgumentNullException.ThrowIfNull(
            customerCheckupPdfReportService);

        ArgumentNullException.ThrowIfNull(
            dialogService);

        _customerService =
            customerService;

        _checkupScanner =
            checkupScanner;

        _checkupAssessmentService =
            checkupAssessmentService;

        _deviceIdentityService =
            deviceIdentityService;

        _customerCheckupComparisonService =
            customerCheckupComparisonService;

        _fileDialogService =
            fileDialogService;

        _customerCheckupPdfReportService =
            customerCheckupPdfReportService;

        _dialogService =
            dialogService;

        AddDeviceCommand =
            new RelayCommand(
                _ =>
                    AddDevice(),
                _ =>
                    SelectedCustomer is not null);

        RescanDeviceCommand =
            new RelayCommand(
                _ =>
                    ExecuteCustomerCheckupAction(),
                _ =>
                    SelectedCustomer is not null
                    && SelectedDevice is not null);

        ExportCustomerCheckupReportCommand =
            new RelayCommand(
                _ =>
                    ExportPreparedCustomerCheckupReport(),
                _ =>
                    SelectedCustomer is not null
                    && SelectedDevice is not null
                    && HasSelectedDevicePreparedCompletion);

        CompleteCustomerCheckupCommand =
            new RelayCommand(
                _ =>
                    CompletePreparedCustomerCheckup(),
                _ =>
                    SelectedCustomer is not null
                    && SelectedDevice is not null
                    && HasSelectedDevicePreparedCompletion);

        DeleteDeviceCommand =
            new RelayCommand(
                _ =>
                    DeleteDevice(),
                _ =>
                    SelectedCustomer is not null
                    && SelectedDevice is not null
                    && !HasSelectedDeviceInProgressCheckup);
    }

    public RelayCommand AddDeviceCommand { get; }

    public RelayCommand RescanDeviceCommand { get; }

    public RelayCommand ExportCustomerCheckupReportCommand
    {
        get;
    }

    public RelayCommand CompleteCustomerCheckupCommand
    {
        get;
    }

    public RelayCommand DeleteDeviceCommand { get; }

    public Customer? SelectedCustomer
    {
        get =>
            _selectedCustomer;

        set
        {
            if (_selectedCustomer == value)
            {
                return;
            }

            SelectedDevice =
                null;

            _selectedCustomer =
                value;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(Devices));
            OnPropertyChanged(
                nameof(DeviceCountText));
            OnPropertyChanged(
                nameof(HasSelectedDeviceInProgressCheckup));
            OnPropertyChanged(
                nameof(HasSelectedDevicePreparedCompletion));
            OnPropertyChanged(
                nameof(RescanDeviceButtonText));

            AddDeviceCommand
                .RaiseCanExecuteChanged();

            RescanDeviceCommand
                .RaiseCanExecuteChanged();

            ExportCustomerCheckupReportCommand
                .RaiseCanExecuteChanged();

            CompleteCustomerCheckupCommand
                .RaiseCanExecuteChanged();

            DeleteDeviceCommand
                .RaiseCanExecuteChanged();
        }
    }

    public CustomerDevice? SelectedDevice
    {
        get =>
            _selectedDevice;

        set
        {
            if (_selectedDevice == value)
            {
                return;
            }

            UnsubscribeFromSelectedTaskList();

            _selectedDevice =
                value;

            SubscribeToSelectedTaskList();

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(HasSelectedDeviceInProgressCheckup));
            OnPropertyChanged(
                nameof(HasSelectedDevicePreparedCompletion));
            OnPropertyChanged(
                nameof(RescanDeviceButtonText));

            RescanDeviceCommand
                .RaiseCanExecuteChanged();

            ExportCustomerCheckupReportCommand
                .RaiseCanExecuteChanged();

            CompleteCustomerCheckupCommand
                .RaiseCanExecuteChanged();

            DeleteDeviceCommand
                .RaiseCanExecuteChanged();
        }
    }

    public IEnumerable<CustomerDevice> Devices =>
        SelectedCustomer?.Devices.ToList()
        ?? Enumerable.Empty<CustomerDevice>();

    public string DeviceCountText
    {
        get
        {
            var count =
                SelectedCustomer?.Devices.Count
                ?? 0;

            return count == 1
                ? "1 Gerät gespeichert"
                : $"{count} Geräte gespeichert";
        }
    }

    public bool HasSelectedDeviceInProgressCheckup =>
        SelectedDevice?
            .CheckupSession
            .HasInProgressCustomerCheckupVisit
        ?? false;

    public bool HasSelectedDevicePreparedCompletion =>
        SelectedDevice?
            .CheckupSession
            .CurrentCustomerCheckupVisit?
            .IsCompletionPrepared
        ?? false;

    public string RescanDeviceButtonText =>
        HasSelectedDevicePreparedCompletion
            ? "Abschluss fortsetzen"
            : HasSelectedDeviceInProgressCheckup
                ? "Abschlusskontrolle"
                : "Checkup starten";

    private void AddDevice()
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        var checkupSession =
            TryCreateCheckupSession();

        if (checkupSession is null)
        {
            return;
        }

        var matchingDevice =
            _deviceIdentityService.FindMatchingDevice(
                SelectedCustomer.Devices,
                checkupSession.DeviceInformation);

        if (matchingDevice is not null)
        {
            StartCheckupForMatchingDevice(
                matchingDevice,
                checkupSession);

            return;
        }

        AddNewDevice(
            checkupSession);
    }

    private void StartCheckupForMatchingDevice(
        CustomerDevice matchingDevice,
        CheckupSession checkupSession)
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        if (matchingDevice
            .CheckupSession
            .HasInProgressCustomerCheckupVisit)
        {
            ShowActiveCheckupError(
                matchingDevice);

            return;
        }

        var confirmed =
            _dialogService.Confirm(
                "Gerät bereits vorhanden",
                $"Das Gerät \"{matchingDevice.DisplayName}\" "
                + "ist diesem Kunden bereits zugeordnet."
                + Environment.NewLine
                + Environment.NewLine
                + "Soll für dieses Gerät ein neuer "
                + "Kundencheckup gestartet werden?"
                + Environment.NewLine
                + Environment.NewLine
                + "Der neue Scan wird als unveränderlicher "
                + "Vorher-Zustand des Vorgangs gesichert.");

        if (!confirmed)
        {
            return;
        }

        StartCustomerCheckupOnDevice(
            matchingDevice,
            checkupSession);
    }

    private void AddNewDevice(
        CheckupSession checkupSession)
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        var displayName =
            !string.IsNullOrWhiteSpace(
                checkupSession.DeviceInformation.Name)
                ? checkupSession.DeviceInformation.Name
                : $"Gerät "
                  + $"{SelectedCustomer.Devices.Count + 1}";

        var device =
            new CustomerDevice
            {
                DisplayName =
                    displayName,

                CheckupSession =
                    checkupSession
            };

        try
        {
            var wasAdded =
                _customerService.AddDeviceToCustomer(
                    SelectedCustomer.Id,
                    device);

            if (!wasAdded)
            {
                ShowPersistenceError(
                    "Gerät konnte nicht gespeichert werden",
                    "Der ausgewählte Kunde ist in der "
                    + "Datenbank nicht mehr vorhanden.");

                return;
            }
        }
        catch (Exception exception)
        {
            ShowPersistenceError(
                "Gerät konnte nicht gespeichert werden",
                exception.Message);

            return;
        }

        SelectedCustomer.Devices.Add(
            device);

        SelectedDevice =
            device;

        RefreshDeviceDisplay();
    }

    private void ExecuteCustomerCheckupAction()
    {
        if (SelectedCustomer is null
            || SelectedDevice is null)
        {
            return;
        }

        if (HasSelectedDeviceInProgressCheckup)
        {
            ContinueCustomerCheckupCompletion();

            return;
        }

        StartCustomerCheckup();
    }

    private void ExportPreparedCustomerCheckupReport()
    {
        if (SelectedCustomer is null
            || SelectedDevice is null)
        {
            return;
        }

        var customer =
            SelectedCustomer;

        var device =
            SelectedDevice;

        var currentVisit =
            device
                .CheckupSession
                .CurrentCustomerCheckupVisit;

        if (currentVisit is null
            || !currentVisit.IsCompletionPrepared)
        {
            _dialogService.ShowError(
                "Kein vollständiger Abschlussentwurf",
                "Der Kundenbericht kann erst erstellt werden, "
                + "wenn Nachher-Scan, Vergleich und "
                + "Technikerangaben vollständig gespeichert sind."
                + Environment.NewLine
                + Environment.NewLine
                + "Der Kundencheckup wurde nicht verändert.");

            RefreshDeviceDisplay();

            return;
        }

        var suggestedFileName =
            BuildCustomerCheckupReportFileName(
                customer,
                device,
                currentVisit);

        var filePath =
            _fileDialogService.SelectPdfSavePath(
                suggestedFileName);

        if (string.IsNullOrWhiteSpace(
                filePath))
        {
            return;
        }

        try
        {
            _customerCheckupPdfReportService.Export(
                customer,
                device,
                currentVisit,
                filePath);
        }
        catch (Exception exception)
        {
            _dialogService.ShowError(
                "Kundenbericht konnte nicht erstellt werden",
                "Die PDF-Datei konnte nicht vollständig "
                + "erzeugt oder gespeichert werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Der Abschlussentwurf und der laufende "
                + "Kundencheckup bleiben unverändert."
                + Environment.NewLine
                + Environment.NewLine
                + "Technische Ursache:"
                + Environment.NewLine
                + BuildErrorDetails(
                    exception));
        }
    }

    private void CompletePreparedCustomerCheckup()
    {
        if (SelectedCustomer is null
            || SelectedDevice is null)
        {
            return;
        }

        var customer =
            SelectedCustomer;

        var device =
            SelectedDevice;

        var currentVisit =
            device
                .CheckupSession
                .CurrentCustomerCheckupVisit;

        if (currentVisit is null
            || !currentVisit.IsCompletionPrepared)
        {
            _dialogService.ShowError(
                "Kein vollständiger Abschlussentwurf",
                "Der Kundencheckup kann erst endgültig "
                + "abgeschlossen werden, wenn Nachher-Scan, "
                + "Vergleich und Technikerangaben vollständig "
                + "gespeichert sind."
                + Environment.NewLine
                + Environment.NewLine
                + "Der Kundencheckup wurde nicht verändert.");

            RefreshDeviceDisplay();

            return;
        }

        var confirmed =
            _dialogService.Confirm(
                "Kundencheckup endgültig abschließen",
                $"Der Kundencheckup für das Gerät "
                + $"\"{device.DisplayName}\" wird nach diesem "
                + "Schritt unveränderlich als abgeschlossen "
                + "gespeichert."
                + Environment.NewLine
                + Environment.NewLine
                + "Aufgaben, Aktionsdokumentation und "
                + "Technikerangaben können danach nicht mehr "
                + "über den laufenden Vorgang geändert werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Vor dem Datenbankabschluss muss der "
                + "Kundenbericht erfolgreich als PDF gespeichert "
                + "werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Wird die Dateiauswahl abgebrochen oder tritt "
                + "bei der PDF-Erstellung beziehungsweise beim "
                + "Datenbankabschluss ein Fehler auf, bleibt der "
                + "Kundencheckup in Bearbeitung."
                + Environment.NewLine
                + Environment.NewLine
                + "Soll der Kundencheckup jetzt endgültig "
                + "abgeschlossen werden?");

        if (!confirmed)
        {
            return;
        }

        var suggestedFileName =
            BuildCustomerCheckupReportFileName(
                customer,
                device,
                currentVisit);

        var filePath =
            _fileDialogService.SelectPdfSavePath(
                suggestedFileName);

        if (string.IsNullOrWhiteSpace(
                filePath))
        {
            return;
        }

        try
        {
            _customerCheckupPdfReportService.Export(
                customer,
                device,
                currentVisit,
                filePath);
        }
        catch (Exception exception)
        {
            _dialogService.ShowError(
                "Kundencheckup nicht abgeschlossen",
                "Der Kundenbericht konnte nicht vollständig "
                + "erzeugt oder gespeichert werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Der Datenbankabschluss wurde deshalb nicht "
                + "gestartet. Der Abschlussentwurf und der "
                + "laufende Kundencheckup bleiben unverändert."
                + Environment.NewLine
                + Environment.NewLine
                + "Technische Ursache:"
                + Environment.NewLine
                + BuildErrorDetails(
                    exception));

            return;
        }

        var previousDisplayName =
            device.DisplayName;

        var previousCheckupSession =
            device.CheckupSession;

        var previousUpdatedAt =
            device.UpdatedAt;

        CustomerCheckupVisit completedVisit;
        CheckupSession completedCheckup;

        try
        {
            completedVisit =
                CreateIndependentVisitCopy(
                    currentVisit);

            completedVisit.CompletePrepared();

            var completedAfterCheckup =
                completedVisit.AfterCheckup
                ?? throw new InvalidOperationException(
                    "Der vorbereitete Kundencheckup enthält "
                    + "keinen vollständigen Nachher-Zustand.");

            completedCheckup =
                completedAfterCheckup.RestoreAsSession();

            completedCheckup.CustomerCheckupVisits =
                previousCheckupSession
                    .CustomerCheckupVisits
                    .Select(visit =>
                        visit.Id == currentVisit.Id
                            ? completedVisit
                            : visit)
                    .ToList();
        }
        catch (Exception exception)
        {
            ShowCustomerCheckupCompletionErrorAfterPdf(
                "Kundencheckup konnte nicht vorbereitet werden",
                filePath,
                "Die Abschlussdaten konnten nach der erfolgreichen "
                + "PDF-Erstellung nicht als unabhängiger "
                + "abgeschlossener Vorgang vorbereitet werden.",
                exception.Message);

            return;
        }

        var completedComputerName =
            completedCheckup
                .DeviceInformation
                .Name;

        if (!string.IsNullOrWhiteSpace(
                completedComputerName))
        {
            device.DisplayName =
                completedComputerName;
        }

        device.CheckupSession =
            completedCheckup;

        device.UpdatedAt =
            DateTime.Now;

        try
        {
            var wasUpdated =
                _customerService.UpdateCustomerDevice(
                    customer.Id,
                    device);

            if (!wasUpdated)
            {
                RestoreDevice(
                    device,
                    previousDisplayName,
                    previousCheckupSession,
                    previousUpdatedAt);

                ShowCustomerCheckupCompletionErrorAfterPdf(
                    "Datenbankabschluss fehlgeschlagen",
                    filePath,
                    "Das Gerät oder der zugehörige Kunde "
                    + "ist in der Datenbank nicht mehr vorhanden.",
                    null);

                RefreshDeviceDisplay();

                return;
            }
        }
        catch (Exception exception)
        {
            RestoreDevice(
                device,
                previousDisplayName,
                previousCheckupSession,
                previousUpdatedAt);

            ShowCustomerCheckupCompletionErrorAfterPdf(
                "Datenbankabschluss fehlgeschlagen",
                filePath,
                "Der abgeschlossene Kundencheckup konnte nicht "
                + "dauerhaft in der Datenbank gespeichert werden.",
                exception.Message);

            RefreshDeviceDisplay();

            return;
        }

        RefreshTaskListSubscription();
        RefreshDeviceDisplay();
    }

    private void StartCustomerCheckup()
    {
        if (SelectedCustomer is null
            || SelectedDevice is null)
        {
            return;
        }

        var selectedDevice =
            SelectedDevice;

        if (selectedDevice
            .CheckupSession
            .HasInProgressCustomerCheckupVisit)
        {
            ShowActiveCheckupError(
                selectedDevice);

            return;
        }

        var checkupSession =
            TryCreateCheckupSession();

        if (checkupSession is null)
        {
            return;
        }

        var matchingDevice =
            _deviceIdentityService.FindMatchingDevice(
                SelectedCustomer.Devices,
                checkupSession.DeviceInformation);

        if (matchingDevice is not null
            && matchingDevice.Id
                != selectedDevice.Id)
        {
            var confirmed =
                _dialogService.Confirm(
                    "Anderes Gerät erkannt",
                    $"Der Eingangsscan gehört nicht zum "
                    + $"ausgewählten Gerät "
                    + $"\"{selectedDevice.DisplayName}\", "
                    + $"sondern zum bereits gespeicherten "
                    + $"Gerät \"{matchingDevice.DisplayName}\"."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Soll der Kundencheckup stattdessen "
                    + "für das erkannte Gerät gestartet werden?");

            if (!confirmed)
            {
                return;
            }

            StartCustomerCheckupOnDevice(
                matchingDevice,
                checkupSession);

            return;
        }

        if (matchingDevice is null)
        {
            var confirmed =
                _dialogService.Confirm(
                    "Gerät nicht eindeutig erkannt",
                    $"Der Eingangsscan konnte dem ausgewählten "
                    + $"Gerät \"{selectedDevice.DisplayName}\" "
                    + "nicht eindeutig zugeordnet werden."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Soll der Kundencheckup trotzdem für "
                    + "dieses Gerät gestartet und der Scan "
                    + "als Vorher-Zustand gesichert werden?");

            if (!confirmed)
            {
                return;
            }
        }

        StartCustomerCheckupOnDevice(
            selectedDevice,
            checkupSession);
    }

    private void ContinueCustomerCheckupCompletion()
    {
        if (SelectedCustomer is null
            || SelectedDevice is null)
        {
            return;
        }

        var selectedDevice =
            SelectedDevice;

        var currentVisit =
            selectedDevice
                .CheckupSession
                .CurrentCustomerCheckupVisit;

        if (currentVisit is null)
        {
            RefreshDeviceDisplay();

            return;
        }

        if (currentVisit.IsCompletionPrepared)
        {
            EditPreparedCompletion(
                selectedDevice,
                currentVisit);

            return;
        }

        RunCustomerCheckupCompletionScan(
            selectedDevice,
            currentVisit);
    }

    private void RunCustomerCheckupCompletionScan(
        CustomerDevice selectedDevice,
        CustomerCheckupVisit currentVisit)
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        var confirmed =
            _dialogService.Confirm(
                "Abschlusskontrolle starten",
                $"Für das Gerät \"{selectedDevice.DisplayName}\" "
                + "wird jetzt ein vollständiger Kontrollscan "
                + "durchgeführt."
                + Environment.NewLine
                + Environment.NewLine
                + "Der Eingangsscan und die bisherige "
                + "Aktionsdokumentation bleiben erhalten. "
                + "Nach dem Scan erscheint eine Vergleichsvorschau "
                + "mit den Technikerangaben."
                + Environment.NewLine
                + Environment.NewLine
                + "Erst mit \"Entwurf speichern\" werden "
                + "Nachher-Scan und Vergleich dauerhaft gesichert. "
                + "Der Kundencheckup bleibt dabei in Bearbeitung."
                + Environment.NewLine
                + Environment.NewLine
                + "Soll die Abschlusskontrolle jetzt starten?");

        if (!confirmed)
        {
            return;
        }

        var afterCheckup =
            TryCreateCheckupSession();

        if (afterCheckup is null)
        {
            return;
        }

        var matchingDevice =
            _deviceIdentityService.FindMatchingDevice(
                SelectedCustomer.Devices,
                afterCheckup.DeviceInformation);

        if (matchingDevice is not null
            && matchingDevice.Id
                != selectedDevice.Id)
        {
            _dialogService.ShowError(
                "Falsches Gerät erkannt",
                $"Der Kontrollscan gehört nicht zum "
                + $"ausgewählten Gerät "
                + $"\"{selectedDevice.DisplayName}\", "
                + $"sondern zum gespeicherten Gerät "
                + $"\"{matchingDevice.DisplayName}\"."
                + Environment.NewLine
                + Environment.NewLine
                + "Der laufende Kundencheckup wurde nicht "
                + "verändert. Schließen Sie das richtige "
                + "Gerät an und starten Sie die "
                + "Abschlusskontrolle erneut.");

            return;
        }

        if (matchingDevice is null)
        {
            _dialogService.ShowError(
                "Gerät nicht eindeutig erkannt",
                $"Der Kontrollscan konnte dem Gerät "
                + $"\"{selectedDevice.DisplayName}\" nicht "
                + "eindeutig zugeordnet werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Ein belastbarer Vorher-/Nachher-Vergleich "
                + "wird nur nach erfolgreicher Geräteprüfung "
                + "erstellt. Der laufende Kundencheckup wurde "
                + "nicht verändert.");

            return;
        }

        CustomerCheckupComparison comparison;

        try
        {
            comparison =
                _customerCheckupComparisonService.Compare(
                    currentVisit,
                    selectedDevice.CheckupSession,
                    afterCheckup);
        }
        catch (Exception exception)
        {
            _dialogService.ShowError(
                "Vorher-/Nachher-Vergleich fehlgeschlagen",
                "Der Nachher-Scan wurde durchgeführt, konnte "
                + "aber nicht belastbar mit dem Eingangsscan "
                + "verglichen werden. Der laufende "
                + "Kundencheckup wurde nicht verändert."
                + Environment.NewLine
                + Environment.NewLine
                + "Technische Ursache:"
                + Environment.NewLine
                + BuildErrorDetails(
                    exception));

            return;
        }

        var draft =
            _dialogService
                .ShowCustomerCheckupCompletionDialog(
                    selectedDevice.DisplayName,
                    currentVisit,
                    comparison);

        if (draft is null)
        {
            return;
        }

        PrepareCustomerCheckupCompletionOnDevice(
            selectedDevice,
            afterCheckup,
            comparison,
            draft);
    }

    private void EditPreparedCompletion(
        CustomerDevice device,
        CustomerCheckupVisit currentVisit)
    {
        if (currentVisit.Comparison is null
            || currentVisit.AfterCheckup is null)
        {
            _dialogService.ShowError(
                "Abschlussentwurf unvollständig",
                "Der gespeicherte Abschlussentwurf enthält "
                + "nicht alle erforderlichen Vergleichsdaten. "
                + "Der Kundencheckup bleibt unverändert.");

            return;
        }

        var draft =
            _dialogService
                .ShowCustomerCheckupCompletionDialog(
                    device.DisplayName,
                    currentVisit,
                    currentVisit.Comparison);

        if (draft is null)
        {
            return;
        }

        UpdatePreparedCompletionOnDevice(
            device,
            currentVisit,
            draft);
    }

    private void PrepareCustomerCheckupCompletionOnDevice(
        CustomerDevice device,
        CheckupSession afterCheckup,
        CustomerCheckupComparison comparison,
        CustomerCheckupCompletionDraft draft)
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        var workingCheckup =
            device.CheckupSession;

        var currentVisit =
            workingCheckup.CurrentCustomerCheckupVisit;

        if (currentVisit is null)
        {
            _dialogService.ShowError(
                "Kein laufender Kundencheckup",
                "Für das ausgewählte Gerät wurde kein "
                + "laufender Kundencheckup gefunden. Der "
                + "Abschlussentwurf wurde nicht gespeichert.");

            return;
        }

        var previousVisits =
            workingCheckup.CustomerCheckupVisits;

        var previousUpdatedAt =
            device.UpdatedAt;

        CustomerCheckupVisit preparedVisit;

        try
        {
            preparedVisit =
                CreateIndependentVisitCopy(
                    currentVisit);

            preparedVisit.PrepareCompletion(
                afterCheckup,
                comparison,
                draft.TechnicianSummary,
                draft.NextSteps,
                draft.NextCheckupDate);
        }
        catch (Exception exception)
        {
            ShowPersistenceError(
                "Abschlussentwurf konnte nicht vorbereitet werden",
                exception.Message);

            return;
        }

        workingCheckup.CustomerCheckupVisits =
            previousVisits
                .Select(visit =>
                    visit.Id == currentVisit.Id
                        ? preparedVisit
                        : visit)
                .ToList();

        device.UpdatedAt =
            DateTime.Now;

        try
        {
            var wasUpdated =
                _customerService.UpdateCustomerDevice(
                    SelectedCustomer.Id,
                    device);

            if (!wasUpdated)
            {
                RestorePreparedVisitChange(
                    workingCheckup,
                    previousVisits,
                    device,
                    previousUpdatedAt);

                ShowPersistenceError(
                    "Abschlussentwurf konnte nicht gespeichert werden",
                    "Das Gerät oder der zugehörige Kunde "
                    + "ist in der Datenbank nicht mehr vorhanden.");

                return;
            }
        }
        catch (Exception exception)
        {
            RestorePreparedVisitChange(
                workingCheckup,
                previousVisits,
                device,
                previousUpdatedAt);

            ShowPersistenceError(
                "Abschlussentwurf konnte nicht gespeichert werden",
                exception.Message);

            return;
        }

        SelectedDevice =
            device;

        RefreshDeviceDisplay();
    }

    private void UpdatePreparedCompletionOnDevice(
        CustomerDevice device,
        CustomerCheckupVisit currentVisit,
        CustomerCheckupCompletionDraft draft)
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        var workingCheckup =
            device.CheckupSession;

        var previousVisits =
            workingCheckup.CustomerCheckupVisits;

        var previousUpdatedAt =
            device.UpdatedAt;

        CustomerCheckupVisit updatedVisit;

        try
        {
            updatedVisit =
                CreateIndependentVisitCopy(
                    currentVisit);

            updatedVisit.UpdateCompletionDetails(
                draft.TechnicianSummary,
                draft.NextSteps,
                draft.NextCheckupDate);
        }
        catch (Exception exception)
        {
            ShowPersistenceError(
                "Technikerangaben konnten nicht übernommen werden",
                exception.Message);

            return;
        }

        workingCheckup.CustomerCheckupVisits =
            previousVisits
                .Select(visit =>
                    visit.Id == currentVisit.Id
                        ? updatedVisit
                        : visit)
                .ToList();

        device.UpdatedAt =
            DateTime.Now;

        try
        {
            var wasUpdated =
                _customerService.UpdateCustomerDevice(
                    SelectedCustomer.Id,
                    device);

            if (!wasUpdated)
            {
                RestorePreparedVisitChange(
                    workingCheckup,
                    previousVisits,
                    device,
                    previousUpdatedAt);

                ShowPersistenceError(
                    "Technikerangaben konnten nicht gespeichert werden",
                    "Das Gerät oder der zugehörige Kunde "
                    + "ist in der Datenbank nicht mehr vorhanden.");

                return;
            }
        }
        catch (Exception exception)
        {
            RestorePreparedVisitChange(
                workingCheckup,
                previousVisits,
                device,
                previousUpdatedAt);

            ShowPersistenceError(
                "Technikerangaben konnten nicht gespeichert werden",
                exception.Message);

            return;
        }

        SelectedDevice =
            device;

        RefreshDeviceDisplay();
    }

    private void StartCustomerCheckupOnDevice(
        CustomerDevice device,
        CheckupSession entranceCheckup)
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        if (device
            .CheckupSession
            .HasInProgressCustomerCheckupVisit)
        {
            ShowActiveCheckupError(
                device);

            return;
        }

        var previousDisplayName =
            device.DisplayName;

        var previousCheckupSession =
            device.CheckupSession;

        var previousUpdatedAt =
            device.UpdatedAt;

        CustomerCheckupVisit customerCheckupVisit;

        try
        {
            customerCheckupVisit =
                CustomerCheckupVisit.Start(
                    entranceCheckup);
        }
        catch (Exception exception)
        {
            ShowPersistenceError(
                "Kundencheckup konnte nicht gestartet werden",
                exception.Message);

            return;
        }

        entranceCheckup.CustomerCheckupVisits =
            previousCheckupSession
                .CustomerCheckupVisits
                .ToList();

        entranceCheckup.CustomerCheckupVisits.Add(
            customerCheckupVisit);

        var scannedComputerName =
            entranceCheckup
                .DeviceInformation
                .Name;

        if (!string.IsNullOrWhiteSpace(
                scannedComputerName))
        {
            device.DisplayName =
                scannedComputerName;
        }

        device.CheckupSession =
            entranceCheckup;

        device.UpdatedAt =
            DateTime.Now;

        try
        {
            var wasUpdated =
                _customerService.UpdateCustomerDevice(
                    SelectedCustomer.Id,
                    device);

            if (!wasUpdated)
            {
                RestoreDevice(
                    device,
                    previousDisplayName,
                    previousCheckupSession,
                    previousUpdatedAt);

                ShowPersistenceError(
                    "Kundencheckup konnte nicht gestartet werden",
                    "Das Gerät oder der zugehörige Kunde "
                    + "ist in der Datenbank nicht mehr vorhanden.");

                return;
            }
        }
        catch (Exception exception)
        {
            RestoreDevice(
                device,
                previousDisplayName,
                previousCheckupSession,
                previousUpdatedAt);

            ShowPersistenceError(
                "Kundencheckup konnte nicht gestartet werden",
                exception.Message);

            return;
        }

        SelectedDevice =
            device;

        RefreshTaskListSubscription();
        RefreshDeviceDisplay();
    }

    private void DeleteDevice()
    {
        if (SelectedCustomer is null
            || SelectedDevice is null)
        {
            return;
        }

        var device =
            SelectedDevice;

        if (device
            .CheckupSession
            .HasInProgressCustomerCheckupVisit)
        {
            ShowActiveCheckupError(
                device);

            return;
        }

        var confirmed =
            _dialogService.Confirm(
                "Gerät löschen",
                $"Soll das Gerät \"{device.DisplayName}\" "
                + "wirklich gelöscht werden?");

        if (!confirmed)
        {
            return;
        }

        try
        {
            var wasDeleted =
                _customerService.DeleteCustomerDevice(
                    SelectedCustomer.Id,
                    device.Id);

            if (!wasDeleted)
            {
                ShowPersistenceError(
                    "Gerät konnte nicht gelöscht werden",
                    "Das Gerät oder der zugehörige Kunde "
                    + "ist in der Datenbank nicht mehr vorhanden.");

                return;
            }
        }
        catch (Exception exception)
        {
            ShowPersistenceError(
                "Gerät konnte nicht gelöscht werden",
                exception.Message);

            return;
        }

        SelectedCustomer.Devices.Remove(
            device);

        SelectedDevice =
            null;

        RefreshDeviceDisplay();
    }

    private CheckupSession? TryCreateCheckupSession()
    {
        try
        {
            var checkupSession =
                _checkupScanner.Scan();

            checkupSession.Assessment =
                _checkupAssessmentService.Assess(
                    checkupSession);

            return checkupSession;
        }
        catch (Exception exception)
        {
            _dialogService.ShowError(
                "Systemscan fehlgeschlagen",
                BuildScanErrorMessage(
                    exception));

            return null;
        }
    }

    private void SubscribeToSelectedTaskList()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        _subscribedTaskList =
            SelectedDevice
                .CheckupSession
                .TaskList;

        _subscribedTaskList.PersistenceRequested +=
            SelectedTaskList_OnPersistenceRequested;
    }

    private void UnsubscribeFromSelectedTaskList()
    {
        if (_subscribedTaskList is null)
        {
            return;
        }

        _subscribedTaskList.PersistenceRequested -=
            SelectedTaskList_OnPersistenceRequested;

        _subscribedTaskList =
            null;
    }

    private void RefreshTaskListSubscription()
    {
        UnsubscribeFromSelectedTaskList();
        SubscribeToSelectedTaskList();
    }

    private void SelectedTaskList_OnPersistenceRequested(
        object? sender,
        EventArgs e)
    {
        PersistSelectedDeviceTaskList();
    }

    private void PersistSelectedDeviceTaskList()
    {
        if (SelectedCustomer is null
            || SelectedDevice is null)
        {
            var message =
                "Für die Aufgabenstatusänderung ist kein "
                + "gespeichertes Kundengerät ausgewählt.";

            ShowTaskPersistenceError(
                message);

            throw new InvalidOperationException(
                message);
        }

        var previousUpdatedAt =
            SelectedDevice.UpdatedAt;

        SelectedDevice.UpdatedAt =
            DateTime.Now;

        try
        {
            var wasUpdated =
                _customerService.UpdateCustomerDevice(
                    SelectedCustomer.Id,
                    SelectedDevice);

            if (!wasUpdated)
            {
                throw new InvalidOperationException(
                    "Das Gerät oder der zugehörige Kunde "
                    + "ist in der Datenbank nicht mehr vorhanden.");
            }
        }
        catch (Exception exception)
        {
            SelectedDevice.UpdatedAt =
                previousUpdatedAt;

            ShowTaskPersistenceError(
                exception.Message);

            throw new InvalidOperationException(
                "Der Aufgabenstatus konnte nicht "
                + "dauerhaft gespeichert werden.",
                exception);
        }

        OnPropertyChanged(
            nameof(SelectedDevice));
    }

    private void RefreshDeviceDisplay()
    {
        OnPropertyChanged(
            nameof(Devices));

        OnPropertyChanged(
            nameof(DeviceCountText));

        OnPropertyChanged(
            nameof(SelectedDevice));

        OnPropertyChanged(
            nameof(HasSelectedDeviceInProgressCheckup));

        OnPropertyChanged(
            nameof(HasSelectedDevicePreparedCompletion));

        OnPropertyChanged(
            nameof(RescanDeviceButtonText));

        RescanDeviceCommand
            .RaiseCanExecuteChanged();

        ExportCustomerCheckupReportCommand
            .RaiseCanExecuteChanged();

        CompleteCustomerCheckupCommand
            .RaiseCanExecuteChanged();

        DeleteDeviceCommand
            .RaiseCanExecuteChanged();
    }

    private void ShowActiveCheckupError(
        CustomerDevice device)
    {
        var currentVisit =
            device
                .CheckupSession
                .CurrentCustomerCheckupVisit;

        var startedAtText =
            currentVisit?.StartedAtText
            ?? "unbekanntem Zeitpunkt";

        var nextActionText =
            currentVisit?.IsCompletionPrepared
                == true
                ? "Öffnen Sie über den Button "
                  + "\"Abschluss fortsetzen\" den gespeicherten "
                  + "Abschlussentwurf."
                : "Führen Sie über den Button "
                  + "\"Abschlusskontrolle\" den Nachher-Scan durch.";

        _dialogService.ShowError(
            "Kundencheckup bereits aktiv",
            $"Für das Gerät \"{device.DisplayName}\" "
            + "läuft bereits ein Kundencheckup."
            + Environment.NewLine
            + Environment.NewLine
            + $"Der Eingangsscan vom {startedAtText} "
            + "ist als Vorher-Zustand gesichert."
            + Environment.NewLine
            + Environment.NewLine
            + nextActionText);
    }

    private void ShowTaskPersistenceError(
        string errorDetails)
    {
        var details =
            string.IsNullOrWhiteSpace(
                errorDetails)
                ? "Keine weiteren Fehlerdetails verfügbar."
                : errorDetails;

        _dialogService.ShowError(
            "Aufgabenstatus nicht gespeichert",
            "Die Statusänderung konnte nicht dauerhaft "
            + "gespeichert werden und wurde deshalb "
            + "zurückgenommen."
            + Environment.NewLine
            + Environment.NewLine
            + $"Technische Details: {details}");
    }

    private void ShowPersistenceError(
        string title,
        string errorDetails)
    {
        var details =
            string.IsNullOrWhiteSpace(
                errorDetails)
                ? "Keine weiteren Fehlerdetails verfügbar."
                : errorDetails;

        _dialogService.ShowError(
            title,
            "Die Änderung konnte nicht dauerhaft in "
            + "der Datenbank gespeichert werden. "
            + "Die Geräteliste wurde nicht als "
            + "erfolgreich aktualisiert."
            + Environment.NewLine
            + Environment.NewLine
            + $"Technische Details: {details}");
    }

    private void ShowCustomerCheckupCompletionErrorAfterPdf(
        string title,
        string filePath,
        string errorMessage,
        string? technicalDetails)
    {
        var details =
            string.IsNullOrWhiteSpace(
                technicalDetails)
                ? "Keine weiteren Fehlerdetails verfügbar."
                : technicalDetails;

        _dialogService.ShowError(
            title,
            "Die PDF-Datei wurde zwar erfolgreich erzeugt, "
            + "der Kundencheckup wurde jedoch nicht erfolgreich "
            + "in der Datenbank abgeschlossen."
            + Environment.NewLine
            + Environment.NewLine
            + errorMessage
            + Environment.NewLine
            + Environment.NewLine
            + "Der ursprüngliche Abschlussentwurf wurde "
            + "wiederhergestellt. Der Kundencheckup bleibt "
            + "in Bearbeitung."
            + Environment.NewLine
            + Environment.NewLine
            + "Die bereits erzeugte PDF-Datei darf deshalb "
            + "nicht als Nachweis eines erfolgreich gespeicherten "
            + "Abschlusses interpretiert werden."
            + Environment.NewLine
            + Environment.NewLine
            + "PDF-Datei:"
            + Environment.NewLine
            + filePath
            + Environment.NewLine
            + Environment.NewLine
            + "Technische Ursache:"
            + Environment.NewLine
            + details);
    }

    private static CustomerCheckupVisit
        CreateIndependentVisitCopy(
            CustomerCheckupVisit source)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        return new CustomerCheckupVisit
        {
            VisitModelVersion =
                source.VisitModelVersion,

            Id =
                source.Id,

            StartedAt =
                source.StartedAt,

            CompletedAt =
                source.CompletedAt,

            Status =
                source.Status,

            BeforeCheckup =
                source.BeforeCheckup,

            AfterCheckup =
                source.AfterCheckup,

            Comparison =
                source.Comparison,

            TechnicianSummary =
                source.TechnicianSummary,

            NextSteps =
                source.NextSteps,

            NextCheckupDate =
                source.NextCheckupDate,

            CancellationReason =
                source.CancellationReason
        };
    }

    private static void RestorePreparedVisitChange(
        CheckupSession workingCheckup,
        List<CustomerCheckupVisit> previousVisits,
        CustomerDevice device,
        DateTime? previousUpdatedAt)
    {
        workingCheckup.CustomerCheckupVisits =
            previousVisits;

        device.UpdatedAt =
            previousUpdatedAt;
    }

    private static void RestoreDevice(
        CustomerDevice device,
        string displayName,
        CheckupSession checkupSession,
        DateTime? updatedAt)
    {
        device.DisplayName =
            displayName;

        device.CheckupSession =
            checkupSession;

        device.UpdatedAt =
            updatedAt;
    }

    private static string BuildCustomerCheckupReportFileName(
        Customer customer,
        CustomerDevice device,
        CustomerCheckupVisit customerCheckupVisit)
    {
        var customerPart =
            !string.IsNullOrWhiteSpace(
                customer.CustomerNumber)
                ? customer.CustomerNumber
                : customer.DisplayName;

        var devicePart =
            !string.IsNullOrWhiteSpace(
                device.DisplayName)
                ? device.DisplayName
                : "Geraet";

        var reportDate =
            customerCheckupVisit
                .AfterCheckup?
                .ScanDate
            ?? DateTime.Now;

        return
            "Weber-IT-Kundencheckup-"
            + SanitizeFileNamePart(
                customerPart)
            + "-"
            + SanitizeFileNamePart(
                devicePart)
            + "-"
            + reportDate.ToString(
                "yyyy-MM-dd")
            + ".pdf";
    }

    private static string SanitizeFileNamePart(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return
                "Unbekannt";
        }

        var invalidCharacters =
            Path.GetInvalidFileNameChars();

        var normalizedCharacters =
            value
                .Trim()
                .Select(character =>
                    invalidCharacters.Contains(
                        character)
                    || char.IsWhiteSpace(
                        character)
                        ? '-'
                        : character)
                .ToArray();

        var normalizedValue =
            new string(
                normalizedCharacters);

        while (normalizedValue.Contains(
                   "--",
                   StringComparison.Ordinal))
        {
            normalizedValue =
                normalizedValue.Replace(
                    "--",
                    "-",
                    StringComparison.Ordinal);
        }

        normalizedValue =
            normalizedValue.Trim(
                '-',
                '.',
                ' ');

        return string.IsNullOrWhiteSpace(
                normalizedValue)
            ? "Unbekannt"
            : normalizedValue;
    }

    private static string BuildScanErrorMessage(
        Exception exception)
    {
        return
            "Die Systeminformationen konnten nicht "
            + "vollständig ausgelesen oder bewertet werden. "
            + "Der gespeicherte Gerätestand und ein eventuell "
            + "laufender Kundencheckup bleiben unverändert."
            + Environment.NewLine
            + Environment.NewLine
            + $"Technische Details: "
            + BuildErrorDetails(
                exception);
    }

    private static string BuildErrorDetails(
        Exception exception)
    {
        return string.IsNullOrWhiteSpace(
                exception.Message)
            ? "Keine weiteren Fehlerdetails verfügbar."
            : exception.Message;
    }
}