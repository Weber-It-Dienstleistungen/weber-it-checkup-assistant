
#if DEBUG

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.26h
///
/// Gezielte Regressionstests für die
/// Freigabesperre des neuen Executors.
///
/// Sicherheitsregeln:
/// - Keine Administratorrechte.
/// - Freigabesperre vor Tests prüfen.
/// - Keine echten Bereinigungsaktionen.
/// - Keine PowerShell-Ausführung.
/// - Keine Dateisystemänderungen.
/// - Keine produktiven Löschoperationen.
///
/// Die Tests verwenden ausschließlich
/// künstliche Aktionspläne.
/// </summary>
public static class
    WindowsTemporaryFilesCleanupExecutorReleaseGateRegressionTests
{
    private const string ActionCode =
        "action.cleanup.selected-safe-categories";

    private const string TaskCode =
        "task.storage.controlled-cleanup";

    // =============================================
    // ALLE PRÜFUNGEN
    // =============================================

    public static string RunAll()
    {
        EnsureSafeTestEnvironment();

        var results = new List<string>();

        Run(
            "Produktive Freigabesperre ist geschlossen",
            VerifyReleaseGate,
            results);

        Run(
            "Gültiger Plan wird sicher blockiert",
            VerifyApprovedPlanBlocked,
            results);

        Run(
            "Abbruch vor Beginn bleibt erhalten",
            VerifyCancellationBeforeStart,
            results);

        Run(
            "Parallele Ausführung wird verhindert",
            VerifyConcurrentExecutionBlocked,
            results);

        Run(
            "Ungültiger Aktionsplan wird zurückgewiesen",
            VerifyInvalidPlanRejected,
            results);

        return
            $"Alle {results.Count} Executor-"
            + "Sicherheitsprüfungen bestanden."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // TESTUMGEBUNG ABSICHERN
    // =============================================

    private static void EnsureSafeTestEnvironment()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Die Executor-Sicherheitstests "
                + "benötigen Windows.");
        }

        using var identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(identity);

        if (principal.IsInRole(
                WindowsBuiltInRole.Administrator))
        {
            throw new InvalidOperationException(
                "SICHERHEITSSTOPP: Die "
                + "Executor-Regressionstests "
                + "dürfen nicht mit "
                + "Administratorrechten laufen.");
        }

        /*
         * Niemals einen gültigen Plan testen,
         * solange die produktive Freigabe
         * nicht nachweislich gesperrt ist.
         */
        VerifyReleaseGate();
    }

    // =============================================
    // TEST 1: FREIGABESPERRE
    // =============================================

    private static void VerifyReleaseGate()
    {
        var gateField =
            typeof(WindowsTemporaryFilesCleanupExecutor)
                .GetField(
                    "HandleCleanupReleaseApproved",
                    BindingFlags.NonPublic
                    | BindingFlags.Static);

        if (gateField is null
            || gateField.FieldType != typeof(bool)
            || !Equals(
                gateField.GetValue(null),
                false))
        {
            throw new InvalidOperationException(
                "SICHERHEITSSTOPP: Die produktive "
                + "Windows-Temp-Bereinigung "
                + "ist nicht eindeutig gesperrt.");
        }
    }

    // =============================================
    // TEST 2: GÜLTIGER PLAN BLOCKIERT
    // =============================================

    private static void VerifyApprovedPlanBlocked()
    {
        var runner =
            new ForbiddenProcessRunner();

        var coordinator =
            new CheckupTaskActionExecutionCoordinator();

        var executor =
            new WindowsTemporaryFilesCleanupExecutor(
                runner,
                coordinator);

        var plan = CreatePlan();

        var result =
            executor.ExecuteAsync(plan)
                .GetAwaiter()
                .GetResult();

        Require(
            result.PlanId == plan.Id,
            "Die Plan-ID wurde nicht erhalten.");

        Require(
            result.WasBlocked
            && !result.WasStarted
            && !result.WasCancelled,
            "Der gültige Plan wurde "
            + "nicht korrekt blockiert.");

        Require(
            result.CategoryResults.Count == 0
            && result.DeletedFileCount == 0
            && result.DeletedDirectoryCount == 0,
            "Die gesperrte Bereinigung "
            + "meldet unerwartete Änderungen.");

        Require(
            result.ErrorMessage.Contains(
                "Sicherheitsprüfung",
                StringComparison.OrdinalIgnoreCase),
            "Die Freigabesperre wurde "
            + "nicht eindeutig dokumentiert.");

        Require(
            !coordinator.IsExecutionRunning,
            "Die Ausführungssperre wurde "
            + "nicht wieder freigegeben.");

        Require(
            runner.CallCount == 0,
            "Ein externer Prozess "
            + "wurde unerwartet aufgerufen.");
    }

    // =============================================
    // TEST 3: ABBRUCH VOR START
    // =============================================

    private static void VerifyCancellationBeforeStart()
    {
        var runner =
            new ForbiddenProcessRunner();

        var coordinator =
            new CheckupTaskActionExecutionCoordinator();

        var executor =
            new WindowsTemporaryFilesCleanupExecutor(
                runner,
                coordinator);

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        var result =
            executor.ExecuteAsync(
                CreatePlan(),
                cancellation.Token)
                .GetAwaiter()
                .GetResult();

        Require(
            result.WasCancelled
            && !result.WasStarted
            && !result.WasBlocked,
            "Der Abbruch vor Beginn "
            + "wurde nicht korrekt behandelt.");

        Require(
            result.CategoryResults.Count == 0,
            "Ein Abbruch vor Beginn "
            + "darf kein Kategorieergebnis "
            + "mit Dateisystemänderungen erzeugen.");

        Require(
            !coordinator.IsExecutionRunning,
            "Nach dem Abbruch ist "
            + "eine Ausführungssperre aktiv.");

        Require(
            runner.CallCount == 0,
            "Beim Abbruch wurde ein "
            + "externer Prozess gestartet.");
    }

    // =============================================
    // TEST 4: PARALLELE AUSFÜHRUNG
    // =============================================

    private static void VerifyConcurrentExecutionBlocked()
    {
        var runner =
            new ForbiddenProcessRunner();

        var coordinator =
            new CheckupTaskActionExecutionCoordinator();

        var executor =
            new WindowsTemporaryFilesCleanupExecutor(
                runner,
                coordinator);

        using (var existingExecution =
            coordinator.TryBeginExecution(
                "action.test.other",
                "Andere laufende Systemaktion"))
        {
            Require(
                existingExecution is not null,
                "Die simulierte laufende "
                + "Systemaktion konnte nicht "
                + "registriert werden.");

            var result =
                executor.ExecuteAsync(
                    CreatePlan())
                    .GetAwaiter()
                    .GetResult();

            Require(
                result.WasBlocked
                && !result.WasStarted,
                "Die parallele Ausführung "
                + "wurde nicht blockiert.");

            Require(
                coordinator.IsExecutionRunning,
                "Die Sperre der bereits "
                + "laufenden Aktion wurde "
                + "unerwartet aufgehoben.");

            Require(
                runner.CallCount == 0,
                "Bei blockierter Ausführung "
                + "wurde ein Prozess gestartet.");
        }

        Require(
            !coordinator.IsExecutionRunning,
            "Die bestehende Sperre wurde "
            + "nach ihrer Freigabe "
            + "nicht korrekt aufgehoben.");
    }

    // =============================================
    // TEST 5: UNGÜLTIGER AKTIONSPLAN
    // =============================================

    private static void VerifyInvalidPlanRejected()
    {
        var runner =
            new ForbiddenProcessRunner();

        var coordinator =
            new CheckupTaskActionExecutionCoordinator();

        var executor =
            new WindowsTemporaryFilesCleanupExecutor(
                runner,
                coordinator);

        /*
         * Windows-Temp muss ausdrücklich
         * Administratorrechte anfordern.
         *
         * Ein Plan ohne diese Anforderung
         * wird bereits bei der Validierung
         * zurückgewiesen.
         */
        var invalidPlan =
            CreatePlan(
                requiresAdministrator: false);

        var rejected = false;

        try
        {
            executor.ExecuteAsync(
                invalidPlan)
                .GetAwaiter()
                .GetResult();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(
            rejected,
            "Der ungültige Aktionsplan "
            + "wurde nicht zurückgewiesen.");

        Require(
            !coordinator.IsExecutionRunning,
            "Ein ungültiger Plan hat eine "
            + "Ausführungssperre hinterlassen.");

        Require(
            runner.CallCount == 0,
            "Ein ungültiger Plan hat "
            + "einen Prozess gestartet.");
    }

    // =============================================
    // GÜLTIGEN TESTPLAN ERSTELLEN
    // =============================================

    private static CheckupTaskActionPlan CreatePlan(
        bool requiresAdministrator = true)
    {
        return new CheckupTaskActionPlan
        {
            Id = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),

            TaskCode = TaskCode,
            ActionCode = ActionCode,

            ActionTitle =
                "Windows-Temp-Sicherheitstest",

            TargetDescription =
                "Simulierter Windows-Temp-Aktionsplan",

            ExpectedEffect =
                "Keine Dateisystemänderung",

            RiskDescription =
                "Ausführung durch Freigabesperre "
                + "verhindert",

            RequiresAdministrator =
                requiresAdministrator,

            MayRequireRestart = false,

            CleanupCategories =
            [
                new CleanupActionCategory
                {
                    Category =
                        CleanupCategoryType.WindowsTemporaryFiles,

                    Classification =
                        CleanupCategoryClassification.SafePotential,

                    MeasurementStatus =
                        CleanupMeasurementStatus.Measured,

                    Title = "Windows-Temp",

                    TargetAreaDescription =
                        "Simulierter Windows-Temp-Zielbereich",

                    MeasuredFileCount = 0,
                    MeasuredSizeBytes = 0
                }
            ]
        };
    }

    // =============================================
    // EXTERNE PROZESSE VERBIETEN
    // =============================================

    private sealed class ForbiddenProcessRunner :
        IMaintenanceProcessRunner
    {
        public int CallCount { get; private set; }

        public Task<ProcessExecutionResult> RunAsync(
            string fileName,
            IReadOnlyCollection<string> arguments,
            bool requiresAdministrator)
        {
            CallCount++;

            throw new InvalidOperationException(
                "SICHERHEITSSTOPP: Ein externer "
                + "Prozess darf während dieser "
                + "Regressionstests nicht "
                + "gestartet werden.");
        }
    }

    // =============================================
    // TESTAUSFÜHRUNG
    // =============================================

    private static void Run(
        string description,
        Action action,
        List<string> results)
    {
        action();

        results.Add(
            "[OK] " + description);
    }

    private static void Require(
        bool condition,
        string errorMessage)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                errorMessage);
        }
    }
}

#endif
