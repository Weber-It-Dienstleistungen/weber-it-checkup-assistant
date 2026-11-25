#if DEBUG
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Updates;

public static class ManualProgramUpdateVerificationRegressionTests
{
    public static string RunAll()
    {
        var results = new List<string>();
        void Run(string title, Action test)
        {
            try { test(); results.Add("OK: " + title); }
            catch (Exception exception) { throw new InvalidOperationException(title, exception); }
        }

        Run("Manuelle Zielversion mit Pruefquelle wird separat dokumentiert", () =>
        {
            var (session, visit, update) = Scenario();
            var result = ManualProgramUpdateVerificationService.CreateResult(session, visit,
                update, "2.0", "Info-Dialog des Programms", DateTimeOffset.Now);
            Require(result.ManualProgramUpdateVerification?.VisitId == visit.Id
                && result.ManualProgramUpdateVerification.ObservedVersion == "2.0"
                && result.Status == CheckupTaskActionStatus.Successful);
            Require(session.ProgramUpdateInformation.AvailableUpdateCount == 1
                && session.TaskList.Tasks[0].Status == CheckupTaskStatus.Open);
            session.TaskList.AddTaskActionResult(session.TaskList.Tasks[0], result);
            var restored = CheckupSnapshot.Capture(session).RestoreAsSession();
            Require(restored.TaskList.Tasks[0].ActionResults[0]
                .ManualProgramUpdateVerification?.Evidence == "Info-Dialog des Programms");
            Require(ManualProgramUpdateVerificationService.GetCandidates(session, visit).Count == 0);
        });
        Run("Abweichende Version oder fehlender Nachweis wird abgewiesen", () =>
        {
            var (session, visit, update) = Scenario();
            Reject(() => ManualProgramUpdateVerificationService.CreateResult(session, visit,
                update, "1.0", "Info", DateTimeOffset.Now));
            Reject(() => ManualProgramUpdateVerificationService.CreateResult(session, visit,
                update, "2.0", "", DateTimeOffset.Now));
        });
        Run("Fremdes Paket und fremder Vorgang werden abgewiesen", () =>
        {
            var (session, visit, update) = Scenario();
            Reject(() => ManualProgramUpdateVerificationService.CreateResult(session, visit,
                new AvailableProgramUpdate { PackageId = "Other.App", Source = "winget", InstalledVersion = "1.0", AvailableVersion = "2.0" },
                "2.0", "Info", DateTimeOffset.Now));
            Require(ManualProgramUpdateVerificationService.GetCandidates(session, new CustomerCheckupVisit()).Count == 0);
        });
        Run("Unvollstaendiger Eingangsscan erlaubt keine Versionsbestaetigung", () =>
        {
            var (session, visit, _) = Scenario();
            visit.BeforeCheckup.ProgramUpdateInformation.IsResultTruncated = true;
            Require(ManualProgramUpdateVerificationService.GetCandidates(session, visit).Count == 0);
        });
        Run("WinGet-Anzeige unterscheidet Nullbefund von fehlender Auswertung", () =>
        {
            var info = new ProgramUpdateInformation();
            Require(info.EmptyUpdateListText.Contains("Keine auswertbare"));
            info.IsWingetAvailable = true; info.IsAnalysisPerformed = true; info.IsAnalysisSuccessful = true;
            Require(info.EmptyUpdateListText.Contains("Keine ausstehenden") && info.WingetAvailabilityText == "Ja");
            info.IsAnalysisSuccessful = false;
            Require(info.EmptyUpdateListText.Contains("Keine auswertbare"));
        });
        return $"Alle {results.Count} manuellen Versionspruefungs-Regressionstests erfolgreich."
            + Environment.NewLine + string.Join(Environment.NewLine, results);
    }

    private static (CheckupSession, CustomerCheckupVisit, AvailableProgramUpdate) Scenario()
    {
        var update = new AvailableProgramUpdate
        { PackageId = "Example.App", Source = "winget", Name = "Test", InstalledVersion = "1.0", AvailableVersion = "2.0" };
        var session = new CheckupSession
        {
            ScanDate = DateTime.Now.AddMinutes(-5),
            ProgramUpdateInformation = new ProgramUpdateInformation
            {
                IsWingetAvailable = true, IsAnalysisPerformed = true, IsAnalysisSuccessful = true,
                AnalysisDate = DateTime.Now.AddMinutes(-5), AvailableUpdateCount = 1,
                AvailableUpdates = new List<AvailableProgramUpdate> { update }
            },
            TaskList = new CheckupTaskList { Tasks = new List<CheckupTask>
            { new() { TaskCode = ManualProgramUpdateVerificationService.TaskCode } } }
        };
        var visit = CustomerCheckupVisit.Start(session);
        session.CustomerCheckupVisits.Add(visit);
        return (session, visit, update);
    }
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Unzulaessige Bestaetigung wurde angenommen.");
    }
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Erwartung nicht erfuellt.");
    }
}
#endif
