using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

public static class CheckupTaskActionCatalog
{
    public static CheckupTaskActionDefinition GetDefinition(
        string taskCode)
    {
        return taskCode switch
        {
            "task.program-updates.available" =>
                CreateExecutableDefinition(
                    taskCode,
                    "action.program-updates.selected-upgrades",
                    "Ausgewählte Programmupdates installieren",
                    "Die erkannten WinGet-Aktualisierungen können "
                    + "einzeln ausgewählt und nach einer zusätzlichen "
                    + "Bestätigung installiert werden.",
                    "Programmaktualisierungen können Funktionen, "
                    + "Einstellungen oder die Kompatibilität der "
                    + "betroffenen Anwendung verändern.",
                    CheckupTaskActionRiskLevel.Medium,
                    requiresAdministrator: false,
                    mayRequireRestart: true),

            "task.restart.perform-controlled-restart" =>
                CreateExecutableDefinition(
                    taskCode,
                    "action.restart.controlled",
                    "Kontrollierten Neustart vorbereiten",
                    "Nach einer ausdrücklichen Bestätigung kann ein "
                    + "kontrollierter Windows-Neustart vorbereitet werden.",
                    "Nicht gespeicherte Arbeiten in anderen Anwendungen "
                    + "können verloren gehen. Vor dem Neustart müssen "
                    + "alle Arbeiten gespeichert und Programme "
                    + "geschlossen werden.",
                    CheckupTaskActionRiskLevel.Medium,
                    requiresAdministrator: false,
                    mayRequireRestart: true),

            "task.storage.restore-system-volume-capacity" =>
                CreateExecutableDefinition(
                    taskCode,
                    "action.cleanup.selected-safe-categories",
                    "Ausgewählte sichere Kategorien bereinigen",
                    "Ausschließlich zuvor analysierte und ausdrücklich "
                    + "ausgewählte Bereinigungskategorien können einzeln "
                    + "freigegeben werden.",
                    "Gelöschte Cache- und Temporärdaten können teilweise "
                    + "nicht wiederhergestellt werden. Unvollständig "
                    + "gemessene oder manuell zu prüfende Kategorien "
                    + "dürfen nicht automatisch freigegeben werden.",
                    CheckupTaskActionRiskLevel.Medium,
                    requiresAdministrator: true,
                    mayRequireRestart: false),

            "task.storage.controlled-cleanup" =>
                CreateExecutableDefinition(
                    taskCode,
                    "action.cleanup.selected-safe-categories",
                    "Ausgewählte sichere Kategorien bereinigen",
                    "Ausschließlich zuvor analysierte und ausdrücklich "
                    + "ausgewählte Bereinigungskategorien können einzeln "
                    + "freigegeben werden.",
                    "Gelöschte Cache- und Temporärdaten können teilweise "
                    + "nicht wiederhergestellt werden. Unvollständig "
                    + "gemessene oder manuell zu prüfende Kategorien "
                    + "dürfen nicht automatisch freigegeben werden.",
                    CheckupTaskActionRiskLevel.Medium,
                    requiresAdministrator: true,
                    mayRequireRestart: false),

            "task.performance.invalid-startup-targets" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.startup.guided-review",
                    "Auffällige Autostarteinträge geführt prüfen",
                    "Die betroffenen Autostarteinträge werden einzeln "
                    + "mit Quelle, Benutzerkontext, Hersteller und "
                    + "Startziel dargestellt. Die eigentliche Änderung "
                    + "bleibt zunächst manuell.",
                    "Ein irrtümlich deaktivierter oder gelöschter "
                    + "Autostarteintrag kann benötigte Programme oder "
                    + "Systemfunktionen beeinträchtigen.",
                    CheckupTaskActionRiskLevel.Medium),

            "task.performance.startup-background-load" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.startup.guided-review",
                    "Autostarteinträge geführt prüfen",
                    "Optional prüfbare Autostarteinträge werden einzeln "
                    + "erläutert. Es erfolgt keine pauschale "
                    + "Deaktivierung.",
                    "Auch ein optional wirkender Eintrag kann für "
                    + "Synchronisation, Sicherung, Updates oder "
                    + "Gerätefunktionen benötigt werden.",
                    CheckupTaskActionRiskLevel.Medium),

