
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Updates;

public class WindowsUpdateInformationProvider :
    IWindowsUpdateInformationProvider
{
    private const int InstallationOperation = 1;
    private const int SucceededResult = 2;
    private const int FailedResult = 4;

    private const int MaximumHistoryEntryCount = 100;
    private const int FailureObservationPeriodInDays = 30;

    private const int MaximumOnlineSearchSeconds = 120;
    private const int MaximumUpdatesToRead = 500;

    private const string OnlineSearchCriteria =
        "IsInstalled=0 and IsHidden=0 and IsAssigned=1";

    private const string SecurityCategoryId =
        "0fa1201d-4330-4fa8-8ae9-b877473b6441";

    private const string CriticalCategoryId =
        "e6cf1350-c01b-414d-a61f-263d14d133b4";

    private const string DefinitionCategoryId =
        "e0789628-ce08-4437-be74-2495b842f43b";

    private static int _onlineSearchRunning;

    public WindowsUpdateInformation GetWindowsUpdateInformation()
    {
        var information = new WindowsUpdateInformation
        {
            IsUpdateSearchPerformed = false,
            IsUpdateSearchSuccessful = false,
            UpdateSearchDetails =
                "Die Windows-Update-Onlineprüfung " +
                "wurde noch nicht durchgeführt."
        };

        ReadWindowsUpdateService(information);
        ReadUpdateHistory(information);
        ReadOnlineUpdates(information);

        return information;
    }

    private static void ReadOnlineUpdates(
        WindowsUpdateInformation information)
    {
        information.IsUpdateSearchPerformed = false;
        information.IsUpdateSearchSuccessful = false;
        information.PendingUpdates = new List<PendingWindowsUpdate>();

        if (Interlocked.CompareExchange(
                ref _onlineSearchRunning,
                1,
                0) != 0)
        {
            information.UpdateSearchDetails =
                "Eine vorherige Windows-Update-Onlineprüfung " +
                "läuft noch. Eine weitere Suche wurde " +
                "nicht parallel gestartet.";

            return;
        }

        Task<OnlineSearchResult> searchTask;

        try
        {
            searchTask = Task.Factory.StartNew(
                () =>
                {
                    try
                    {
                        return SearchOnlineUpdates();
                    }
                    finally
                    {
                        Interlocked.Exchange(
                            ref _onlineSearchRunning,
                            0);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(
                ref _onlineSearchRunning,
                0);

            information.UpdateSearchDetails =
                "Die Windows-Update-Onlineprüfung " +
                "konnte nicht gestartet werden: " +
                exception.Message;

            return;
        }

        information.IsUpdateSearchPerformed = true;

        try
        {
            var completed = searchTask.Wait(
                TimeSpan.FromSeconds(
                    MaximumOnlineSearchSeconds));

            if (!completed)
            {
                information.UpdateSearchDetails =
                    "Die Windows-Update-Onlineprüfung " +
                    "hat das Zeitlimit von " +
                    MaximumOnlineSearchSeconds +
                    " Sekunden überschritten. " +
                    "Der Checkup wird fortgesetzt. " +
                    "Der Windows Update Agent kann " +
                    "seinen Hintergrundvorgang " +
                    "noch abschließen.";

                return;
            }

            var result = searchTask.GetAwaiter().GetResult();

            information.UpdateSearchDate = result.SearchDate;

            information.PendingUpdates = result.Updates;

            information.IsUpdateSearchSuccessful =
                result.IsSuccessful;

            information.UpdateSearchDetails =
                result.Details;
        }
        catch (Exception exception)
        {
            information.IsUpdateSearchSuccessful = false;

            information.UpdateSearchDetails =
                "Die Windows-Update-Onlineprüfung " +
                "ist fehlgeschlagen: " +
                exception.GetBaseException().Message;
        }
    }

    private static OnlineSearchResult SearchOnlineUpdates()
    {
        object? sessionObject = null;
        object? searcherObject = null;
        object? searchResultObject = null;
        object? updatesObject = null;

        var searchDate = DateTime.Now;
        var pendingUpdates = new List<PendingWindowsUpdate>();

        try
        {
            var sessionType = Type.GetTypeFromProgID(
                "Microsoft.Update.Session");

            if (sessionType is null)
            {
                return OnlineSearchResult.Failure(
                    "Der Windows Update Agent ist " +
                    "auf diesem Rechner nicht verfügbar.",
                    searchDate);
            }

            sessionObject = Activator.CreateInstance(
                sessionType);

            if (sessionObject is null)
            {
                return OnlineSearchResult.Failure(
                    "Die Windows-Update-Sitzung " +
                    "konnte nicht erstellt werden.",
                    searchDate);
            }

            dynamic session = sessionObject;

            searcherObject = session.CreateUpdateSearcher();

            dynamic searcher = searcherObject;

            // Den vom Rechner vorgesehenen Updatedienst
            // einschließlich möglicher WSUS-Konfiguration
            // verwenden. Keine fremde Updatequelle erzwingen.
            searcher.Online = true;

            searcher.ClientApplicationID =
                "Weber IT Checkup Assistant";

            // Vorerst nur regulär zugewiesene Updates.
            // Optionale Updates werden in einem späteren
            // Entwicklungsschritt separat erfasst.
            searchResultObject = searcher.Search(
                OnlineSearchCriteria);

            dynamic searchResult = searchResultObject;

            var resultCode = Convert.ToInt32(
                searchResult.ResultCode);

            if (resultCode != SucceededResult)
            {
                return OnlineSearchResult.Failure(
                    "Der Windows Update Agent hat " +
                    "die Suche nicht vollständig " +
                    "erfolgreich abgeschlossen. " +
                    "Ergebniscode: " + resultCode + ".",
                    searchDate);
            }

            updatesObject = searchResult.Updates;

            dynamic updates = updatesObject;

            var updateCount = Convert.ToInt32(
                updates.Count);

            if (updateCount > MaximumUpdatesToRead)
            {
                return OnlineSearchResult.Failure(
                    "Es wurden mehr als " +
                    MaximumUpdatesToRead +
                    " Updates gemeldet. " +
                    "Die Ergebnisliste wurde zum Schutz " +
                    "vor einer unvollständigen Bewertung " +
                    "nicht als erfolgreich eingestuft.",
                    searchDate);
            }

            var unreadableUpdateCount = 0;

            for (var index = 0;
                 index < updateCount;
                 index++)
            {
                object? updateObject = null;

                try
                {
                    updateObject = updates.Item(index);

                    var update = ReadPendingUpdate(
                        updateObject);

                    if (string.IsNullOrWhiteSpace(
                            update.UpdateId))
                    {
                        unreadableUpdateCount++;
                        continue;
                    }

                    pendingUpdates.Add(update);
                }
                catch
                {
                    unreadableUpdateCount++;
                }
                finally
                {
                    ReleaseComObject(updateObject);
                }
            }

            pendingUpdates = pendingUpdates
                .GroupBy(
                    update => update.UpdateId,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(
                        update => update.RevisionNumber ?? 0)
                    .First())
                .OrderBy(
                    update => update.Title,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (unreadableUpdateCount > 0)
            {
                return new OnlineSearchResult
                {
                    IsSuccessful = false,
                    SearchDate = DateTime.Now,
                    Updates = pendingUpdates,
                    Details =
                        "Die Windows-Update-Suche wurde " +
                        "durchgeführt, jedoch konnten " +
                        unreadableUpdateCount +
                        " Updateeinträge nicht vollständig " +
                        "ausgewertet werden. " +
                        "Das Gesamtergebnis ist deshalb " +
                        "nicht zuverlässig bewertbar."
                };
            }

            return new OnlineSearchResult
            {
                IsSuccessful = true,
                SearchDate = DateTime.Now,
                Updates = pendingUpdates,
                Details =
                    pendingUpdates.Count == 0
                        ? "Die Windows-Update-Onlineprüfung " +
                          "wurde erfolgreich durchgeführt. " +
                          "Es wurden keine ausstehenden, " +
                          "regulär zugewiesenen Windows-Updates " +
                          "gefunden. Optionale Updates sind " +
                          "in dieser Prüfung nicht enthalten."
                        : "Die Windows-Update-Onlineprüfung " +
                          "wurde erfolgreich durchgeführt. " +
                          pendingUpdates.Count +
                          " ausstehende, regulär zugewiesene " +
                          "Windows-Updates wurden erkannt. " +
                          "Optionale Updates sind in dieser " +
                          "Prüfung nicht enthalten."
            };
        }
        catch (Exception exception)
        {
            return OnlineSearchResult.Failure(
                "Die Windows-Update-Suche ist " +
                "fehlgeschlagen: " +
                exception.Message,
                searchDate);
        }
        finally
        {
            ReleaseComObject(updatesObject);
            ReleaseComObject(searchResultObject);
            ReleaseComObject(searcherObject);
            ReleaseComObject(sessionObject);
        }
    }

    private static PendingWindowsUpdate ReadPendingUpdate(
        object updateObject)
    {
        dynamic update = updateObject;

        var pending = new PendingWindowsUpdate
        {
            Title = Convert.ToString(update.Title)
                ?.Trim() ?? string.Empty,

            IsHidden = Convert.ToBoolean(
                update.IsHidden),

            IsAssigned = true,

            IsAutoSelected = Convert.ToBoolean(
                update.AutoSelectOnWebSites)
        };

        object? identityObject = null;

        try
        {
            identityObject = update.Identity;

            dynamic identity = identityObject;

            pending.UpdateId = Convert.ToString(
                identity.UpdateID) ?? string.Empty;

            pending.RevisionNumber = Convert.ToInt32(
                identity.RevisionNumber);
        }
        finally
        {
            ReleaseComObject(identityObject);
        }

        try
        {
            pending.MsrcSeverity =
                Convert.ToString(update.MsrcSeverity)
                    ?.Trim() ?? string.Empty;
        }
        catch
        {
            // Nicht jedes Update besitzt eine
            // MSRC-Sicherheitseinstufung.
        }

        try
        {
            var deploymentDate = Convert.ToDateTime(
                update.LastDeploymentChangeTime);

            if (deploymentDate.Year >= 2000
                && deploymentDate
                    <= DateTime.Now.AddDays(7))
            {
                pending.LastDeploymentChangeTime =
                    deploymentDate;
            }
        }
        catch
        {
            // Fehlt ein belastbares Datum, bleibt
            // der Wert ausdrücklich unbekannt.
        }

        try
        {
            pending.IsOptional = Convert.ToBoolean(
                update.BrowseOnly);
        }
        catch
        {
            // BrowseOnly ist nicht auf allen
            // COM-Interfaceversionen verfügbar.
        }

        try
        {
            object? behaviorObject = null;

            try
            {
                behaviorObject =
                    update.InstallationBehavior;

                dynamic behavior = behaviorObject;

                pending.RequiresRestart =
                    Convert.ToInt32(
                        behavior.RebootBehavior) == 1;
            }
            finally
            {
                ReleaseComObject(behaviorObject);
            }
        }
        catch
        {
            // Ein fehlender Neustarthinweis
            // bedeutet nicht, dass ein Neustart
            // definitiv ausgeschlossen ist.
        }

        pending.KbArticleIds =
            ReadKbArticleIds(updateObject);

        pending.Category =
            DetermineUpdateCategory(updateObject);

        if (pending.Category is
            WindowsUpdateCategory.Driver
            or WindowsUpdateCategory.Feature)
        {
            pending.IsOptional = true;
        }

        if (pending.Title.Contains(
                "Preview",
                StringComparison.OrdinalIgnoreCase)
            || pending.Title.Contains(
                "Vorschau",
                StringComparison.OrdinalIgnoreCase))
        {
            pending.IsOptional = true;
        }

        return pending;
    }

    private static List<string> ReadKbArticleIds(
        object updateObject)
    {
        object? idsObject = null;
        var ids = new List<string>();

        try
        {
            dynamic update = updateObject;

            idsObject = update.KBArticleIDs;

            dynamic collection = idsObject;

            var count = Math.Min(
                Convert.ToInt32(collection.Count),
                100);

            for (var index = 0;
                 index < count;
                 index++)
            {
                var id = Convert.ToString(
                    collection.Item(index));

                if (!string.IsNullOrWhiteSpace(id))
                {
                    ids.Add(id.Trim());
                }
            }
        }
        catch
        {
            // KB-Nummern sind hilfreiche Metadaten,
            // aber keine Voraussetzung für die
            // grundsätzliche Updateerkennung.
        }
        finally
        {
            ReleaseComObject(idsObject);
        }

        return ids.Distinct(
            StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static WindowsUpdateCategory DetermineUpdateCategory(
        object updateObject)
    {
        dynamic update = updateObject;

        try
        {
            if (Convert.ToInt32(update.Type) == 2)
            {
                return WindowsUpdateCategory.Driver;
            }
        }
        catch
        {
            // Die Typinformation kann fehlen.
        }

        object? categoriesObject = null;

        var names = new List<string>();
        var ids = new List<string>();

        try
        {
            categoriesObject = update.Categories;

            dynamic categories = categoriesObject;

            var count = Convert.ToInt32(
                categories.Count);

            for (var index = 0;
                 index < count;
                 index++)
            {
                object? categoryObject = null;

                try
                {
                    categoryObject = categories.Item(index);

                    dynamic category = categoryObject;

                    var name = Convert.ToString(
                        category.Name);

                    var id = Convert.ToString(
                        category.CategoryID);

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        names.Add(name);
                    }

                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        ids.Add(id);
                    }
                }
                catch
                {
                    // Einzelne nicht lesbare Kategorien
                    // verhindern die übrige Erkennung nicht.
                }
                finally
                {
                    ReleaseComObject(categoryObject);
                }
            }
        }
        catch
        {
            return WindowsUpdateCategory.Unknown;
        }
        finally
        {
            ReleaseComObject(categoriesObject);
        }

        if (ids.Any(id =>
                id.Equals(
                    SecurityCategoryId,
                    StringComparison.OrdinalIgnoreCase)
                || id.Equals(
                    CriticalCategoryId,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return WindowsUpdateCategory.Security;
        }

        if (ids.Any(id =>
                id.Equals(
                    DefinitionCategoryId,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return WindowsUpdateCategory.Definition;
        }

        if (names.Any(name =>
                name.Contains(
                    "Security",
                    StringComparison.OrdinalIgnoreCase)
                || name.Contains(
                    "Sicherheit",
                    StringComparison.OrdinalIgnoreCase)
                || name.Contains(
                    "Critical",
                    StringComparison.OrdinalIgnoreCase)
                || name.Contains(
                    "Kritisch",
                    StringComparison.OrdinalIgnoreCase)))
        {
            return WindowsUpdateCategory.Security;
        }

        if (names.Any(name =>
                name.Contains(
                    "Definition",
                    StringComparison.OrdinalIgnoreCase)))
        {
            return WindowsUpdateCategory.Definition;
        }

        if (names.Any(name =>
                name.Contains(
                    "Feature",
                    StringComparison.OrdinalIgnoreCase)
                || name.Contains(
                    "Funktionsupdate",
                    StringComparison.OrdinalIgnoreCase)
                || name.Contains(
                    "Upgrade",
                    StringComparison.OrdinalIgnoreCase)))
        {
            return WindowsUpdateCategory.Feature;
        }

        if (names.Count > 0)
        {
            return WindowsUpdateCategory.Quality;
        }

        return WindowsUpdateCategory.Unknown;
    }

    private static void ReadWindowsUpdateService(
        WindowsUpdateInformation information)
    {
        try
        {
            using var searcher =
                new ManagementObjectSearcher(
                    "SELECT State, StartMode " +
                    "FROM Win32_Service " +
                    "WHERE Name = 'wuauserv'");

            foreach (var result in searcher.Get())
            {
                var state =
                    result["State"]
                        ?.ToString()
                        ?.Trim()
                    ?? string.Empty;

                var startMode =
                    result["StartMode"]
                        ?.ToString()
                        ?.Trim()
                    ?? string.Empty;

                information.ServiceState =
                    MapServiceState(
                        state,
                        startMode);

                information.ServiceStartMode =
                    MapServiceStartMode(startMode);

                information.ServiceStatusDetails =
                    BuildServiceStatusDetails(
                        information.ServiceState,
                        information.ServiceStartMode);

                return;
            }

            information.ServiceState =
                WindowsUpdateServiceState.Unknown;

            information.ServiceStatusDetails =
                "Der Windows-Update-Dienst wurde " +
                "nicht gefunden.";
        }
        catch (Exception exception)
        {
            information.ServiceState =
                WindowsUpdateServiceState.Unknown;

            information.ServiceStatusDetails =
                "Der Windows-Update-Dienst konnte " +
                "nicht ausgewertet werden: " +
                exception.Message;
        }
    }

    private static WindowsUpdateServiceState MapServiceState(
        string state,
        string startMode)
    {
        if (startMode.Equals(
                "Disabled",
                StringComparison.OrdinalIgnoreCase))
        {
            return WindowsUpdateServiceState.Disabled;
        }

        if (state.Equals(
                "Running",
                StringComparison.OrdinalIgnoreCase))
        {
            return WindowsUpdateServiceState.Running;
        }

        if (state.Equals(
                "Stopped",
                StringComparison.OrdinalIgnoreCase))
        {
            return WindowsUpdateServiceState.Stopped;
        }

        return WindowsUpdateServiceState.Unknown;
    }

    private static string MapServiceStartMode(
        string startMode)
    {
        if (startMode.Equals(
                "Auto",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Automatisch";
        }

        if (startMode.Equals(
                "Manual",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Manuell beziehungsweise Triggerstart";
        }

        if (startMode.Equals(
                "Disabled",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Deaktiviert";
        }

        return string.IsNullOrWhiteSpace(startMode)
            ? "Nicht ermittelbar"
            : startMode;
    }

    private static string BuildServiceStatusDetails(
        WindowsUpdateServiceState serviceState,
        string startMode)
    {
        return serviceState switch
        {
            WindowsUpdateServiceState.Running =>
                "Der Windows-Update-Dienst wird " +
                $"ausgeführt. Starttyp: {startMode}.",

            WindowsUpdateServiceState.Stopped =>
                "Der Windows-Update-Dienst ist derzeit " +
                "beendet. Das ist bei einem " +
                "bedarfsgesteuerten Dienst nicht " +
                $"automatisch fehlerhaft. Starttyp: {startMode}.",

            WindowsUpdateServiceState.Disabled =>
                "Der Windows-Update-Dienst ist deaktiviert.",

            _ =>
                "Der Zustand des Windows-Update-Dienstes " +
                "konnte nicht eindeutig bestimmt werden. " +
                $"Starttyp: {startMode}."
        };
    }

    private static void ReadUpdateHistory(
        WindowsUpdateInformation information)
    {
        object? updateSession = null;
        object? updateSearcher = null;
        object? historyEntries = null;

        try
        {
            var updateSessionType =
                Type.GetTypeFromProgID(
                    "Microsoft.Update.Session");

            if (updateSessionType is null)
            {
                return;
            }

            updateSession =
                Activator.CreateInstance(
                    updateSessionType);

            if (updateSession is null)
            {
                return;
            }

            dynamic session =
                updateSession;

            updateSearcher =
                session.CreateUpdateSearcher();

            dynamic searcher =
                updateSearcher;

            var totalHistoryCount =
                Convert.ToInt32(
                    searcher.GetTotalHistoryCount());

            var historyCount =
                Math.Min(
                    totalHistoryCount,
                    MaximumHistoryEntryCount);

            if (historyCount <= 0)
            {
                return;
            }

            historyEntries =
                searcher.QueryHistory(
                    0,
                    historyCount);

            dynamic entries =
                historyEntries;

            var successfulInstallations =
                new Dictionary<string, DateTime>(
                    StringComparer.OrdinalIgnoreCase);

            var failedInstallations =
                new List<WindowsUpdateFailure>();

            var failureThreshold =
                DateTime.Now.AddDays(
                    -FailureObservationPeriodInDays);

            for (var index = 0;
                 index < historyCount;
                 index++)
            {
                object? historyEntry = null;

                try
                {
                    historyEntry =
                        entries.Item(index);

                    dynamic entry =
                        historyEntry;

                    var operation =
                        Convert.ToInt32(
                            entry.Operation);

                    if (operation != InstallationOperation)
                    {
                        continue;
                    }

                    var title =
                        Convert.ToString(
                            entry.Title)
                            ?.Trim()
                        ?? string.Empty;

                    var date =
                        Convert.ToDateTime(
                            entry.Date);

                    var resultCode =
                        Convert.ToInt32(
                            entry.ResultCode);

                    if (resultCode == SucceededResult)
                    {
                        if (!information
                                .LastSuccessfulInstallationDate
                                .HasValue
                            || date >
                                information
                                    .LastSuccessfulInstallationDate
                                    .Value)
                        {
                            information
                                .LastSuccessfulInstallationDate =
                                date;
                        }

                        if (!string.IsNullOrWhiteSpace(title)
                            && (!successfulInstallations.TryGetValue(
                                    title,
                                    out DateTime existingSuccessDate)
                                || date > existingSuccessDate))
                        {
                            successfulInstallations[title] =
                                date;
                        }

                        continue;
                    }

                    if (resultCode != FailedResult
                        || date < failureThreshold)
                    {
                        continue;
                    }

                    var errorCode =
                        Convert.ToInt32(
                            entry.HResult);

                    failedInstallations.Add(
                        new WindowsUpdateFailure
                        {
                            Title =
                                string.IsNullOrWhiteSpace(title)
                                    ? "Unbekanntes Windows-Update"
                                    : title,

                            Date = date,

                            ErrorCode = errorCode,

                            ErrorCodeHex =
                                $"0x{unchecked((uint)errorCode):X8}"
                        });
                }
                catch
                {
                    // Ein beschädigter Verlaufseintrag
                    // darf die übrige Analyse nicht verhindern.
                }
                finally
                {
                    ReleaseComObject(historyEntry);
                }
            }

            information.RecentFailures =
                failedInstallations
                    .Where(failure =>
                        !successfulInstallations.TryGetValue(
                            failure.Title,
                            out DateTime laterSuccessDate)
                        || !failure.Date.HasValue
                        || laterSuccessDate
                            <= failure.Date.Value)
                    .OrderByDescending(
                        failure => failure.Date)
                    .Take(5)
                    .ToList();
        }
        catch
        {
            // Ein nicht verfügbarer Updateverlauf
            // darf den Systemscan nicht abbrechen.
        }
        finally
        {
            ReleaseComObject(historyEntries);
            ReleaseComObject(updateSearcher);
            ReleaseComObject(updateSession);
        }
    }

    private static void ReleaseComObject(
        object? comObject)
    {
        if (comObject is null
            || !Marshal.IsComObject(comObject))
        {
            return;
        }

        try
        {
            Marshal.FinalReleaseComObject(
                comObject);
        }
        catch
        {
            // Fehler bei der COM-Freigabe
            // dürfen das Scanergebnis nicht verändern.
        }
    }

    private sealed class OnlineSearchResult
    {
        public bool IsSuccessful { get; init; }

        public DateTime SearchDate { get; init; }

        public string Details { get; init; } =
            string.Empty;

        public List<PendingWindowsUpdate> Updates { get; init; } =
            new();

        public static OnlineSearchResult Failure(
            string details,
            DateTime searchDate)
        {
            return new OnlineSearchResult
            {
                IsSuccessful = false,
                SearchDate = searchDate,
                Details = details
            };
        }
    }
}
