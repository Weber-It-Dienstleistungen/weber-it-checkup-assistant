using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class FirewallAssessmentRule :
    ICheckupAssessmentRule
{
    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        var profiles =
            checkupSession
                .SecurityInformation
                .FirewallProfiles;

        if (profiles.Count == 0
            || profiles.All(
                profile =>
                    profile.State
                    == SecurityState.Unknown))
        {
            return new List<CheckupFinding>
            {
                new()
                {
                    Code =
                        "system.security.firewall-not-evaluable",

                    Title =
                        "Firewallstatus nicht ermittelbar",

                    Description =
                        "Die Zustände der Windows-Firewallprofile "
                        + "konnten nicht zuverlässig ausgelesen "
                        + "werden.",

                    Category =
                        FindingCategory.Security,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.InformationOnly,

                    CauseGroup =
                        "system.security.firewall-data-quality"
                }
            };
        }

        var findings =
            new List<CheckupFinding>();

        var activeDisabledProfiles =
            profiles
                .Where(
                    profile =>
                        profile.IsActive
                        && profile.State
                        == SecurityState.Disabled)
                .ToList();

        var inactiveDisabledProfiles =
            profiles
                .Where(
                    profile =>
                        !profile.IsActive
                        && profile.State
                        == SecurityState.Disabled)
                .ToList();

        var unknownProfiles =
            profiles
                .Where(
                    profile =>
                        profile.State
                        == SecurityState.Unknown)
                .ToList();

        if (activeDisabledProfiles.Count > 0)
        {
            var profileNames =
                string.Join(
                    ", ",
                    activeDisabledProfiles.Select(
                        profile =>
                            profile.ProfileName));

            findings.Add(
                new CheckupFinding
                {
                    Code =
                        "system.security.active-firewall-disabled",

                    Title =
                        "Aktives Firewallprofil deaktiviert",

                    Description =
                        $"Mindestens ein derzeit verwendetes "
                        + $"Firewallprofil ist deaktiviert: "
                        + $"{profileNames}. Die Windows-Firewall "
                        + "sollte zeitnah geprüft und – sofern "
                        + "keine andere Firewall zuständig ist – "
                        + "wieder aktiviert werden.",

                    Category =
                        FindingCategory.Security,

                    Severity =
                        FindingSeverity.Critical,

                    AssessmentTarget =
                        FindingAssessmentTarget.SystemCondition,

                    CauseGroup =
                        "system.security.firewall-configuration"
                });
        }
        else
        {
            var activeProfileNames =
                profiles
                    .Where(profile => profile.IsActive)
                    .Select(profile => profile.ProfileName)
                    .ToList();

            var activeProfileDescription =
                activeProfileNames.Count > 0
                    ? string.Join(
                        ", ",
                        activeProfileNames)
                    : "kein Profil eindeutig erkannt";

            findings.Add(
                new CheckupFinding
                {
                    Code =
                        "system.security.active-firewall-enabled",

                    Title =
                        "Aktive Firewallprofile eingeschaltet",

                    Description =
                        $"Die derzeit aktiven Firewallprofile "
                        + $"sind eingeschaltet. Aktuell erkannt: "
                        + $"{activeProfileDescription}.",

                    Category =
                        FindingCategory.Security,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.SystemCondition,

                    CauseGroup =
                        "system.security.firewall-configuration"
                });
        }

        if (inactiveDisabledProfiles.Count > 0)
        {
            var profileNames =
                string.Join(
                    ", ",
                    inactiveDisabledProfiles.Select(
                        profile =>
                            profile.ProfileName));

            findings.Add(
                new CheckupFinding
                {
                    Code =
                        "system.security.inactive-firewall-disabled",

                    Title =
                        "Inaktives Firewallprofil deaktiviert",

                    Description =
                        $"Derzeit nicht aktive Firewallprofile "
                        + $"sind deaktiviert: {profileNames}. "
                        + "Bei einem späteren Wechsel des "
                        + "Netzwerktyps könnte dadurch ein "
                        + "ungeschützter Zustand entstehen. Die "
                        + "Konfiguration sollte geprüft werden.",

                    Category =
                        FindingCategory.Security,

                    Severity =
                        FindingSeverity.Warning,

                    AssessmentTarget =
                        FindingAssessmentTarget.SystemCondition,

                    CauseGroup =
                        "system.security.firewall-configuration"
                });
        }

        if (unknownProfiles.Count > 0)
        {
            var profileNames =
                string.Join(
                    ", ",
                    unknownProfiles.Select(
                        profile =>
                            profile.ProfileName));

            findings.Add(
                new CheckupFinding
                {
                    Code =
                        "system.security.firewall-partially-not-evaluable",

                    Title =
                        "Firewallprofile teilweise nicht auswertbar",

                    Description =
                        $"Folgende Firewallprofile konnten nicht "
                        + $"eindeutig ausgewertet werden: "
                        + $"{profileNames}.",

                    Category =
                        FindingCategory.Security,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.InformationOnly,

                    CauseGroup =
                        "system.security.firewall-data-quality"
                });
        }

        return findings;
    }
}