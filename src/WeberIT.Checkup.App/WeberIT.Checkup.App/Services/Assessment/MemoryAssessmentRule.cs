using System.Globalization;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class MemoryAssessmentRule : ICheckupAssessmentRule
{
    private const double RecommendedMemoryInGigabytes = 16;
    private const double MinimumMemoryInGigabytes = 8;

    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        var installedMemory =
            checkupSession.HardwareInformation.InstalledMemory;

        if (!TryParseGigabytes(
                installedMemory,
                out var memoryInGigabytes))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Code =
                        "hardware.memory.not-evaluable",

                    Title =
                        "Arbeitsspeicher nicht auswertbar",

                    Description =
                        "Die Größe des installierten Arbeitsspeichers konnte nicht zuverlässig ermittelt werden.",

                    Category =
                        FindingCategory.Hardware,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.InformationOnly,

                    CauseGroup =
                        "hardware.memory.data-quality"
                }
            };
        }

        if (memoryInGigabytes < MinimumMemoryInGigabytes)
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Code =
                        "hardware.memory.very-low",

                    Title =
                        "Sehr wenig Arbeitsspeicher",

                    Description =
                        $"Das Gerät verfügt über {installedMemory}. "
                        + "Für einen zuverlässigen aktuellen Windows-Betrieb "
                        + "sollten mindestens 8 GB Arbeitsspeicher vorhanden sein.",

                    Category =
                        FindingCategory.Hardware,

                    Severity =
                        FindingSeverity.Warning,

                    AssessmentTarget =
                        FindingAssessmentTarget.HardwareCondition,

                    CauseGroup =
                        "hardware.memory.capacity"
                }
            };
        }

        if (memoryInGigabytes < RecommendedMemoryInGigabytes)
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Code =
                        "hardware.memory.upgrade-recommended",

                    Title =
                        "Arbeitsspeicher erweiterbar",

                    Description =
                        $"Das Gerät verfügt über {installedMemory}. "
                        + "Eine Erweiterung auf mindestens 16 GB kann "
                        + "die Alltagstauglichkeit und Leistungsreserven verbessern.",

                    Category =
                        FindingCategory.Hardware,

                    Severity =
                        FindingSeverity.Recommendation,

                    AssessmentTarget =
                        FindingAssessmentTarget.HardwareCondition,

                    CauseGroup =
                        "hardware.memory.capacity"
                }
            };
        }

        return new List<CheckupFinding>
        {
            new()
            {
                Code =
                    "hardware.memory.sufficient",

                Title =
                    "Ausreichend Arbeitsspeicher",

                Description =
                    $"Mit {installedMemory} verfügt das Gerät über "
                    + "eine zeitgemäße Arbeitsspeicherausstattung.",

                Category =
                    FindingCategory.Hardware,

                Severity =
                    FindingSeverity.Information,

                AssessmentTarget =
                    FindingAssessmentTarget.HardwareCondition,

                CauseGroup =
                    "hardware.memory.capacity"
            }
        };
    }

    private static bool TryParseGigabytes(
        string? installedMemory,
        out double memoryInGigabytes)
    {
        memoryInGigabytes = 0;

        if (string.IsNullOrWhiteSpace(installedMemory))
        {
            return false;
        }

        var normalizedValue = installedMemory
            .Replace(
                "GB",
                string.Empty,
                StringComparison.OrdinalIgnoreCase)
            .Trim()
            .Replace(',', '.');

        return double.TryParse(
                   normalizedValue,
                   NumberStyles.AllowDecimalPoint,
                   CultureInfo.InvariantCulture,
                   out memoryInGigabytes)
               && memoryInGigabytes > 0;
    }
}