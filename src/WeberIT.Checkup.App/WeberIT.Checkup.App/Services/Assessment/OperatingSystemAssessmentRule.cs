using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class OperatingSystemAssessmentRule : ICheckupAssessmentRule
{
    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        var operatingSystem =
            checkupSession.OperatingSystemInformation;

        var operatingSystemName =
            operatingSystem.Name?.Trim() ?? string.Empty;

        var operatingSystemVersion =
            operatingSystem.Version?.Trim() ?? string.Empty;

        if (IsUnknownOperatingSystem(operatingSystemName))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Title = "Betriebssystem nicht auswertbar",
                    Description =
                        "Das installierte Betriebssystem konnte nicht zuverlässig bestimmt werden.",
                    Category = FindingCategory.OperatingSystem,
                    Severity = FindingSeverity.Information
                }
            };
        }

        if (Contains(
                operatingSystemName,
                "Windows 11"))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Title = "Windows 11 erkannt",
                    Description =
                        $"Auf dem Gerät ist {BuildVersionDescription(operatingSystemName, operatingSystemVersion)} installiert. "
                        + "Der konkrete Wartungsstatus der installierten Windows-Version "
                        + "sollte weiterhin über Windows Update geprüft werden.",
                    Category = FindingCategory.OperatingSystem,
                    Severity = FindingSeverity.Information
                }
            };
        }

        if (Contains(
                operatingSystemName,
                "Windows 10"))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Title = "Windows 10 prüfen",
                    Description =
                        $"Auf dem Gerät ist {BuildVersionDescription(operatingSystemName, operatingSystemVersion)} installiert. "
                        + "Der reguläre Support für Windows 10 endete am 14.10.2025. "
                        + "Es sollte geprüft werden, ob das Gerät durch ESU oder eine länger unterstützte "
                        + "LTSC-Ausgabe abgesichert ist oder auf Windows 11 umgestellt werden kann.",
                    Category = FindingCategory.OperatingSystem,
                    Severity = FindingSeverity.Warning
                }
            };
        }

        if (IsUnsupportedLegacyWindows(operatingSystemName))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Title = "Veraltetes Windows erkannt",
                    Description =
                        $"Auf dem Gerät ist {BuildVersionDescription(operatingSystemName, operatingSystemVersion)} installiert. "
                        + "Diese Windows-Generation wird regulär nicht mehr mit Sicherheitsupdates versorgt "
                        + "und sollte zeitnah ersetzt werden.",
                    Category = FindingCategory.OperatingSystem,
                    Severity = FindingSeverity.Critical
                }
            };
        }

        return new List<CheckupFinding>
        {
            new()
            {
                Title = "Windows-Supportstatus prüfen",
                Description =
                    $"Auf dem Gerät wurde {BuildVersionDescription(operatingSystemName, operatingSystemVersion)} erkannt. "
                    + "Der Supportstatus dieser Ausgabe konnte nicht automatisch eingeordnet werden "
                    + "und sollte manuell geprüft werden.",
                Category = FindingCategory.OperatingSystem,
                Severity = FindingSeverity.Information
            }
        };
    }

    private static bool IsUnknownOperatingSystem(
        string operatingSystemName)
    {
        return string.IsNullOrWhiteSpace(operatingSystemName)
               || operatingSystemName.Equals(
                   "Unbekannt",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnsupportedLegacyWindows(
        string operatingSystemName)
    {
        return Contains(
                   operatingSystemName,
                   "Windows 8.1")
               || Contains(
                   operatingSystemName,
                   "Windows 8")
               || Contains(
                   operatingSystemName,
                   "Windows 7")
               || Contains(
                   operatingSystemName,
                   "Windows Vista")
               || Contains(
                   operatingSystemName,
                   "Windows XP");
    }

    private static bool Contains(
        string value,
        string searchValue)
    {
        return value.Contains(
            searchValue,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildVersionDescription(
        string operatingSystemName,
        string operatingSystemVersion)
    {
        if (string.IsNullOrWhiteSpace(operatingSystemVersion)
            || operatingSystemVersion.Equals(
                "Unbekannt",
                StringComparison.OrdinalIgnoreCase))
        {
            return operatingSystemName;
        }

        return $"{operatingSystemName}, Version {operatingSystemVersion}";
    }
}