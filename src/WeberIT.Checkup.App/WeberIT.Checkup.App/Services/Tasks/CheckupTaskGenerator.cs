using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

public static class CheckupTaskGenerator
{
    private const int CurrentTaskListVersion =
        2;

    public static CheckupTaskList Generate(
        IReadOnlyCollection<CheckupFinding> findings)
    {
        var createdAt =
            DateTime.Now;

        var taskCandidates =
            findings
                .Select(
                    finding =>
                        CreateCandidate(finding))
                .Where(
                    candidate =>
                        candidate is not null)
                .Cast<TaskCandidate>()
                .ToList();

        var tasks =
            taskCandidates
                .GroupBy(
                    candidate =>
                        BuildGroupKey(candidate),
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                        CreateTask(
                            group.ToList(),
                            createdAt))
                .OrderByDescending(
                    task =>
                        GetPriorityOrder(
                            task.Priority))
                .ThenBy(
                    task =>
                        task.Category)
                .ThenBy(
                    task =>
                        task.Title,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        return new CheckupTaskList
        {
            TaskListVersion =
                CurrentTaskListVersion,

            CreatedAt =
                createdAt,

            Tasks =
                tasks
        };
    }

    private static TaskCandidate? CreateCandidate(
        CheckupFinding finding)
    {
        var template =
            GetTaskTemplate(
                finding.Code);

        if (template is null)
        {
            return null;
        }

        return new TaskCandidate(
            finding,
            template);
    }

    private static TaskTemplate? GetTaskTemplate(
        string findingCode)
    {
        return findingCode switch
        {
            "system.security.antivirus-missing-disabled"
                or "system.security.antivirus-disabled" =>
                    new TaskTemplate(
                        "task.security.antivirus-protection",
                        "Virenschutz herstellen",
                        CheckupTaskPriority.Required,
                        CheckupTaskCategory.Security),

            "system.security.antivirus-not-registered" =>
                new TaskTemplate(
                    "task.security.antivirus-protection",
                    "Virenschutzregistrierung prüfen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.Security),

            "system.security.multiple-antivirus-products" =>
                new TaskTemplate(
                    "task.security.antivirus-registration",
                    "Registrierte Virenschutzprodukte prüfen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.Security),

            "system.security.active-firewall-disabled" =>
                new TaskTemplate(
                    "task.security.firewall-configuration",
                    "Firewallkonfiguration herstellen",
                    CheckupTaskPriority.Required,
                    CheckupTaskCategory.Security),

            "system.security.inactive-firewall-disabled" =>
                new TaskTemplate(
                    "task.security.firewall-configuration",
                    "Firewallkonfiguration prüfen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.Security),

            "system.security.uac-disabled" =>
                new TaskTemplate(
                    "task.security.user-account-control",
                    "Benutzerkontensteuerung aktivieren prüfen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.Security),

            "system.security.security-center-disabled" =>
                new TaskTemplate(
                    "task.security.security-center",
                    "Windows-Sicherheitscenter prüfen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.Security),

            "system.security.drive-encryption-incomplete" =>
                new TaskTemplate(
                    "task.security.drive-encryption",
                    "Laufwerksverschlüsselung prüfen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.Security),

            "system.security.mobile-drive-not-encrypted" =>
                new TaskTemplate(
                    "task.security.drive-encryption",
                    "Laufwerksverschlüsselung prüfen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.Security),

            "system.security.stationary-drive-not-encrypted" =>
                new TaskTemplate(
                    "task.security.drive-encryption",
                    "Laufwerksverschlüsselung erwägen",
                    CheckupTaskPriority.Optional,
                    CheckupTaskCategory.Security),

            "system.security.secure-boot-disabled" =>
                new TaskTemplate(
                    "task.security.secure-boot",
                    "Secure Boot prüfen",
                    CheckupTaskPriority.Optional,
                    CheckupTaskCategory.Security),

            "system.security.tpm-inactive" =>
                new TaskTemplate(
                    "task.security.tpm-configuration",
                    "TPM-Konfiguration prüfen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.Security),

            "system.operating-system.legacy-unsupported" =>
                new TaskTemplate(
                    "task.operating-system.replace-unsupported",
                    "Nicht unterstütztes Windows ablösen",
                    CheckupTaskPriority.Required,
                    CheckupTaskCategory.OperatingSystem),

            "system.operating-system.windows-10-support" =>
                new TaskTemplate(
                    "task.operating-system.windows-10-support",
                    "Windows-10-Supportstatus klären",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.OperatingSystem),

            "system.windows-update.service-disabled" =>
                new TaskTemplate(
                    "task.windows-update.service-configuration",
                    "Windows-Update-Dienst prüfen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.WindowsUpdate),

            "system.windows-update.recent-failures" =>
                new TaskTemplate(
                    "task.windows-update.recent-failures",
                    "Fehlgeschlagene Windows-Updates prüfen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.WindowsUpdate),

            "system.program-updates.available" =>
                new TaskTemplate(
                    "task.program-updates.available",
                    "Verfügbare Programmupdates prüfen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.ProgramUpdates),

            "system.restart.required" =>
                new TaskTemplate(
                    "task.restart.perform-controlled-restart",
                    "Kontrollierten Neustart durchführen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.Restart),

            "hardware.storage.health-critical" =>
                new TaskTemplate(
                    "task.storage.physical-health",
                    "Kritischen Datenträgerzustand bearbeiten",
                    CheckupTaskPriority.Required,
                    CheckupTaskCategory.Storage),

            "hardware.storage.health-warning" =>
                new TaskTemplate(
                    "task.storage.physical-health",
                    "Datenträgerwarnung prüfen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.Storage),

            "hardware.storage.relevant-hdd" =>
                new TaskTemplate(
                    "task.hardware.storage-technology",
                    "SSD-Aufrüstung prüfen",
                    CheckupTaskPriority.Optional,
                    CheckupTaskCategory.Hardware),

            "system.storage.system-volume-critically-low"
                or "system.storage.system-volume-low"
                or "system.cleanup.potential-with-low-system-space" =>
                    new TaskTemplate(
                        "task.storage.restore-system-volume-capacity",
                        "Freien Speicher auf dem Systemlaufwerk herstellen",
                        CheckupTaskPriority.Recommended,
                        CheckupTaskCategory.Storage),

            "system.storage.volume-low" =>
                new TaskTemplate(
                    "task.storage.additional-volume-capacity",
                    "Freien Speicher des Datenvolumes prüfen",
                    CheckupTaskPriority.Optional,
                    CheckupTaskCategory.Storage),

            "system.cleanup.large-safe-potential" =>
                new TaskTemplate(
                    "task.storage.controlled-cleanup",
                    "Kontrollierte Bereinigung prüfen",
                    CheckupTaskPriority.Optional,
                    CheckupTaskCategory.Storage),

            "system.startup.conspicuous-entries" =>
                new TaskTemplate(
                    "task.performance.invalid-startup-targets",
                    "Auffällige Autostarteinträge prüfen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.Performance),

            "system.startup.optional-entries"
                or "system.startup.extensive" =>
                    new TaskTemplate(
                        "task.performance.startup-background-load",
                        "Autostart optimieren",
                        CheckupTaskPriority.Optional,
                        CheckupTaskCategory.Performance),

            "system.devices.missing-driver" =>
                new TaskTemplate(
                    "task.devices.missing-driver",
                    "Fehlende Gerätetreiber prüfen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.DevicesAndDrivers),

            "system.devices.windows-problem" =>
                new TaskTemplate(
                    "task.devices.windows-problem",
                    "Windows-Geräteprobleme prüfen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.DevicesAndDrivers),

            "system.devices.unsigned-driver" =>
                new TaskTemplate(
                    "task.devices.unsigned-driver",
                    "Nicht signierte Gerätetreiber prüfen",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.DevicesAndDrivers),

            "hardware.memory.very-low" =>
                new TaskTemplate(
                    "task.hardware.memory-capacity",
                    "Arbeitsspeicherausstattung verbessern",
                    CheckupTaskPriority.Recommended,
                    CheckupTaskCategory.Hardware),

            "hardware.memory.upgrade-recommended" =>
                new TaskTemplate(
                    "task.hardware.memory-capacity",
                    "Arbeitsspeichererweiterung prüfen",
                    CheckupTaskPriority.Optional,
                    CheckupTaskCategory.Hardware),

            "hardware.tpm.older-version" =>
                new TaskTemplate(
                    "task.hardware.tpm-capability",
                    "TPM-Fähigkeit bei der weiteren Planung berücksichtigen",
                    CheckupTaskPriority.Optional,
                    CheckupTaskCategory.Hardware),

            _ =>
                null
        };
    }

    private static string BuildGroupKey(
        TaskCandidate candidate)
    {
        if (!string.IsNullOrWhiteSpace(
                candidate.Finding.CauseGroup))
        {
            return candidate.Finding.CauseGroup;
        }

        return candidate.Template.TaskCode;
    }

    private static CheckupTask CreateTask(
        IReadOnlyCollection<TaskCandidate> candidates,
        DateTime createdAt)
    {
        var leadingCandidate =
            candidates
                .OrderByDescending(
                    candidate =>
                        GetPriorityOrder(
                            candidate.Template.Priority))
                .ThenBy(
                    candidate =>
                        candidate.Template.TaskCode,
                    StringComparer.OrdinalIgnoreCase)
                .First();

        var sourceFindingCodes =
            candidates
                .Select(
                    candidate =>
                        candidate.Finding.Code)
                .Where(
                    code =>
                        !string.IsNullOrWhiteSpace(code))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    code =>
                        code,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        var sourceCauseGroup =
            candidates
                .Select(
                    candidate =>
                        candidate.Finding.CauseGroup)
                .FirstOrDefault(
                    causeGroup =>
                        !string.IsNullOrWhiteSpace(
                            causeGroup))
            ?? string.Empty;

        var description =
            BuildDescription(
                candidates);

        return new CheckupTask
        {
            TaskCode =
                leadingCandidate.Template.TaskCode,

            SourceFindingCodes =
                sourceFindingCodes,

            SourceCauseGroup =
                sourceCauseGroup,

            Title =
                leadingCandidate.Template.Title,

            Description =
                description,

            Priority =
                leadingCandidate.Template.Priority,

            Category =
                leadingCandidate.Template.Category,

            Status =
                CheckupTaskStatus.Open,

            CreatedAt =
                createdAt,

            ActionResults =
                new List<CheckupTaskActionResult>()
        };
    }

    private static string BuildDescription(
        IEnumerable<TaskCandidate> candidates)
    {
        var descriptions =
            candidates
                .Select(
                    candidate =>
                        candidate.Finding.Description?.Trim())
                .Where(
                    description =>
                        !string.IsNullOrWhiteSpace(
                            description))
                .Cast<string>()
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (descriptions.Count == 0)
        {
            return
                "Die zugehörigen Befunde sollten kontrolliert "
                + "geprüft und nachvollziehbar dokumentiert werden.";
        }

        return string.Join(
            Environment.NewLine
            + Environment.NewLine,
            descriptions);
    }

    private static int GetPriorityOrder(
        CheckupTaskPriority priority)
    {
        return priority switch
        {
            CheckupTaskPriority.Required =>
                3,

            CheckupTaskPriority.Recommended =>
                2,

            _ =>
                1
        };
    }

    private sealed record TaskTemplate(
        string TaskCode,
        string Title,
        CheckupTaskPriority Priority,
        CheckupTaskCategory Category);

    private sealed record TaskCandidate(
        CheckupFinding Finding,
        TaskTemplate Template);
}