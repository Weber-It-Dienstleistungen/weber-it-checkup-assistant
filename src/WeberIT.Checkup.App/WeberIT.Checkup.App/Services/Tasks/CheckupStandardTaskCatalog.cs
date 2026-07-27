using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

public static class CheckupStandardTaskCatalog
{
    public const int CurrentTaskListVersion =
        3;

    public const string SystemFileCheckTaskCode =
        "task.maintenance.system-file-check";

    public const string WindowsImageRepairTaskCode =
        "task.maintenance.windows-image-repair";

    public const string SystemFileCheckActionCode =
        "action.maintenance.system-file-check";

    public const string WindowsImageRepairActionCode =
        "action.maintenance.windows-image-repair";

    public static IReadOnlyList<CheckupTask>
        CreateStandardTasks(
            DateTime createdAt)
    {
        return new[]
        {
            CreateSystemFileCheckTask(
                createdAt)
        };
    }

    public static CheckupTask
        CreateSystemFileCheckTask(
            DateTime? createdAt = null)
    {
        return new CheckupTask
        {
            TaskCode =
                SystemFileCheckTaskCode,

            SourceFindingCodes =
                new List<string>(),

            SourceCauseGroup =
                "maintenance.system-integrity",

            Title =
                "Windows-Systemdateien prüfen",

            Description =
                "Im Rahmen des regelmäßigen Checkups sollen "
                + "die geschützten Windows-Systemdateien mit "
                + "SFC überprüft werden. Die Prüfung dokumentiert, "
                + "ob Integritätsverletzungen vorliegen und ob "
                + "Windows erkannte Beschädigungen reparieren "
                + "konnte. Wird weiterer Reparaturbedarf erkannt, "
                + "erzeugt das Programm anschließend eine "
                + "separate DISM-Aufgabe.",

            Priority =
                CheckupTaskPriority.Recommended,

            Category =
                CheckupTaskCategory.OperatingSystem,

            Status =
                CheckupTaskStatus.Open,

            CreatedAt =
                createdAt
                ?? DateTime.Now,

            ActionResults =
                new List<CheckupTaskActionResult>()
        };
    }

    public static CheckupTask
        CreateWindowsImageRepairTask(
            DateTime? createdAt = null)
    {
        return new CheckupTask
        {
            TaskCode =
                WindowsImageRepairTaskCode,

            SourceFindingCodes =
                new List<string>
                {
                    "maintenance.sfc.unrepaired-system-files"
                },

            SourceCauseGroup =
                "maintenance.system-integrity",

            Title =
                "Windows-Komponentenspeicher reparieren",

            Description =
                "Die vorherige SFC-Prüfung hat beschädigte "
                + "Systemdateien erkannt, die nicht vollständig "
                + "repariert werden konnten. Der Windows-"
                + "Komponentenspeicher soll deshalb kontrolliert "
                + "mit DISM /RestoreHealth geprüft und repariert "
                + "werden. Nach erfolgreicher DISM-Ausführung "
                + "soll die SFC-Prüfung erneut gestartet werden.",

            Priority =
                CheckupTaskPriority.Recommended,

            Category =
                CheckupTaskCategory.OperatingSystem,

            Status =
                CheckupTaskStatus.Open,

            CreatedAt =
                createdAt
                ?? DateTime.Now,

            ActionResults =
                new List<CheckupTaskActionResult>()
        };
    }

    public static CheckupTaskActionDefinition?
        GetActionDefinition(
            string taskCode)
    {
        return taskCode switch
        {
            SystemFileCheckTaskCode =>
                new CheckupTaskActionDefinition
                {
                    TaskCode =
                        SystemFileCheckTaskCode,

                    Availability =
                        CheckupTaskActionAvailability.Executable,

                    ActionCode =
                        SystemFileCheckActionCode,

                    ActionTitle =
                        "Windows-Systemdateien mit SFC prüfen",

                    Description =
                        "SFC /scannow überprüft die geschützten "
                        + "Windows-Systemdateien. Erkannte "
                        + "Beschädigungen werden, soweit Windows "
                        + "dies selbstständig durchführen kann, "
                        + "direkt repariert. Das technische "
                        + "Ergebnis wird anschließend in der "
                        + "Aufgabe gespeichert.",

                    RiskDescription =
                        "Die Prüfung benötigt Administratorrechte "
                        + "und kann längere Zeit in Anspruch nehmen. "
                        + "SFC kann beschädigte Windows-Systemdateien "
                        + "durch Dateien aus dem lokalen "
                        + "Komponentenspeicher ersetzen.",

                    RiskLevel =
                        CheckupTaskActionRiskLevel.Medium,

                    RequiresAdministrator =
                        true,

                    MayRequireRestart =
                        false
                },

            WindowsImageRepairTaskCode =>
                new CheckupTaskActionDefinition
                {
                    TaskCode =
                        WindowsImageRepairTaskCode,

                    Availability =
                        CheckupTaskActionAvailability.Executable,

                    ActionCode =
                        WindowsImageRepairActionCode,

                    ActionTitle =
                        "Windows-Komponentenspeicher mit DISM reparieren",

                    Description =
                        "DISM /Online /Cleanup-Image /RestoreHealth "
                        + "prüft und repariert den Windows-"
                        + "Komponentenspeicher. Dabei kann Windows "
                        + "Update als Reparaturquelle verwendet "
                        + "werden. Das technische Ergebnis wird "
                        + "anschließend in der Aufgabe gespeichert.",

                    RiskDescription =
                        "Die Reparatur benötigt Administratorrechte, "
                        + "kann längere Zeit dauern und benötigt "
                        + "gegebenenfalls eine funktionierende "
                        + "Windows-Update-Verbindung. Ein Neustart "
                        + "kann erforderlich werden.",

                    RiskLevel =
                        CheckupTaskActionRiskLevel.High,

                    RequiresAdministrator =
                        true,

                    MayRequireRestart =
                        true
                },

            _ =>
                null
        };
    }

    public static bool IsMaintenanceActionCode(
        string actionCode)
    {
        return string.Equals(
                   actionCode,
                   SystemFileCheckActionCode,
                   StringComparison.Ordinal)
               || string.Equals(
                   actionCode,
                   WindowsImageRepairActionCode,
                   StringComparison.Ordinal);
    }

    public static bool IsSystemFileCheckTask(
        string taskCode)
    {
        return string.Equals(
            taskCode,
            SystemFileCheckTaskCode,
            StringComparison.Ordinal);
    }

    public static bool IsWindowsImageRepairTask(
        string taskCode)
    {
        return string.Equals(
            taskCode,
            WindowsImageRepairTaskCode,
            StringComparison.Ordinal);
    }

    public static string GetTargetDescription(
        string taskCode)
    {
        if (IsSystemFileCheckTask(
                taskCode))
        {
            return
                "Geschützte Windows-Systemdateien des "
                + "aktuell ausgeführten Windows-Systems";
        }

        if (IsWindowsImageRepairTask(
                taskCode))
        {
            return
                "Windows-Komponentenspeicher des aktuell "
                + "ausgeführten Windows-Systems";
        }

        throw new InvalidOperationException(
            "Für die ausgewählte Wartungsaufgabe ist "
            + "kein freigegebener Zielbereich definiert.");
    }
}