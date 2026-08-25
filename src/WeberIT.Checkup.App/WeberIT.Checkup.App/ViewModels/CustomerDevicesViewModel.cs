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

    private bool _isScanRunning;

    private int _scanProgressPercentage;

    private string _scanContextTitle =
        "Systemscan";

    private string _scanContextDescription =
        "Der Computer wird vollständig ausgelesen und bewertet.";

    private string _scanProgressTitle =
        "Systemscan";

    private string _scanCurrentStepText =
        "Noch kein Systemscan gestartet.";

    private string _scanProgressSummaryText =
        "0 von 12 Bereichen verarbeitet.";

    private IReadOnlyList<CheckupScanProgress>
        _scanSteps =
            CreatePendingScanSteps();

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
            new AsyncRelayCommand(
                AddDeviceAsync,
                () =>
                    SelectedCustomer is not null
                    && !IsScanRunning);

        RescanDeviceCommand =
            new AsyncRelayCommand(
                ExecuteCustomerCheckupActionAsync,
                () =>
                    SelectedCustomer is not null
                    && SelectedDevice is not null
                    && !IsScanRunning);

        ExportCustomerCheckupReportCommand =
            new RelayCommand(
                _ =>
                    ExportPreparedCustomerCheckupReport(),
                _ =>
                    SelectedCustomer is not null
                    && SelectedDevice is not null
                    && HasSelectedDevicePreparedCompletion
                    && !IsScanRunning);

        CompleteCustomerCheckupCommand =
            new RelayCommand(
                _ =>
                    CompletePreparedCustomerCheckup(),
                _ =>
                    SelectedCustomer is not null
                    && SelectedDevice is not null
                    && HasSelectedDevicePreparedCompletion
                    && !IsScanRunning);

        DeleteDeviceCommand =
            new RelayCommand(
                _ =>
                    DeleteDevice(),
                _ =>
                    SelectedCustomer is not null
                    && SelectedDevice is not null
                    && !HasSelectedDeviceInProgressCheckup
                    && !IsScanRunning);
    }

    public AsyncRelayCommand AddDeviceCommand { get; }

    public AsyncRelayCommand RescanDeviceCommand { get; }

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
        IsScanRunning
            ? "Systemscan läuft …"
            : HasSelectedDevicePreparedCompletion
                ? "Abschluss fortsetzen"
                : HasSelectedDeviceInProgressCheckup
                    ? "Abschlusskontrolle"
                    : "Checkup starten";

    public bool IsScanRunning
    {
        get =>
            _isScanRunning;

        private set
        {
            if (_isScanRunning == value)
            {
                return;
            }

            _isScanRunning =
                value;

            OnPropertyChanged();

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

    public string ScanContextTitle
    {
        get =>
            _scanContextTitle;

        private set
        {
            if (string.Equals(
                    _scanContextTitle,
                    value,
                    StringComparison.Ordinal))
            {
                return;
            }

            _scanContextTitle =
                value;

            OnPropertyChanged();
        }
    }

    public string ScanContextDescription
    {
        get =>
            _scanContextDescription;

        private set
        {
            if (string.Equals(
                    _scanContextDescription,
                    value,
                    StringComparison.Ordinal))
            {
                return;
            }

            _scanContextDescription =
                value;

            OnPropertyChanged();
        }
    }

    public int ScanProgressPercentage
    {
        get =>
            _scanProgressPercentage;

        private set
        {
            var normalizedValue =
                Math.Clamp(
                    value,
                    0,
                    100);

            if (_scanProgressPercentage
                == normalizedValue)
            {
                return;
            }

            _scanProgressPercentage =
                normalizedValue;

            OnPropertyChanged();

            OnPropertyChanged(
                nameof(ScanProgressPercentageText));
        }
    }

    public string ScanProgressPercentageText =>
        $"{ScanProgressPercentage} %";

    public string ScanProgressTitle
    {
        get =>
            _scanProgressTitle;

        private set
        {
            if (string.Equals(
                    _scanProgressTitle,
                    value,
                    StringComparison.Ordinal))
            {
                return;
            }

            _scanProgressTitle =
                value;

            OnPropertyChanged();
        }
    }

    public string ScanCurrentStepText
    {
        get =>
            _scanCurrentStepText;

        private set
        {
            if (string.Equals(
                    _scanCurrentStepText,
                    value,
                    StringComparison.Ordinal))
            {
                return;
            }

            _scanCurrentStepText =
                value;

            OnPropertyChanged();
        }
    }

    public string ScanProgressSummaryText
    {
        get =>
            _scanProgressSummaryText;

        private set
        {
            if (string.Equals(
                    _scanProgressSummaryText,
                    value,
                    StringComparison.Ordinal))
            {
                return;
            }

            _scanProgressSummaryText =
                value;

            OnPropertyChanged();
        }
    }

    public IReadOnlyList<CheckupScanProgress> ScanSteps
    {
        get =>
            _scanSteps;

        private set
        {
            if (ReferenceEquals(
                    _scanSteps,
                    value))
            {
                return;
            }

            _scanSteps =
                value;

            OnPropertyChanged();
        }
    }

    private async Task AddDeviceAsync()
    {
        var customer =
            SelectedCustomer;

        if (customer is null)
        {
            return;
        }

        var checkupSession =
            await TryCreateCheckupSessionAsync(
                "Gerät hinzufügen",
                "Der angeschlossene Computer wird vollständig "
                + "ausgelesen und bewertet. Bei einem neuen Gerät "
                + "wird dieser Scan anschließend als "
                + "Vorher-Zustand des ersten Kundencheckups "
                + "gesichert.");

        if (checkupSession is null)
        {
            return;
        }

        if (!IsCurrentScanContext(
                customer))
        {
            ShowChangedScanContextError();

            return;
        }

        var matchingDevice =
            _deviceIdentityService.FindMatchingDevice(
                customer.Devices,
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

        /*
         * Ein Scan, der unmittelbar über
         * "Gerät hinzufügen" entstanden ist, ist bei einem
         * tatsächlich neuen Gerät zugleich der Eingangsscan
         * des ersten Kundencheckups.
         *
         * Der Scanner erzeugt eine frische CheckupSession.
         * Sollte sie unerwartet bereits Besuchsdaten enthalten,
         * wird das neue Gerät nicht gespeichert. So verhindern
         * wir, dass fremde oder historische Vorgangsdaten
         * versehentlich an ein neues Kundengerät übernommen
         * werden.
         */
        if (checkupSession.CustomerCheckupVisits.Count > 0)
        {
            _dialogService.ShowError(
                "Kundencheckup konnte nicht gestartet werden",
                "Der neue Systemscan enthält unerwartet bereits "
                + "einen Kundencheckup-Verlauf."
                + Environment.NewLine
                + Environment.NewLine
                + "Das Gerät wurde deshalb vorsorglich nicht "
                + "gespeichert. Bitte führen Sie den Vorgang "
                + "erneut mit einem frischen Scan durch.");

            return;
        }

        CustomerCheckupVisit customerCheckupVisit;

        try
        {
            customerCheckupVisit =
                CustomerCheckupVisit.Start(
                    checkupSession);
        }
        catch (Exception exception)
        {
            _dialogService.ShowError(
                "Kundencheckup konnte nicht gestartet werden",
                "Der gerade durchgeführte Systemscan konnte "
                + "nicht als unveränderlicher Vorher-Zustand "
                + "des neuen Kundencheckups vorbereitet werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Das Gerät wurde nicht gespeichert."
                + Environment.NewLine
                + Environment.NewLine
                + "Technische Ursache:"
                + Environment.NewLine
                + BuildErrorDetails(
                    exception));

            return;
        }

        /*
         * Besuch und aktueller Arbeitsstand werden gemeinsam
         * im ersten Datenbankvorgang gespeichert.
         *
         * CustomerCheckupVisit.Start(...) erzeugt über das
         * Modell den unabhängigen CheckupSnapshot. Die
         * CheckupSession selbst bleibt der veränderbare
         * Arbeitsstand des laufenden Kundencheckups.
         */
        checkupSession.CustomerCheckupVisits.Add(
            customerCheckupVisit);

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

    private async Task ExecuteCustomerCheckupActionAsync()
    {
        if (SelectedCustomer is null
            || SelectedDevice is null)
        {
            return;
        }

        if (HasSelectedDeviceInProgressCheckup)
        {
            await ContinueCustomerCheckupCompletionAsync();

            return;
        }

        await StartCustomerCheckupAsync();
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

    private async Task StartCustomerCheckupAsync()
    {
        var customer =
            SelectedCustomer;

        var selectedDevice =
            SelectedDevice;

        if (customer is null
            || selectedDevice is null)
        {
            return;
        }

        if (selectedDevice
            .CheckupSession
            .HasInProgressCustomerCheckupVisit)
        {
            ShowActiveCheckupError(
                selectedDevice);

            return;
        }

        var checkupSession =
            await TryCreateCheckupSessionAsync(
                "Kundencheckup starten",
                $"Für das Gerät \"{selectedDevice.DisplayName}\" "
                + "wird ein vollständiger Eingangsscan "
                + "durchgeführt. Der erfolgreiche Scan bildet "
                + "anschließend den unveränderlichen "
                + "Vorher-Zustand des neuen Kundencheckups.");

        if (checkupSession is null)
        {
            return;
        }

        if (!IsCurrentScanContext(
                customer,
                selectedDevice))
        {
            ShowChangedScanContextError();

            return;
        }

        var matchingDevice =
            _deviceIdentityService.FindMatchingDevice(
                customer.Devices,
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

    private async Task ContinueCustomerCheckupCompletionAsync()
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

        await RunCustomerCheckupCompletionScanAsync(
            selectedDevice,
            currentVisit);
    }

    private async Task RunCustomerCheckupCompletionScanAsync(
        CustomerDevice selectedDevice,
        CustomerCheckupVisit currentVisit)
    {
        var customer =
            SelectedCustomer;

        if (customer is null)
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
            await TryCreateCheckupSessionAsync(
                "Abschlusskontrolle",
                $"Für das Gerät \"{selectedDevice.DisplayName}\" "
                + "wird der aktuelle Nachher-Zustand vollständig "
                + "ausgelesen und bewertet. Eingangsscan und "
                + "bisherige Aktionsdokumentation bleiben "
                + "währenddessen unverändert.");

        if (afterCheckup is null)
        {
            return;
        }

        if (!IsCurrentScanContext(
                customer,
                selectedDevice))
        {
            ShowChangedScanContextError();

            return;
        }

        var matchingDevice =
            _deviceIdentityService.FindMatchingDevice(
                customer.Devices,
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

    private async Task<CheckupSession?>
        TryCreateCheckupSessionAsync(
            string contextTitle,
            string contextDescription)
    {
        BeginScanProgress(
            contextTitle,
            contextDescription);

        IProgress<CheckupScanProgress> progress =
            new Progress<CheckupScanProgress>(
                ApplyScanProgress);

        try
        {
            var checkupSession =
                await Task.Run(
                    () =>
                    {
                        var session =
                            _checkupScanner.Scan(
                                progress);

                        progress.Report(
                            CheckupScanProgress.CreateRunning(
                                CheckupScanStepCatalog.Assessment));

                        try
                        {
                            session.Assessment =
                                _checkupAssessmentService.Assess(
                                    session);

                            progress.Report(
                                CheckupScanProgress.CreateSuccessful(
                                    CheckupScanStepCatalog.Assessment));
                        }
                        catch (Exception exception)
                        {
                            progress.Report(
                                CheckupScanProgress.CreateFailed(
                                    CheckupScanStepCatalog.Assessment,
                                    BuildProgressErrorMessage(
                                        exception)));

                            throw new InvalidOperationException(
                                "Die Bewertung und Aufgabenerzeugung "
                                + "konnte nicht abgeschlossen werden.",
                                exception);
                        }

                        return session;
                    });

            CompleteScanProgress();

            return checkupSession;
        }
        catch (Exception exception)
        {
            FailScanProgress(
                exception);

            _dialogService.ShowError(
                "Systemscan fehlgeschlagen",
                BuildScanErrorMessage(
                    exception));

            return null;
        }
        finally
        {
            IsScanRunning =
                false;
        }
    }

    private void BeginScanProgress(
        string contextTitle,
        string contextDescription)
    {
        ScanContextTitle =
            contextTitle;

        ScanContextDescription =
            contextDescription;

        ScanSteps =
            CreatePendingScanSteps();

        ScanProgressPercentage =
            0;

        ScanProgressTitle =
            "Systemscan wird vorbereitet";

        ScanCurrentStepText =
            "Die einzelnen Scanbereiche werden vorbereitet …";

        UpdateScanProgressSummary();

        IsScanRunning =
            true;
    }

    private void ApplyScanProgress(
        CheckupScanProgress progress)
    {
        ArgumentNullException.ThrowIfNull(
            progress);

        ScanSteps =
            ScanSteps
                .Select(
                    existingStep =>
                        string.Equals(
                            existingStep.StepCode,
                            progress.StepCode,
                            StringComparison.Ordinal)
                            ? progress
                            : existingStep)
                .ToList();

        ScanProgressPercentage =
            Math.Max(
                ScanProgressPercentage,
                progress.ProgressPercentage);

        ScanProgressTitle =
            progress.Status
                == CheckupScanStepStatus.Failed
                    ? "Systemscan fehlgeschlagen"
                    : "Systemscan läuft";

        ScanCurrentStepText =
            progress.Status switch
            {
                CheckupScanStepStatus.Running =>
                    progress.StepTitle
                    + " wird ausgelesen …",

                CheckupScanStepStatus.Successful =>
                    progress.StepTitle
                    + " wurde abgeschlossen.",

                CheckupScanStepStatus.Warning =>
                    progress.StepTitle
                    + " wurde mit einem Hinweis abgeschlossen.",

                CheckupScanStepStatus.Failed =>
                    "Fehler bei: "
                    + progress.StepTitle,

                _ =>
                    progress.StepTitle
            };

        UpdateScanProgressSummary();
    }

    private void CompleteScanProgress()
    {
        ScanProgressPercentage =
            100;

        ScanProgressTitle =
            "Systemscan erfolgreich abgeschlossen";

        ScanCurrentStepText =
            "Alle Scanbereiche wurden verarbeitet. "
            + "Die Ergebnisse und Aufgaben stehen jetzt bereit.";

        UpdateScanProgressSummary();
    }

    private void FailScanProgress(
        Exception exception)
    {
        var errorMessage =
            BuildProgressErrorMessage(
                exception);

        if (!ScanSteps.Any(
                step =>
                    step.Status
                    == CheckupScanStepStatus.Failed))
        {
            var activeStep =
                ScanSteps.FirstOrDefault(
                    step =>
                        step.Status
                        == CheckupScanStepStatus.Running);

            if (activeStep is not null)
            {
                var failedStep =
                    activeStep with
                    {
                        Status =
                            CheckupScanStepStatus.Failed,

                        ProgressPercentage =
                            Math.Max(
                                activeStep.ProgressPercentage,
                                ScanProgressPercentage),

                        Message =
                            errorMessage
                    };

                ScanSteps =
                    ScanSteps
                        .Select(
                            step =>
                                string.Equals(
                                    step.StepCode,
                                    failedStep.StepCode,
                                    StringComparison.Ordinal)
                                    ? failedStep
                                    : step)
                        .ToList();
            }
        }

        ScanProgressTitle =
            "Systemscan fehlgeschlagen";

        ScanCurrentStepText =
            "Der Systemscan wurde nicht vollständig "
            + "abgeschlossen. Der gespeicherte Gerätestand "
            + "und ein eventuell laufender Kundencheckup "
            + "bleiben unverändert.";

        UpdateScanProgressSummary();
    }

    private void UpdateScanProgressSummary()
    {
        var processedStepCount =
            ScanSteps.Count(
                step =>
                    step.Status
                        is CheckupScanStepStatus.Successful
                        or CheckupScanStepStatus.Warning
                        or CheckupScanStepStatus.Failed);

        var warningCount =
            ScanSteps.Count(
                step =>
                    step.Status
                    == CheckupScanStepStatus.Warning);

        var failureCount =
            ScanSteps.Count(
                step =>
                    step.Status
                    == CheckupScanStepStatus.Failed);

        var summary =
            $"{processedStepCount} von "
            + $"{CheckupScanStepCatalog.TotalStepCount} "
            + "Bereichen verarbeitet";

        if (warningCount == 0
            && failureCount == 0)
        {
            ScanProgressSummaryText =
                summary
                + " · keine Hinweise";

            return;
        }

        var statusParts =
            new List<string>();

        if (warningCount > 0)
        {
            statusParts.Add(
                warningCount == 1
                    ? "1 Hinweis"
                    : $"{warningCount} Hinweise");
        }

        if (failureCount > 0)
        {
            statusParts.Add(
                failureCount == 1
                    ? "1 Fehler"
                    : $"{failureCount} Fehler");
        }

        ScanProgressSummaryText =
            summary
            + " · "
            + string.Join(
                " · ",
                statusParts);
    }

    private static IReadOnlyList<CheckupScanProgress>
        CreatePendingScanSteps()
    {
        return CheckupScanStepCatalog
            .AllSteps
            .Select(
                CheckupScanProgress.CreatePending)
            .ToList();
    }

    private bool IsCurrentScanContext(
        Customer customer,
        CustomerDevice? device = null)
    {
        if (SelectedCustomer?.Id
            != customer.Id)
        {
            return false;
        }

        if (device is null)
        {
            return true;
        }

        return SelectedDevice?.Id
            == device.Id;
    }

    private void ShowChangedScanContextError()
    {
        _dialogService.ShowError(
            "Kundenauswahl wurde verändert",
            "Während des Systemscans hat sich die aktive "
            + "Kunden- oder Geräteauswahl verändert."
            + Environment.NewLine
            + Environment.NewLine
            + "Der neue Scan wird deshalb vorsorglich nicht "
            + "einem Kundengerät zugeordnet. Bereits gespeicherte "
            + "Checkup-Daten bleiben unverändert.");
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
            + "Technische Details: "
            + BuildErrorDetails(
                exception);
    }

    private static string BuildProgressErrorMessage(
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(
            exception);

        var messages =
            new List<string>();

        Exception? currentException =
            exception;

        while (currentException is not null
               && messages.Count < 5)
        {
            if (!string.IsNullOrWhiteSpace(
                    currentException.Message))
            {
                var message =
                    currentException.Message.Trim();

                if (!messages.Contains(
                        message,
                        StringComparer.OrdinalIgnoreCase))
                {
                    messages.Add(
                        message);
                }
            }

            currentException =
                currentException.InnerException;
        }

        return messages.Count > 0
            ? string.Join(
                " → ",
                messages)
            : "Keine weiteren Fehlerdetails verfügbar.";
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