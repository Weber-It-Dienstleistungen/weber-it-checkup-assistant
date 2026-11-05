
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Cleanup;

/// <summary>
/// Sprint 55.14.34t
///
/// Führt gezielte Bereinigungs-Zwischenprüfungen
/// während eines laufenden Kundencheckups durch.
///
/// Sicherheitsregeln:
/// - Nur unterstützte Bereinigungskategorien.
/// - Keine Dateilöschung.
/// - Keine Änderung von Systemattributen.
/// - Keine externen Prozesse.
/// - Geräteidentität vor der Messung prüfen.
/// - Ausschließlich lokales Systemvolume.
/// - Bestehende Messlogik wiederverwenden.
/// - Eingangsscan nicht überschreiben.
/// - Abschlussentwurf nicht verändern.
/// - Daten erst nach erfolgreicher Prüfung speichern.
/// - Bei Speicherfehler Änderungen zurücknehmen.
///
/// Ergänzung:
/// - Beginn der Kontrollmessung dokumentieren.
/// - Strukturierte Löschprotokolle nur bei
///   eindeutiger Zuordnung berücksichtigen.
/// - Keine Vermischung verschiedener Checkups.
/// - Keine willkürliche Auswahl bei mehreren
///   möglichen Bereinigungsaktionen.
/// - Löschprotokoll unabhängig kopieren.
/// - Aktions-ID, Plan-ID sowie Ausführungsbeginn
///   und Ausführungsende dauerhaft referenzieren.
/// - Ohne eindeutiges Protokoll bleibt die
///   Zwischenprüfung weiterhin möglich.
///
/// Jede Zwischenprüfung erfasst genau
/// eine Bereinigungskategorie.
/// </summary>
public sealed class CleanupPotentialIntermediateScanService
{
    private static readonly TimeSpan CategoryAnalysisTimeLimit =
        TimeSpan.FromMinutes(2);

    private const string ExecutionActionCode =
        "control.cleanup.intermediate-scan";

    private const string ExecutionActionTitle =
        "Bereinigungspotenzial erneut prüfen";

    private readonly IDeviceInformationScanner
        _deviceInformationScanner;

    private readonly IDeviceIdentityService
        _deviceIdentityService;

    private readonly ICheckupTaskActionExecutionCoordinator
        _executionCoordinator;

    private readonly ICustomerService
        _customerService;

    private readonly CleanupDirectoryMeasurer
        _directoryMeasurer;

    private readonly BrowserCleanupCategoryProvider
        _browserCategoryProvider;

    // =============================================
    // KONSTRUKTOR
    // =============================================

    public CleanupPotentialIntermediateScanService(
        IDeviceInformationScanner deviceInformationScanner,
        IDeviceIdentityService deviceIdentityService,
        ICheckupTaskActionExecutionCoordinator
            executionCoordinator,
        ICustomerService customerService)
    {
        ArgumentNullException.ThrowIfNull(
            deviceInformationScanner);

        ArgumentNullException.ThrowIfNull(
            deviceIdentityService);

        ArgumentNullException.ThrowIfNull(
            executionCoordinator);

        ArgumentNullException.ThrowIfNull(
            customerService);

        _deviceInformationScanner =
            deviceInformationScanner;

        _deviceIdentityService =
            deviceIdentityService;

        _executionCoordinator =
            executionCoordinator;

        _customerService =
            customerService;

        _directoryMeasurer =
            new CleanupDirectoryMeasurer();

        _browserCategoryProvider =
            new BrowserCleanupCategoryProvider(
                _directoryMeasurer);
    }

    // =============================================
    // ZWISCHENPRÜFUNG STARTEN
    // =============================================

