using System.Management;
using System.Runtime.InteropServices;
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

    public WindowsUpdateInformation GetWindowsUpdateInformation()
    {
        var information =
            new WindowsUpdateInformation
            {
                IsUpdateSearchPerformed = false,

                IsUpdateSearchSuccessful = false,

                UpdateSearchDetails =
                    "Es wurde keine Online-Updatesuche "
                    + "durchgeführt. Ausgewertet wurden "
                    + "ausschließlich lokal vorhandene "
                    + "Windows-Informationen."
            };

        ReadWindowsUpdateService(information);
        ReadUpdateHistory(information);

        return information;
    }

    private static void ReadWindowsUpdateService(
        WindowsUpdateInformation information)
    {
        try
        {
            using var searcher =
                new ManagementObjectSearcher(
                    "SELECT State, StartMode "
                    + "FROM Win32_Service "
                    + "WHERE Name = 'wuauserv'");

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
                "Der Windows-Update-Dienst wurde "
                + "nicht gefunden.";
        }
        catch (Exception exception)
        {
            information.ServiceState =
                WindowsUpdateServiceState.Unknown;

            information.ServiceStatusDetails =
                "Der Windows-Update-Dienst konnte "
                + "nicht ausgewertet werden: "
                + exception.Message;
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
                "Der Windows-Update-Dienst wird "
                + $"ausgeführt. Starttyp: {startMode}.",

            WindowsUpdateServiceState.Stopped =>
                "Der Windows-Update-Dienst ist derzeit "
                + "beendet. Das ist bei einem "
                + "bedarfsgesteuerten Dienst nicht "
                + $"automatisch fehlerhaft. Starttyp: {startMode}.",

            WindowsUpdateServiceState.Disabled =>
                "Der Windows-Update-Dienst ist "
                + "deaktiviert.",

            _ =>
                "Der Zustand des Windows-Update-Dienstes "
                + "konnte nicht eindeutig bestimmt werden. "
                + $"Starttyp: {startMode}."
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
                            || date
                            > information
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

                            Date =
                                date,

                            ErrorCode =
                                errorCode,

                            ErrorCodeHex =
                                $"0x{unchecked((uint)errorCode):X8}"
                        });
                }
                catch
                {
                    // Ein einzelner beschädigter Verlaufseintrag
                    // darf die restliche Auswertung nicht verhindern.
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
            // darf den gesamten Systemscan nicht abbrechen.
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
            // Fehler beim Freigeben eines COM-Objekts
            // sind für das Scanergebnis nicht relevant.
        }
    }
}