            "task.windows-update.recent-failures" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.windows-update.guided-review",
                    "Fehlgeschlagene Windows-Updates geführt prüfen",
                    "Die Windows-Update-Ansicht kann gezielt geöffnet "
                    + "und das weitere Vorgehen dokumentiert werden. "
                    + "Es wird noch keine automatische Installation "
                    + "ausgeführt.",
                    "Eine erneute Onlinesuche kann Downloads, "
                    + "Installationen oder späteren Neustartbedarf "
                    + "auslösen.",
                    CheckupTaskActionRiskLevel.Medium,
                    mayRequireRestart: true),

            "task.windows-update.service-configuration" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.windows-update.service-review",
                    "Windows-Update-Dienst geführt prüfen",
                    "Der ermittelte Dienstzustand wird erläutert. "
                    + "Die Dienstkonfiguration wird nicht automatisch "
                    + "verändert.",
                    "Eine falsche Dienstkonfiguration kann zukünftige "
                    + "Windows-Aktualisierungen verhindern oder "
                    + "bestehende Verwaltungsrichtlinien umgehen.",
                    CheckupTaskActionRiskLevel.High),

            "task.security.antivirus-protection" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.security.antivirus-review",
                    "Virenschutz geführt prüfen",
                    "Die Windows-Sicherheitsansicht kann geöffnet und "
                    + "der erkannte Virenschutzstatus erläutert werden. "
                    + "Eine Schutzsoftware wird nicht automatisch "
                    + "aktiviert, entfernt oder installiert.",
                    "Änderungen an Schutzsoftware können den Rechner "
                    + "vorübergehend ungeschützt lassen oder Konflikte "
                    + "zwischen mehreren Produkten verursachen.",
                    CheckupTaskActionRiskLevel.High),

            "task.security.antivirus-registration" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.security.antivirus-registration-review",
                    "Virenschutzregistrierung geführt prüfen",
                    "Registrierte Virenschutzprodukte können einzeln "
                    + "geprüft werden. Eine automatische Entfernung "
                    + "erfolgt nicht.",
                    "Das Entfernen einer tatsächlich benötigten "
                    + "Schutzsoftware kann den Systemschutz "
                    + "beeinträchtigen.",
                    CheckupTaskActionRiskLevel.High),

            "task.security.firewall-configuration" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.security.firewall-review",
                    "Firewallkonfiguration geführt prüfen",
                    "Die Windows-Firewalleinstellungen können geöffnet "
                    + "und der erkannte Zustand erläutert werden. "
                    + "Es erfolgt keine pauschale Aktivierung oder "
                    + "Regeländerung.",
                    "Firewalländerungen können Netzwerkzugriffe "
                    + "blockieren oder unbeabsichtigt freigeben.",
                    CheckupTaskActionRiskLevel.High),

            "task.security.user-account-control" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.security.uac-review",
                    "Benutzerkontensteuerung geführt prüfen",
                    "Die UAC-Einstellungen können geöffnet und die "
                    + "Sicherheitsauswirkungen erläutert werden. "
                    + "Der Wert wird nicht automatisch verändert.",
                    "Eine ungeeignete UAC-Einstellung kann den Schutz "
                    + "vor unbemerkten Systemänderungen reduzieren.",
                    CheckupTaskActionRiskLevel.High,
                    mayRequireRestart: true),

            "task.security.security-center" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.security.security-center-review",
                    "Windows-Sicherheitscenter geführt prüfen",
                    "Der Dienstzustand kann erläutert und die passende "
                    + "Windows-Verwaltungsansicht geöffnet werden. "
                    + "Eine Dienständerung erfolgt nicht automatisch.",
                    "Dienständerungen können Sicherheitsmeldungen und "
                    + "die Erkennung installierter Schutzprodukte "
                    + "beeinträchtigen.",
                    CheckupTaskActionRiskLevel.High),

            "task.devices.missing-driver" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.devices.missing-driver-review",
                    "Fehlenden Treiber geführt prüfen",
                    "Das betroffene Gerät, der Windows-Fehlercode und "
                    + "verfügbare Herstellerangaben werden dargestellt. "
                    + "Der Geräte-Manager kann geöffnet werden.",
                    "Ein vermeintlich passender, tatsächlich falscher "
                    + "Treiber kann Geräte- oder Systemfunktionen "
                    + "beeinträchtigen.",
                    CheckupTaskActionRiskLevel.High,
                    mayRequireRestart: true),

            "task.devices.windows-problem" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.devices.windows-problem-review",
                    "Windows-Geräteproblem geführt prüfen",
                    "Das betroffene Gerät und der Windows-Fehlercode "
                    + "werden erläutert. Es wird nicht automatisch "
                    + "angenommen, dass ein Treiber die Ursache ist.",
                    "Ungezielte Treiber- oder Gerätekonfigurationsänderungen "
                    + "können das Problem verschärfen.",
                    CheckupTaskActionRiskLevel.High,
                    mayRequireRestart: true),

            "task.devices.unsigned-driver" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.devices.unsigned-driver-review",
                    "Nicht signierten Treiber geführt prüfen",
                    "Treiberanbieter, Version und INF-Angaben werden "
                    + "für eine manuelle Herstellerprüfung angezeigt.",
                    "Ein ungeprüfter Austausch kann zu Geräteausfällen, "
                    + "Startproblemen oder Sicherheitsrisiken führen.",
                    CheckupTaskActionRiskLevel.High,
                    mayRequireRestart: true),

            _ =>
                CreateManualDefinition(
                    taskCode)
        };
    }

    public static bool HasExecutableAction(
        string taskCode)
    {
        return GetDefinition(
                taskCode)
            .IsExecutable;
    }

    public static bool HasGuidedSupport(
        string taskCode)
    {
        return GetDefinition(
                taskCode)
            .IsGuided;
    }

    private static CheckupTaskActionDefinition
        CreateExecutableDefinition(
            string taskCode,
            string actionCode,
            string actionTitle,
            string description,
            string riskDescription,
            CheckupTaskActionRiskLevel riskLevel,
            bool requiresAdministrator,
            bool mayRequireRestart)
    {
        return new CheckupTaskActionDefinition
        {
            TaskCode =
                taskCode,

            Availability =
                CheckupTaskActionAvailability.Executable,

            ActionCode =
                actionCode,

            ActionTitle =
                actionTitle,

            Description =
                description,

            RiskDescription =
                riskDescription,

            RiskLevel =
                riskLevel,

            RequiresAdministrator =
                requiresAdministrator,

            MayRequireRestart =
                mayRequireRestart
        };
    }

    private static CheckupTaskActionDefinition
        CreateGuidedDefinition(
            string taskCode,
            string actionCode,
            string actionTitle,
            string description,
            string riskDescription,
            CheckupTaskActionRiskLevel riskLevel,
            bool mayRequireRestart = false)
    {
        return new CheckupTaskActionDefinition
        {
            TaskCode =
                taskCode,

            Availability =
                CheckupTaskActionAvailability.Guided,

            ActionCode =
                actionCode,

            ActionTitle =
                actionTitle,

            Description =
                description,

            RiskDescription =
                riskDescription,

            RiskLevel =
                riskLevel,

            RequiresAdministrator =
                false,

            MayRequireRestart =
                mayRequireRestart
        };
    }

    private static CheckupTaskActionDefinition
        CreateManualDefinition(
            string taskCode)
    {
        return new CheckupTaskActionDefinition
        {
            TaskCode =
                taskCode ?? string.Empty,

            Availability =
                CheckupTaskActionAvailability.ManualOnly,

            ActionTitle =
                "Aufgabe manuell bearbeiten",

            Description =
                "Für diese Aufgabe ist keine automatische oder "
                + "geführte Systemaktion freigegeben. Die Prüfung, "
                + "Entscheidung und Durchführung bleiben vollständig "
                + "in der Verantwortung des Technikers.",

            RiskDescription =
                "Das konkrete Risiko hängt von der erforderlichen "
                + "manuellen Maßnahme ab.",

            RiskLevel =
                CheckupTaskActionRiskLevel.None,

            RequiresAdministrator =
                false,

            MayRequireRestart =
                false
        };
    }
}