    public async Task<CleanupPotentialIntermediateScan>
        RunAsync(
            Customer customer,
            CustomerDevice device,
            CleanupCategoryType category,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(device);

        ValidateSupportedCategory(
            category);

        ValidateCustomerDevice(
            customer,
            device);

        var session =
            device.CheckupSession;

        var currentVisit =
            GetEditableVisit(session);

        ValidateCategoryInEntranceScan(
            currentVisit,
            category);

        cancellationToken.ThrowIfCancellationRequested();

        // =========================================
        // PARALLELE SYSTEMAKTIONEN VERHINDERN
        // =========================================

        using var executionLease =
            _executionCoordinator.TryBeginExecution(
                ExecutionActionCode,
                ExecutionActionTitle);

        if (executionLease is null)
        {
            var activeAction =
                _executionCoordinator.ActiveActionTitle;

            throw new InvalidOperationException(
                "Die Bereinigungs-Zwischenprüfung "
                + "kann nicht gestartet werden, "
                + "weil bereits eine andere "
                + "Systemaktion läuft."
                + (string.IsNullOrWhiteSpace(activeAction)
                    ? string.Empty
                    : Environment.NewLine
                      + Environment.NewLine
                      + "Aktive Aktion: "
                      + activeAction));
        }

        // =========================================
        // GERÄTEIDENTITÄT PRÜFEN
        // =========================================

        var deviceScan =
            await Task.Run(
                () => _deviceInformationScanner.Scan());

        cancellationToken.ThrowIfCancellationRequested();

        if (!deviceScan.IsSuccessful
            || deviceScan.Data is null)
        {
            throw new InvalidOperationException(
                "Die Identität des angeschlossenen "
                + "Computers konnte nicht "
                + "zuverlässig ermittelt werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Es wurde keine Bereinigungs-"
                + "Zwischenprüfung durchgeführt.");
        }

        var matchingDevice =
            _deviceIdentityService.FindMatchingDevice(
                customer.Devices,
                deviceScan.Data);

        if (matchingDevice is null
            || matchingDevice.Id != device.Id)
        {
            throw new InvalidOperationException(
                "Der aktuell untersuchte Computer "
                + "stimmt nicht eindeutig mit dem "
                + "ausgewählten Kundengerät überein."
                + Environment.NewLine
                + Environment.NewLine
                + "Es wurden keine neuen Messwerte "
                + "gespeichert.");
        }

        // =========================================
        // SYSTEMVOLUME VALIDIEREN
        // =========================================

        var systemVolumeRoot =
            GetValidatedSystemVolumeRoot(
                currentVisit);

        cancellationToken.ThrowIfCancellationRequested();

        // =========================================
        // MESSBEGINN FESTHALTEN
        // =========================================

        /*
         * Der Zeitpunkt wird unmittelbar
         * vor der eigentlichen Kategorieanalyse
         * erfasst.
         *
         * Eine Bereinigungsaktion darf nur
         * zugeordnet werden, wenn sie bereits
         * vor diesem Zeitpunkt abgeschlossen war.
         *
         * Der Zeitpunkt darf ausdrücklich nicht
         * erst nach der Messung ermittelt werden.
         */
        var measurementStartedAt =
            DateTimeOffset.Now;

        // =========================================
        // AUSGEWÄHLTE KATEGORIE NEU MESSEN
        // =========================================

        /*
         * Ausschließlich die angeforderte
         * Bereinigungskategorie wird analysiert.
         *
         * Der bestehende Messdienst ist
         * ausschließlich lesend.
         *
         * Die Messung erfolgt außerhalb
         * des WPF-UI-Threads.
         *
         * Die vorhandenen Messverfahren
         * besitzen eigene Zeitlimits.
         */
        var afterMeasurement =
            await Task.Run(
                () => MeasureSelectedCategory(
                    category,
                    systemVolumeRoot));

        /*
         * Der Abschlusszeitpunkt beschreibt
         * die tatsächlich beendete Neumessung.
         *
         * Er wird unabhängig vom späteren
         * Datenbankschreibvorgang festgehalten.
         */
        var afterMeasuredAt =
            DateTime.Now;

        cancellationToken.ThrowIfCancellationRequested();

        if (afterMeasurement is null
            || afterMeasurement.Category != category)
        {
            throw new InvalidOperationException(
                "Die erneute Bereinigungsanalyse "
                + "hat kein eindeutig zuordenbares "
                + "Kategorieergebnis geliefert.");
        }

        // =========================================
        // KUNDEN- UND GERÄTEKONTEXT ERNEUT PRÜFEN
        // =========================================

        ValidateCustomerDevice(
            customer,
            device);

        if (!ReferenceEquals(
                device.CheckupSession,
                session))
        {
            throw new InvalidOperationException(
                "Der Gerätestand hat sich während "
                + "der Bereinigungsanalyse geändert."
                + Environment.NewLine
                + Environment.NewLine
                + "Das Ergebnis wurde nicht übernommen.");
        }

        var activeVisit =
            GetEditableVisit(session);

        if (!ReferenceEquals(
                activeVisit,
                currentVisit))
        {
            throw new InvalidOperationException(
                "Der laufende Kundencheckup hat "
                + "sich während der Analyse geändert."
                + Environment.NewLine
                + Environment.NewLine
                + "Die Messung wurde nicht gespeichert.");
        }

        ValidateCategoryInEntranceScan(
            activeVisit,
            category);

        cancellationToken.ThrowIfCancellationRequested();

        // =========================================
        // EINDEUTIGES LÖSCHPROTOKOLL SUCHEN
        // =========================================

        /*
         * Der Resolver arbeitet ausschließlich
         * auf bereits gespeicherten strukturierten
         * Aufgabenprotokollen.
         *
         * Er verändert weder die Aufgabenliste
         * noch die eigentliche Bereinigung.
         *
         * Nur genau eine passende Aktion darf
         * als Nachweis übernommen werden.
         *
         * Voraussetzungen:
         *
         * - Derselbe Kundencheckup.
         * - Dieselbe Bereinigungskategorie.
         * - Gültiger technischer Aktionsstatus.
         * - Vollständiger Ausführungszeitraum
         *   vor Beginn dieser Kontrollmessung.
         * - Aktion nach der vorherigen
         *   maßgeblichen Messgrenze.
         *
         * Bei fehlender oder mehrdeutiger
         * Zuordnung liefert der Resolver null.
         *
         * Die Kontrollmessung bleibt dadurch
         * unabhängig vom Vorliegen einer
         * ausgeführten Löschaktion.
         */
        var correlation =
            CleanupExecutionCorrelationResolver.FindUniqueMatch(
                session,
                currentVisit,
                category,
                measurementStartedAt);

        cancellationToken.ThrowIfCancellationRequested();

        // =========================================
        // UNABHÄNGIGE VORGANGSKOPIE
        // =========================================

        var updatedVisit =
            currentVisit.CreateIndependentCopy();

        /*
         * Die ursprüngliche Eingangsmessung
         * wird innerhalb des Vorgangsmodells
         * direkt aus BeforeCheckup gelesen.
         *
         * Der neue Messwert wird unabhängig
         * kopiert und dokumentiert.
         *
         * Liegt ein eindeutiges strukturiertes
         * Löschprotokoll vor, wird dessen
         * Kategorieergebnis ebenfalls als
         * unabhängige Kopie übernommen.
         *
         * Die technische Löschmenge und
         * die gemessene Bestandsänderung
         * bleiben zwei getrennte Sachverhalte.
         */

        // =========================================
        // SPRINT 55.14.34t
        // VOLLSTÄNDIGE REFERENZEN ÜBERGEBEN
        // =========================================

        /*
         * Die beiden Kennungen und die beiden
         * Ausführungszeitpunkte stammen
         * ausschließlich aus dem eindeutig
         * gefundenen strukturierten Protokoll.
         *
         * Bei correlation == null werden
         * sämtliche optionalen Werte null.
         *
         * Das Datenmodell validiert die
         * Vollständigkeit, bevor es den
         * neuen Zwischenbefund übernimmt.
         *
         * Es werden keine IDs erfunden und
         * keine Zeitpunkte nachträglich
         * aus der Messung abgeleitet.
         */
        var recordedScan =
            updatedVisit.RecordCleanupPotentialIntermediateScan(
                afterMeasurement,
                afterMeasuredAt,
                recordedExecution:
                    correlation?.CategoryResult,
                recordedActionResultId:
                    correlation?.ActionResultId,
                recordedPlanId:
                    correlation?.PlanId,
                recordedActionStartedAt:
                    correlation?.StartedAt,
                recordedActionFinishedAt:
                    correlation?.FinishedAt);

        // =========================================
        // BESTEHENDEN DATENBANKWEG VERWENDEN
        // =========================================

        var previousVisits =
            session.CustomerCheckupVisits;

        var previousUpdatedAt =
            device.UpdatedAt;

        session.CustomerCheckupVisits =
            previousVisits
                .Select(visit =>
                    ReferenceEquals(
                        visit,
                        currentVisit)
                        ? updatedVisit
                        : visit)
                .ToList();

        device.UpdatedAt =
            DateTime.Now;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var saved =
                _customerService.UpdateCustomerDevice(
                    customer.Id,
                    device);

            if (!saved)
            {
                throw new InvalidOperationException(
                    "Der Kunde oder das ausgewählte "
                    + "Gerät ist in der Datenbank "
                    + "nicht mehr vorhanden."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Die Zwischenprüfung konnte "
                    + "nicht gespeichert werden.");
            }
        }
        catch
        {
            /*
             * Bei einem fehlgeschlagenen
             * Datenbankschreibvorgang wird der
             * vorherige In-Memory-Zustand
             * wiederhergestellt.
             *
             * Ein bereits begonnener externer
             * Schreibvorgang kann dadurch
             * allerdings nicht rückgängig
             * gemacht werden.
             */
            session.CustomerCheckupVisits =
                previousVisits;

            device.UpdatedAt =
                previousUpdatedAt;

            throw;
        }

