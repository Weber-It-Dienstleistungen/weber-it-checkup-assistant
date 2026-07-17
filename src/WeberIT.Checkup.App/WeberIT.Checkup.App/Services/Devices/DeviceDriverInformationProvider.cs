using System.Diagnostics;
using System.Management;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Devices;

public class DeviceDriverInformationProvider :
    IDeviceDriverInformationProvider
{
    private static readonly TimeSpan AnalysisTimeLimit =
        TimeSpan.FromSeconds(8);

    private static readonly HashSet<string>
        IndividuallyRelevantDeviceClasses =
            new(
                new[]
                {
                    "Display",
                    "Net",
                    "MEDIA",
                    "HDC",
                    "SCSIAdapter",
                    "Bluetooth",
                    "Camera",
                    "Image",
                    "Printer",
                    "Ports"
                },
                StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string>
        DriverExpectedDeviceClasses =
            new(
                new[]
                {
                    "Display",
                    "Net",
                    "MEDIA",
                    "HDC",
                    "SCSIAdapter",
                    "Bluetooth",
                    "Camera",
                    "Image",
                    "Printer",
                    "Ports",
                    "USB"
                },
                StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string>
        PrivacySensitiveDeviceClasses =
            new(
                new[]
                {
                    "Bluetooth",
                    "WPD"
                },
                StringComparer.OrdinalIgnoreCase);

    public DeviceDriverInformation Analyze()
    {
        var stopwatch =
            Stopwatch.StartNew();

        var information =
            new DeviceDriverInformation
            {
                AnalysisDate =
                    DateTime.Now
            };

        var deadline =
            DateTime.UtcNow.Add(
                AnalysisTimeLimit);

        var deviceResult =
            ReadPlugAndPlayDevices(
                deadline);

        WmiReadResult<RawDriverInformation> driverResult;

        if (DateTime.UtcNow >= deadline)
        {
            driverResult =
                WmiReadResult<RawDriverInformation>
                    .TimedOut();
        }
        else
        {
            driverResult =
                ReadSignedDrivers(
                    deadline);
        }

        if (deviceResult.WasSuccessful)
        {
            ApplyDeviceInformation(
                information,
                deviceResult.Items,
                driverResult.Items,
                driverResult.WasSuccessful);
        }

        ApplyAnalysisStatus(
            information,
            deviceResult,
            driverResult);

        stopwatch.Stop();

        information.AnalysisDurationMilliseconds =
            stopwatch.ElapsedMilliseconds;

        return information;
    }

    private static WmiReadResult<RawDeviceInformation>
        ReadPlugAndPlayDevices(
            DateTime deadline)
    {
        const string queryText =
            """
            SELECT
                PNPDeviceID,
                Name,
                Caption,
                PNPClass,
                Manufacturer,
                ConfigManagerErrorCode,
                Status
            FROM Win32_PnPEntity
            """;

        return ExecuteWmiQuery(
            queryText,
            deadline,
            CreateRawDeviceInformation);
    }

    private static WmiReadResult<RawDriverInformation>
        ReadSignedDrivers(
            DateTime deadline)
    {
        const string queryText =
            """
            SELECT
                DeviceID,
                DeviceName,
                DeviceClass,
                Manufacturer,
                DriverProviderName,
                DriverVersion,
                DriverDate,
                InfName,
                IsSigned
            FROM Win32_PnPSignedDriver
            """;

        return ExecuteWmiQuery(
            queryText,
            deadline,
            CreateRawDriverInformation);
    }

    private static WmiReadResult<T> ExecuteWmiQuery<T>(
        string queryText,
        DateTime deadline,
        Func<ManagementObject, T?> converter)
        where T : class
    {
        if (DateTime.UtcNow >= deadline)
        {
            return WmiReadResult<T>.TimedOut();
        }

        var items =
            new List<T>();

        try
        {
            var remainingTime =
                deadline - DateTime.UtcNow;

            if (remainingTime <= TimeSpan.Zero)
            {
                return WmiReadResult<T>.TimedOut();
            }

            var options =
                new EnumerationOptions
                {
                    ReturnImmediately =
                        false,

                    Rewindable =
                        false,

                    Timeout =
                        remainingTime
                };

            using var searcher =
                new ManagementObjectSearcher(
                    new ManagementScope(
                        @"\\.\root\CIMV2"),
                    new ObjectQuery(
                        queryText),
                    options);

            using var results =
                searcher.Get();

            foreach (ManagementObject result in results)
            {
                using (result)
                {
                    if (DateTime.UtcNow >= deadline)
                    {
                        return WmiReadResult<T>
                            .TimedOut(
                                items);
                    }

                    try
                    {
                        var item =
                            converter(
                                result);

                        if (item is not null)
                        {
                            items.Add(
                                item);
                        }
                    }
                    catch
                    {
                        // Ein einzelner unvollständiger WMI-Datensatz
                        // darf die gesamte Analyse nicht abbrechen.
                    }
                }
            }

            return WmiReadResult<T>
                .Successful(
                    items);
        }
        catch
        {
            if (DateTime.UtcNow >= deadline)
            {
                return WmiReadResult<T>
                    .TimedOut(
                        items);
            }

            return WmiReadResult<T>
                .Failed(
                    items);
        }
    }

    private static RawDeviceInformation?
        CreateRawDeviceInformation(
            ManagementObject managementObject)
    {
        var deviceId =
            GetStringValue(
                managementObject,
                "PNPDeviceID");

        var displayName =
            FirstAvailableValue(
                GetStringValue(
                    managementObject,
                    "Name"),
                GetStringValue(
                    managementObject,
                    "Caption"));

        var deviceClass =
            GetStringValue(
                managementObject,
                "PNPClass");

        var manufacturer =
            GetStringValue(
                managementObject,
                "Manufacturer");

        var configManagerErrorCode =
            GetInt32Value(
                managementObject,
                "ConfigManagerErrorCode");

        var status =
            GetStringValue(
                managementObject,
                "Status");

        if (string.IsNullOrWhiteSpace(deviceId)
            && string.IsNullOrWhiteSpace(displayName)
            && !configManagerErrorCode.HasValue)
        {
            return null;
        }

        return new RawDeviceInformation
        {
            DeviceId =
                deviceId,

            DisplayName =
                displayName,

            DeviceClass =
                deviceClass,

            Manufacturer =
                manufacturer,

            ConfigManagerErrorCode =
                configManagerErrorCode,

            Status =
                status
        };
    }

    private static RawDriverInformation?
        CreateRawDriverInformation(
            ManagementObject managementObject)
    {
        var deviceId =
            GetStringValue(
                managementObject,
                "DeviceID");

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        return new RawDriverInformation
        {
            DeviceId =
                deviceId,

            DeviceName =
                GetStringValue(
                    managementObject,
                    "DeviceName"),

            DeviceClass =
                GetStringValue(
                    managementObject,
                    "DeviceClass"),

            Manufacturer =
                GetStringValue(
                    managementObject,
                    "Manufacturer"),

            DriverProvider =
                GetStringValue(
                    managementObject,
                    "DriverProviderName"),

            DriverVersion =
                GetStringValue(
                    managementObject,
                    "DriverVersion"),

            DriverDate =
                GetWmiDateValue(
                    managementObject,
                    "DriverDate"),

            InfName =
                GetSafeInfName(
                    GetStringValue(
                        managementObject,
                        "InfName")),

            IsSigned =
                GetBooleanValue(
                    managementObject,
                    "IsSigned")
        };
    }

    private static void ApplyDeviceInformation(
        DeviceDriverInformation information,
        IEnumerable<RawDeviceInformation> rawDevices,
        IEnumerable<RawDriverInformation> rawDrivers,
        bool driverSourceAvailable)
    {
        var driverLookup =
            rawDrivers
                .Where(
                    driver =>
                        !string.IsNullOrWhiteSpace(
                            driver.DeviceId))
                .GroupBy(
                    driver =>
                        driver.DeviceId,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group =>
                        group.Key,
                    SelectBestDriver,
                    StringComparer.OrdinalIgnoreCase);

        var uniqueDeviceIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var entries =
            new List<DeviceDriverEntryInformation>();

        var evaluatedDeviceCount =
            0;

        var problemDeviceCount =
            0;

        var missingDriverCount =
            0;

        var disabledDeviceCount =
            0;

        var notEvaluableDeviceCount =
            0;

        foreach (var rawDevice in rawDevices)
        {
            if (!string.IsNullOrWhiteSpace(
                    rawDevice.DeviceId)
                && !uniqueDeviceIds.Add(
                    rawDevice.DeviceId))
            {
                continue;
            }

            evaluatedDeviceCount++;

            RawDriverInformation? driver =
                null;

            if (!string.IsNullOrWhiteSpace(
                    rawDevice.DeviceId))
            {
                driverLookup.TryGetValue(
                    rawDevice.DeviceId,
                    out driver);
            }

            var entry =
                BuildEntry(
                    rawDevice,
                    driver,
                    driverSourceAvailable);

            if (entry.HasWindowsProblem)
            {
                problemDeviceCount++;
            }

            if (entry.HasMissingDriver)
            {
                missingDriverCount++;
            }

            if (entry.IsDisabled)
            {
                disabledDeviceCount++;
            }

            if (entry.IsNotEvaluable)
            {
                notEvaluableDeviceCount++;
            }

            if (ShouldDisplayEntry(
                    rawDevice,
                    entry))
            {
                entries.Add(
                    entry);
            }
        }

        information.EvaluatedDeviceCount =
            evaluatedDeviceCount;

        information.ProblemDeviceCount =
            problemDeviceCount;

        information.MissingDriverCount =
            missingDriverCount;

        information.DisabledDeviceCount =
            disabledDeviceCount;

        information.NotEvaluableDeviceCount =
            notEvaluableDeviceCount;

        information.Entries =
            OrderEntries(
                entries);

        information.AggregatedDeviceCount =
            Math.Max(
                0,
                evaluatedDeviceCount
                - information.Entries.Count);
    }

    private static DeviceDriverEntryInformation BuildEntry(
        RawDeviceInformation rawDevice,
        RawDriverInformation? driver,
        bool driverSourceAvailable)
    {
        var deviceClass =
            NormalizeDeviceClass(
                FirstAvailableValue(
                    rawDevice.DeviceClass,
                    driver?.DeviceClass));

        var displayName =
            CreateSafeDisplayName(
                FirstAvailableValue(
                    rawDevice.DisplayName,
                    driver?.DeviceName),
                deviceClass);

        var manufacturer =
            NormalizeTextValue(
                FirstAvailableValue(
                    rawDevice.Manufacturer,
                    driver?.Manufacturer));

        var operationalState =
            DetermineOperationalState(
                rawDevice.ConfigManagerErrorCode);

        var driverAssignmentState =
            DetermineDriverAssignmentState(
                rawDevice.ConfigManagerErrorCode,
                deviceClass,
                driver,
                driverSourceAvailable);

        var classification =
            DetermineClassification(
                operationalState,
                driverAssignmentState,
                deviceClass,
                driver,
                driverSourceAvailable);

        return new DeviceDriverEntryInformation
        {
            DisplayName =
                displayName,

            DeviceClass =
                GetCustomerFriendlyDeviceClass(
                    deviceClass),

            Manufacturer =
                manufacturer,

            OperationalState =
                operationalState,

            ConfigManagerErrorCode =
                rawDevice.ConfigManagerErrorCode,

            StatusDescription =
                GetStatusDescription(
                    rawDevice.ConfigManagerErrorCode,
                    rawDevice.Status),

            DriverAssignmentState =
                driverAssignmentState,

            DriverProvider =
                NormalizeTextValue(
                    driver?.DriverProvider),

            DriverVersion =
                NormalizeTextValue(
                    driver?.DriverVersion),

            DriverDate =
                driver?.DriverDate,

            InfName =
                NormalizeTextValue(
                    driver?.InfName),

            IsSigned =
                driver?.IsSigned,

            Classification =
                classification
        };
    }

    private static DeviceOperationalState
        DetermineOperationalState(
            int? configManagerErrorCode)
    {
        return configManagerErrorCode switch
        {
            0 =>
                DeviceOperationalState.Working,

            22 =>
                DeviceOperationalState.Disabled,

            null =>
                DeviceOperationalState.Unknown,

            _ =>
                DeviceOperationalState.Problem
        };
    }

    private static DriverAssignmentState
        DetermineDriverAssignmentState(
            int? configManagerErrorCode,
            string deviceClass,
            RawDriverInformation? driver,
            bool driverSourceAvailable)
    {
        if (configManagerErrorCode == 28)
        {
            return DriverAssignmentState.Missing;
        }

        if (driver is not null)
        {
            return DriverAssignmentState.Assigned;
        }

        if (!driverSourceAvailable)
        {
            return DriverAssignmentState.Unknown;
        }

        if (!ExpectsDedicatedDriver(
                deviceClass))
        {
            return DriverAssignmentState.NotRequired;
        }

        return DriverAssignmentState.Unknown;
    }

    private static DeviceDriverClassification
        DetermineClassification(
            DeviceOperationalState operationalState,
            DriverAssignmentState driverAssignmentState,
            string deviceClass,
            RawDriverInformation? driver,
            bool driverSourceAvailable)
    {
        if (driverAssignmentState
            == DriverAssignmentState.Missing)
        {
            return DeviceDriverClassification.MissingDriver;
        }

        if (operationalState
            == DeviceOperationalState.Disabled)
        {
            return DeviceDriverClassification.Disabled;
        }

        if (operationalState
            == DeviceOperationalState.Problem)
        {
            return DeviceDriverClassification.WindowsProblem;
        }

        if (driver is not null
            && driver.IsSigned == false
            && IsRelevantDeviceClass(
                deviceClass))
        {
            return DeviceDriverClassification.UnsignedDriver;
        }

        if (operationalState
            == DeviceOperationalState.Unknown)
        {
            return DeviceDriverClassification.NotEvaluable;
        }

        if (driverSourceAvailable
            && driver is null
            && ExpectsDedicatedDriver(
                deviceClass))
        {
            return DeviceDriverClassification.NotEvaluable;
        }

        return DeviceDriverClassification.Working;
    }

    private static bool ShouldDisplayEntry(
        RawDeviceInformation rawDevice,
        DeviceDriverEntryInformation entry)
    {
        if (entry.OperationalState
            == DeviceOperationalState.Problem)
        {
            return true;
        }

        if (entry.DriverAssignmentState
            == DriverAssignmentState.Missing)
        {
            return true;
        }

        if (entry.Classification
            is DeviceDriverClassification.UnsignedDriver
            or DeviceDriverClassification.NotEvaluable)
        {
            return true;
        }

        if (entry.OperationalState
            == DeviceOperationalState.Disabled)
        {
            return true;
        }

        var deviceClass =
            NormalizeDeviceClass(
                rawDevice.DeviceClass);

        if (IndividuallyRelevantDeviceClasses.Contains(
                deviceClass))
        {
            return true;
        }

        if (deviceClass.Equals(
                "USB",
                StringComparison.OrdinalIgnoreCase))
        {
            return IsRelevantUsbDevice(
                rawDevice.DisplayName);
        }

        return false;
    }

    private static bool IsRelevantUsbDevice(
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        return displayName.Contains(
                   "controller",
                   StringComparison.OrdinalIgnoreCase)
               || displayName.Contains(
                   "host",
                   StringComparison.OrdinalIgnoreCase)
               || displayName.Contains(
                   "hub",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRelevantDeviceClass(
        string deviceClass)
    {
        return IndividuallyRelevantDeviceClasses.Contains(
                   deviceClass)
               || deviceClass.Equals(
                   "USB",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool ExpectsDedicatedDriver(
        string deviceClass)
    {
        return DriverExpectedDeviceClasses.Contains(
            deviceClass);
    }

    private static RawDriverInformation SelectBestDriver(
        IEnumerable<RawDriverInformation> drivers)
    {
        return drivers
            .OrderByDescending(
                GetDriverCompletenessScore)
            .First();
    }

    private static int GetDriverCompletenessScore(
        RawDriverInformation driver)
    {
        var score =
            0;

        if (!string.IsNullOrWhiteSpace(
                driver.DriverProvider))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(
                driver.DriverVersion))
        {
            score++;
        }

        if (driver.DriverDate.HasValue)
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(
                driver.InfName))
        {
            score++;
        }

        if (driver.IsSigned.HasValue)
        {
            score++;
        }

        return score;
    }

    private static List<DeviceDriverEntryInformation>
        OrderEntries(
            IEnumerable<DeviceDriverEntryInformation> entries)
    {
        return entries
            .OrderBy(
                entry =>
                    GetClassificationOrder(
                        entry.Classification))
            .ThenBy(
                entry =>
                    entry.DeviceClass,
                StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(
                entry =>
                    entry.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static int GetClassificationOrder(
        DeviceDriverClassification classification)
    {
        return classification switch
        {
            DeviceDriverClassification.MissingDriver =>
                0,

            DeviceDriverClassification.WindowsProblem =>
                1,

            DeviceDriverClassification.UnsignedDriver =>
                2,

            DeviceDriverClassification.NotEvaluable =>
                3,

            DeviceDriverClassification.Disabled =>
                4,

            DeviceDriverClassification.Working =>
                5,

            _ =>
                6
        };
    }

    private static void ApplyAnalysisStatus(
        DeviceDriverInformation information,
        WmiReadResult<RawDeviceInformation> deviceResult,
        WmiReadResult<RawDriverInformation> driverResult)
    {
        if (deviceResult.WasTimedOut
            || driverResult.WasTimedOut)
        {
            information.AnalysisStatus =
                DeviceDriverAnalysisStatus.TimedOut;

            information.AnalysisMessage =
                "Das Zeitlimit der Geräte- und Treiberanalyse wurde erreicht. "
                + "Die bis dahin zuverlässig ermittelten Informationen "
                + "werden verwendet; das Ergebnis kann unvollständig sein.";

            return;
        }

        if (!deviceResult.WasSuccessful)
        {
            information.AnalysisStatus =
                DeviceDriverAnalysisStatus.NotEvaluable;

            information.AnalysisMessage =
                "Die lokalen Plug-and-Play-Geräte konnten nicht "
                + "zuverlässig ausgewertet werden.";

            return;
        }

        if (!driverResult.WasSuccessful)
        {
            information.AnalysisStatus =
                DeviceDriverAnalysisStatus.PartiallyAnalyzed;

            information.AnalysisMessage =
                "Die lokalen Geräte wurden ermittelt. Die zugehörigen "
                + "Treiberinformationen konnten jedoch nicht vollständig "
                + "ausgewertet werden.";

            return;
        }

        information.AnalysisStatus =
            DeviceDriverAnalysisStatus.Analyzed;

        information.AnalysisMessage =
            information.EvaluatedDeviceCount == 0
                ? "Windows hat keine auswertbaren Plug-and-Play-Geräte geliefert."
                : $"{information.EvaluatedDeviceCount} lokale Geräte "
                  + "wurden vollständig lesend ausgewertet.";
    }

    private static string GetStatusDescription(
        int? configManagerErrorCode,
        string rawStatus)
    {
        return configManagerErrorCode switch
        {
            0 =>
                "Windows meldet für dieses Gerät keinen Gerätefehler.",

            1 =>
                "Das Gerät ist nicht korrekt konfiguriert.",

            3 =>
                "Der Treiber für dieses Gerät ist möglicherweise beschädigt "
                + "oder die Systemressourcen reichen nicht aus.",

            10 =>
                "Das Gerät kann laut Windows nicht gestartet werden.",

            12 =>
                "Für das Gerät stehen nicht genügend freie Systemressourcen zur Verfügung.",

            14 =>
                "Windows meldet, dass für dieses Gerät ein Neustart erforderlich sein kann.",

            18 =>
                "Windows empfiehlt eine erneute Einrichtung des Gerätetreibers.",

            19 =>
                "Die Konfigurationsinformationen für dieses Gerät sind "
                + "unvollständig oder beschädigt.",

            21 =>
                "Windows entfernt dieses Gerät momentan aus dem System.",

            22 =>
                "Das Gerät ist in Windows deaktiviert.",

            24 =>
                "Das Gerät ist nicht vorhanden, funktioniert nicht richtig "
                + "oder besitzt nicht alle erforderlichen Treiber.",

            28 =>
                "Für dieses Gerät ist kein benötigter Treiber installiert.",

            31 =>
                "Das Gerät funktioniert nicht ordnungsgemäß, weil Windows "
                + "die erforderlichen Treiber nicht laden kann.",

            32 =>
                "Der Starttyp des Gerätetreibers ist in Windows deaktiviert.",

            37 =>
                "Windows kann den Gerätetreiber nicht initialisieren.",

            39 =>
                "Windows kann den Gerätetreiber nicht laden. Der Treiber "
                + "kann beschädigt sein oder fehlen.",

            43 =>
                "Windows hat das Gerät angehalten, weil es ein Problem gemeldet hat.",

            45 =>
                "Das Gerät ist momentan nicht mit dem Computer verbunden.",

            47 =>
                "Das Gerät wurde für eine sichere Entfernung vorbereitet "
                + "und ist momentan nicht verfügbar.",

            48 =>
                "Windows verhindert die Ausführung des Gerätetreibers "
                + "aufgrund eines bekannten Problems.",

            52 =>
                "Windows kann die digitale Signatur des erforderlichen "
                + "Gerätetreibers nicht überprüfen.",

            null =>
                CreateUnknownStatusDescription(
                    rawStatus),

            _ =>
                $"Windows meldet für dieses Gerät den Gerätecode "
                + $"{configManagerErrorCode.Value}. Eine manuelle Prüfung "
                + "im Windows-Geräte-Manager ist erforderlich."
        };
    }

    private static string CreateUnknownStatusDescription(
        string rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus))
        {
            return "Der Gerätestatus konnte nicht eindeutig ermittelt werden.";
        }

        return rawStatus.Equals(
            "OK",
            StringComparison.OrdinalIgnoreCase)
                ? "Windows meldet für dieses Gerät keinen allgemeinen Statusfehler."
                : "Windows konnte für dieses Gerät keinen eindeutig "
                  + "übersetzbaren Status liefern.";
    }

    private static string CreateSafeDisplayName(
        string displayName,
        string deviceClass)
    {
        if (PrivacySensitiveDeviceClasses.Contains(
                deviceClass))
        {
            return deviceClass.Equals(
                "Bluetooth",
                StringComparison.OrdinalIgnoreCase)
                    ? "Bluetooth-Gerät"
                    : "Tragbares Gerät";
        }

        var normalizedDisplayName =
            NormalizeTextValue(
                displayName);

        if (!string.IsNullOrWhiteSpace(
                normalizedDisplayName))
        {
            return normalizedDisplayName;
        }

        return GetCustomerFriendlyDeviceClass(
            deviceClass);
    }

    private static string NormalizeDeviceClass(
        string? deviceClass)
    {
        return string.IsNullOrWhiteSpace(deviceClass)
            ? string.Empty
            : deviceClass.Trim();
    }

    private static string GetCustomerFriendlyDeviceClass(
        string deviceClass)
    {
        return deviceClass switch
        {
            "Display" =>
                "Grafikkarte",

            "Net" =>
                "Netzwerkadapter",

            "MEDIA" =>
                "Audio- oder Mediengerät",

            "HDC" =>
                "Speichercontroller",

            "SCSIAdapter" =>
                "SCSI- oder Speicheradapter",

            "Bluetooth" =>
                "Bluetooth",

            "Camera" =>
                "Kamera",

            "Image" =>
                "Bildverarbeitungsgerät",

            "Printer" =>
                "Drucker",

            "Ports" =>
                "Anschluss",

            "USB" =>
                "USB-Gerät",

            "HIDClass" =>
                "Eingabegerät",

            "Keyboard" =>
                "Tastatur",

            "Mouse" =>
                "Maus",

            "System" =>
                "Systemgerät",

            "SoftwareDevice" =>
                "Softwaregerät",

            "WPD" =>
                "Tragbares Gerät",

            _ when string.IsNullOrWhiteSpace(deviceClass) =>
                "Unbekannte Geräteklasse",

            _ =>
                deviceClass
        };
    }

    private static string GetSafeInfName(
        string infName)
    {
        if (string.IsNullOrWhiteSpace(infName))
        {
            return string.Empty;
        }

        try
        {
            return System.IO.Path.GetFileName(
                infName.Trim());
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string NormalizeTextValue(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private static string FirstAvailableValue(
        params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string GetStringValue(
        ManagementBaseObject managementObject,
        string propertyName)
    {
        try
        {
            return managementObject[propertyName]
                ?.ToString()
                ?.Trim()
                ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int? GetInt32Value(
        ManagementBaseObject managementObject,
        string propertyName)
    {
        try
        {
            var value =
                managementObject[propertyName];

            if (value is null)
            {
                return null;
            }

            return Convert.ToInt32(
                value);
        }
        catch
        {
            return null;
        }
    }

    private static bool? GetBooleanValue(
        ManagementBaseObject managementObject,
        string propertyName)
    {
        try
        {
            var value =
                managementObject[propertyName];

            if (value is null)
            {
                return null;
            }

            return Convert.ToBoolean(
                value);
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? GetWmiDateValue(
        ManagementBaseObject managementObject,
        string propertyName)
    {
        var value =
            GetStringValue(
                managementObject,
                propertyName);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return ManagementDateTimeConverter
                .ToDateTime(
                    value);
        }
        catch
        {
            return null;
        }
    }

    private sealed class RawDeviceInformation
    {
        public string DeviceId { get; set; } =
            string.Empty;

        public string DisplayName { get; set; } =
            string.Empty;

        public string DeviceClass { get; set; } =
            string.Empty;

        public string Manufacturer { get; set; } =
            string.Empty;

        public int? ConfigManagerErrorCode { get; set; }

        public string Status { get; set; } =
            string.Empty;
    }

    private sealed class RawDriverInformation
    {
        public string DeviceId { get; set; } =
            string.Empty;

        public string DeviceName { get; set; } =
            string.Empty;

        public string DeviceClass { get; set; } =
            string.Empty;

        public string Manufacturer { get; set; } =
            string.Empty;

        public string DriverProvider { get; set; } =
            string.Empty;

        public string DriverVersion { get; set; } =
            string.Empty;

        public DateTime? DriverDate { get; set; }

        public string InfName { get; set; } =
            string.Empty;

        public bool? IsSigned { get; set; }
    }

    private sealed class WmiReadResult<T>
        where T : class
    {
        public bool WasSuccessful { get; private set; }

        public bool WasTimedOut { get; private set; }

        public List<T> Items { get; private set; } =
            new();

        public static WmiReadResult<T> Successful(
            IEnumerable<T> items)
        {
            return new WmiReadResult<T>
            {
                WasSuccessful =
                    true,

                Items =
                    items.ToList()
            };
        }

        public static WmiReadResult<T> Failed(
            IEnumerable<T>? items = null)
        {
            return new WmiReadResult<T>
            {
                WasSuccessful =
                    false,

                Items =
                    items?.ToList()
                    ?? new List<T>()
            };
        }

        public static WmiReadResult<T> TimedOut(
            IEnumerable<T>? items = null)
        {
            return new WmiReadResult<T>
            {
                WasSuccessful =
                    false,

                WasTimedOut =
                    true,

                Items =
                    items?.ToList()
                    ?? new List<T>()
            };
        }
    }
}