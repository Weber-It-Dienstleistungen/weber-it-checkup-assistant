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
                CreateManualDefinition(
                    taskCode,
                    "Neustart manuell vorbereiten",
                    "Für den Neustart ist noch keine kontrollierte "
                    + "technische Ausführung freigegeben. Vor der "
                    + "manuellen Durchführung müssen alle Arbeiten "
                    + "gespeichert und geöffnete Programme geschlossen "
                    + "werden.",
                    "Nicht gespeicherte Arbeiten in anderen Anwendungen "
                    + "können verloren gehen. Ein Neustart darf deshalb "
                    + "nicht allein aufgrund eines vorhandenen Befehls "
                    + "automatisiert werden.",
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
                    requiresAdministrator: false,
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
                    requiresAdministrator: false,
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

            "task.operating-system.replace-unsupported" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.operating-system.version-review",
                    "Windows-Version geführt prüfen",
                    "Die Windows-Systeminformationen können geöffnet "
                    + "und mit dem im Checkup erkannten Versions- und "
                    + "Supportstatus abgeglichen werden. Eine Installation "
                    + "oder Migration wird nicht gestartet.",
                    "Der Austausch oder die Aktualisierung eines "
                    + "Betriebssystems kann Programme, Treiber, "
                    + "Benutzerdaten und Gerätekonfigurationen betreffen.",
                    CheckupTaskActionRiskLevel.High,
                    mayRequireRestart: true),

            "task.operating-system.windows-10-support" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.operating-system.version-review",
                    "Windows-10-Supportstatus geführt prüfen",
                    "Die installierte Windows-Edition, Version und "
                    + "Systemausgabe können geöffnet und mit der "
                    + "Checkup-Bewertung abgeglichen werden. Es erfolgt "
                    + "keine automatische Upgradeentscheidung.",
                    "Ein ungeprüftes Funktionsupgrade oder ein Wechsel "
                    + "der Windows-Ausgabe kann Kompatibilitätsprobleme "
                    + "oder zusätzlichen Lizenzbedarf verursachen.",
                    CheckupTaskActionRiskLevel.High,
                    mayRequireRestart: true),

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

            "task.security.drive-encryption" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.security.drive-encryption-review",
                    "Laufwerksverschlüsselung geführt prüfen",
                    "Die BitLocker-Laufwerksübersicht kann geöffnet und "
                    + "mit den im Checkup erkannten Laufwerken "
                    + "abgeglichen werden. Verschlüsselung, Entschlüsselung "
                    + "und Wiederherstellungsschlüssel werden nicht "
                    + "automatisch verändert.",
                    "Ungeprüfte Änderungen an einer "
                    + "Laufwerksverschlüsselung können den Zugriff auf "
                    + "Daten erschweren oder einen "
                    + "Wiederherstellungsschlüssel erforderlich machen.",
                    CheckupTaskActionRiskLevel.High,
                    mayRequireRestart: true),

            "task.security.secure-boot" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.security.secure-boot-review",
                    "Secure-Boot-Status geführt prüfen",
                    "Die Windows-Systeminformationen können geöffnet "
                    + "werden, um den erkannten Secure-Boot-Zustand "
                    + "zusätzlich einzuordnen. Firmwareeinstellungen "
                    + "werden nicht verändert.",
                    "Ungeeignete Änderungen an UEFI-, Start- oder "
                    + "Secure-Boot-Einstellungen können den Windows-Start "
                    + "oder andere Betriebssysteme beeinträchtigen.",
                    CheckupTaskActionRiskLevel.High,
                    mayRequireRestart: true),

            "task.security.tpm-configuration" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.security.tpm-review",
                    "TPM-Konfiguration geführt prüfen",
                    "Die lokale TPM-Verwaltung kann geöffnet und der "
                    + "erkannte Zustand mit dem Checkup abgeglichen "
                    + "werden. Das TPM wird weder initialisiert noch "
                    + "gelöscht.",
                    "Das Löschen oder ungezielte Neukonfigurieren des TPM "
                    + "kann BitLocker-, Anmelde- oder "
                    + "Sicherheitsfunktionen beeinträchtigen.",
                    CheckupTaskActionRiskLevel.High,
                    mayRequireRestart: true),

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
                    "Ungezielte Treiber- oder "
                    + "Gerätekonfigurationsänderungen können das Problem "
                    + "verschärfen.",
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

            "task.storage.physical-health" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.storage.disk-management-review",
                    "Betroffenen Datenträger geführt zuordnen",
                    "Die Windows-Datenträgerverwaltung kann zur "
                    + "eindeutigen Zuordnung von Datenträgern, Partitionen "
                    + "und Volumes geöffnet werden. Die technische "
                    + "Gesundheitsbewertung stammt weiterhin aus dem "
                    + "Checkup.",
                    "Änderungen an Partitionen, Volumes oder "
                    + "Datenträgerinitialisierungen können zu Datenverlust "
                    + "führen. Die Prüfansicht führt selbst keine Änderung "
                    + "aus.",
                    CheckupTaskActionRiskLevel.High,
                    mayRequireRestart: true),

            "task.storage.additional-volume-capacity" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.storage.disk-management-review",
                    "Datenvolume geführt prüfen",
                    "Die Datenträgerverwaltung kann geöffnet werden, um "
                    + "das betroffene Volume, seine Partitionierung und "
                    + "verfügbaren Speicherbereiche einzuordnen. Es wird "
                    + "keine Partition verändert.",
                    "Verkleinern, Erweitern, Formatieren oder Löschen eines "
                    + "Volumes kann Programme und gespeicherte Daten "
                    + "beeinträchtigen.",
                    CheckupTaskActionRiskLevel.High),

            "task.hardware.storage-technology" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.hardware.storage-review",
                    "Datenträgertechnologie geführt einordnen",
                    "Die Datenträgerverwaltung kann zur Zuordnung des "
                    + "betroffenen Laufwerks geöffnet werden. Eine "
                    + "SSD-Aufrüstung bleibt eine manuell zu planende "
                    + "Hardwaremaßnahme.",
                    "Ein Datenträgertausch erfordert eine belastbare "
                    + "Datensicherung und gegebenenfalls eine "
                    + "Systemmigration.",
                    CheckupTaskActionRiskLevel.High,
                    mayRequireRestart: true),

            "task.hardware.memory-capacity" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.hardware.memory-review",
                    "Arbeitsspeicher geführt einordnen",
                    "Die Windows-Systeminformationen können geöffnet und "
                    + "die installierte Arbeitsspeichermenge mit der "
                    + "Checkup-Bewertung abgeglichen werden. Es erfolgt "
                    + "keine automatische Hardwareänderung.",
                    "Eine Arbeitsspeichererweiterung muss zu Mainboard, "
                    + "Prozessor, vorhandenen Modulen und Firmware passen.",
                    CheckupTaskActionRiskLevel.Medium,
                    mayRequireRestart: true),

            "task.hardware.tpm-capability" =>
                CreateGuidedDefinition(
                    taskCode,
                    "action.hardware.tpm-review",
                    "TPM-Fähigkeit geführt einordnen",
                    "Die lokale TPM-Verwaltung kann geöffnet werden, um "
                    + "Verfügbarkeit und Versionsinformationen zusätzlich "
                    + "einzuordnen. Das TPM wird nicht verändert.",
                    "TPM-Änderungen können Laufwerksverschlüsselung und "
                    + "Windows-Sicherheitsfunktionen beeinflussen.",
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
        return CreateManualDefinition(
            taskCode,
            "Aufgabe manuell bearbeiten",
            "Für diese Aufgabe ist keine automatische oder "
            + "geführte Systemaktion freigegeben. Die Prüfung, "
            + "Entscheidung und Durchführung bleiben vollständig "
            + "in der Verantwortung des Technikers.",
            "Das konkrete Risiko hängt von der erforderlichen "
            + "manuellen Maßnahme ab.",
            CheckupTaskActionRiskLevel.None,
            requiresAdministrator: false,
            mayRequireRestart: false);
    }

    private static CheckupTaskActionDefinition
        CreateManualDefinition(
            string taskCode,
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
                taskCode ?? string.Empty,

            Availability =
                CheckupTaskActionAvailability.ManualOnly,

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
}