        // =========================================
        // ERGEBNIS ZURÜCKGEBEN
        // =========================================

        return recordedScan;
    }

    // =============================================
    // KATEGORIE GEZIELT MESSEN
    // =============================================

    private CleanupCategoryResult MeasureSelectedCategory(
        CleanupCategoryType category,
        string systemVolumeRoot)
    {
        var deadline =
            DateTime.UtcNow.Add(
                CategoryAnalysisTimeLimit);

        /*
         * Browsercache besitzt bereits
         * eine eigene, umfangreiche Logik
         * zur Erkennung unterstützter
         * Browserprofile.
         *
         * Diese verwenden wir unverändert.
         */
        if (category == CleanupCategoryType.BrowserCache)
        {
            return _browserCategoryProvider.Analyze(
                systemVolumeRoot,
                CategoryAnalysisTimeLimit);
        }

        string path;
        string description;
        SearchOption searchOption;
        string filePattern;

        switch (category)
        {
            // =====================================
            // BENUTZERTEMPORÄRDATEIEN
            // =====================================

            case CleanupCategoryType.UserTemporaryFiles:
                path =
                    Path.GetTempPath();

                description =
                    "Temporäre Dateien des aktuell "
                    + "angemeldeten Benutzers";

                searchOption =
                    SearchOption.AllDirectories;

                filePattern =
                    "*";

                break;

            // =====================================
            // WINDOWS-TEMP
            // =====================================

            case CleanupCategoryType.WindowsTemporaryFiles:
                var windowsDirectory =
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Windows);

                path =
                    string.IsNullOrWhiteSpace(windowsDirectory)
                        ? string.Empty
                        : Path.Combine(
                            windowsDirectory,
                            "Temp");

                description =
                    "Temporäre Dateien von Windows";

                searchOption =
                    SearchOption.AllDirectories;

                filePattern =
                    "*";

                break;

            // =====================================
            // DIRECTX-SHADERCACHE
            // =====================================

            case CleanupCategoryType.DirectXShaderCache:
                var localApplicationData =
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData);

                path =
                    string.IsNullOrWhiteSpace(localApplicationData)
                        ? string.Empty
                        : Path.Combine(
                            localApplicationData,
                            "D3DSCache");

                description =
                    "DirectX-Shadercache";

                searchOption =
                    SearchOption.AllDirectories;

                filePattern =
                    "*";

                break;

            // =====================================
            // VORSCHAUBILDCACHE
            // =====================================

            case CleanupCategoryType.ThumbnailCache:
                var explorerLocalData =
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData);

                path =
                    string.IsNullOrWhiteSpace(explorerLocalData)
                        ? string.Empty
                        : Path.Combine(
                            explorerLocalData,
                            "Microsoft",
                            "Windows",
                            "Explorer");

                description =
                    "Windows-Vorschaubildcache";

                searchOption =
                    SearchOption.TopDirectoryOnly;

                filePattern =
                    "thumbcache_*.db";

                break;

            default:
                throw new InvalidOperationException(
                    "Die ausgewählte Kategorie "
                    + "ist nicht für die gezielte "
                    + "Zwischenprüfung freigegeben.");
        }

        // =========================================
        // BEWÄHRTES MESSVERFAHREN
        // =========================================

        return _directoryMeasurer.MeasureDirectory(
            category,
            CleanupCategoryClassification.SafePotential,
            description,
            path,
            systemVolumeRoot,
            deadline,
            searchOption,
            filePattern);
    }

    // =============================================
    // UNTERSTÜTZTE KATEGORIEN
    // =============================================

    private static void ValidateSupportedCategory(
        CleanupCategoryType category)
    {
        if (category is
            CleanupCategoryType.UserTemporaryFiles
            or CleanupCategoryType.WindowsTemporaryFiles
            or CleanupCategoryType.DirectXShaderCache
            or CleanupCategoryType.ThumbnailCache
            or CleanupCategoryType.BrowserCache)
        {
            return;
        }

        throw new InvalidOperationException(
            "Für diese Bereinigungskategorie "
            + "ist derzeit keine gezielte "
            + "Zwischenprüfung verfügbar.");
    }

    // =============================================
    // GESPEICHERTE EINGANGSMESSUNG PRÜFEN
    // =============================================

    private static void ValidateCategoryInEntranceScan(
        CustomerCheckupVisit visit,
        CleanupCategoryType category)
    {
        var information =
            visit.BeforeCheckup?.CleanupPotentialInformation;

        if (information is null
            || !information.HasAnalysis
            || information.Categories is null
            || information.Categories.Count(
                item => item.Category == category) != 1)
        {
            throw new InvalidOperationException(
                "Die ausgewählte Kategorie "
                + "ist im ursprünglichen Eingangsscan "
                + "nicht eindeutig vorhanden.");
        }
    }

    // =============================================
    // GESPEICHERTES SYSTEMVOLUME PRÜFEN
    // =============================================

    private static string GetValidatedSystemVolumeRoot(
        CustomerCheckupVisit visit)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Die Bereinigungs-Zwischenprüfung "
                + "benötigt Windows.");
        }

        var storage =
            visit.BeforeCheckup?.StorageInformation;

        var systemVolume =
            storage?.Volumes.FirstOrDefault(
                volume =>
                    volume.IsSystemVolume
                    && volume.IsReady);

        if (systemVolume is null
            || string.IsNullOrWhiteSpace(
                systemVolume.DriveLetter))
        {
            throw new InvalidOperationException(
                "Der Eingangsscan enthält kein "
                + "eindeutig nutzbares Systemvolume.");
        }

        var storedDriveLetter =
            systemVolume.DriveLetter.Trim();

        /*
         * Nur klassische lokale Laufwerks-
         * kennungen werden akzeptiert.
         *
         * Beispielsweise C: oder C:\.
         *
         * Keine UNC-Pfade, keine relativen
         * Verzeichnisse und keine frei
         * übergebenen Zielpfade.
         */
        if (storedDriveLetter.Length < 2
            || !char.IsLetter(
                storedDriveLetter[0])
            || storedDriveLetter[1] != ':'
            || (storedDriveLetter.Length > 2
                && storedDriveLetter
                    .Substring(2)
                    .Trim('\\', '/')
                    .Length > 0))
        {
            throw new InvalidOperationException(
                "Die gespeicherte Systemlaufwerkskennung "
                + "ist nicht zulässig.");
        }

        var expectedRoot =
            char.ToUpperInvariant(
                storedDriveLetter[0])
            + @":\";

        var currentWindowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        if (string.IsNullOrWhiteSpace(
                currentWindowsDirectory))
        {
            throw new InvalidOperationException(
                "Das aktuelle Windows-Verzeichnis "
                + "konnte nicht bestimmt werden.");
        }

        var actualRoot =
            Path.GetPathRoot(
                Path.GetFullPath(
                    currentWindowsDirectory));

        if (!string.Equals(
                expectedRoot,
                actualRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Das Systemlaufwerk des gespeicherten "
                + "Eingangsscans stimmt nicht mit dem "
                + "aktuellen Windows-Systemlaufwerk überein."
                + Environment.NewLine
                + Environment.NewLine
                + "Die Zwischenprüfung wird zum Schutz "
                + "des Kundencheckups nicht ausgeführt.");
        }

        return expectedRoot;
    }

    // =============================================
    // KUNDEN- UND GERÄTEZUORDNUNG
    // =============================================

    private static void ValidateCustomerDevice(
        Customer customer,
        CustomerDevice device)
    {
        if (customer.Id == Guid.Empty
            || device.Id == Guid.Empty
            || !customer.Devices.Any(
                storedDevice =>
                    ReferenceEquals(
                        storedDevice,
                        device)))
        {
            throw new InvalidOperationException(
                "Das ausgewählte Gerät konnte "
                + "dem Kunden nicht eindeutig "
                + "zugeordnet werden.");
        }
    }

    // =============================================
    // BEARBEITBAREN KUNDENCHECKUP ERMITTELN
    // =============================================

    private static CustomerCheckupVisit GetEditableVisit(
        CheckupSession session)
    {
        var visit =
            session.CurrentCustomerCheckupVisit;

        if (visit is null
            || !visit.IsInProgress)
        {
            throw new InvalidOperationException(
                "Für dieses Gerät ist derzeit "
                + "kein bearbeitbarer Kundencheckup "
                + "vorhanden.");
        }

        if (visit.IsCompletionPrepared)
        {
            throw new InvalidOperationException(
                "Für diesen Kundencheckup wurde "
                + "bereits eine Abschlusskontrolle "
                + "vorbereitet."
                + Environment.NewLine
                + Environment.NewLine
                + "Weitere Bereinigungs-Zwischenprüfungen "
                + "sind deshalb nicht zulässig.");
        }

        return visit;
    }
}
