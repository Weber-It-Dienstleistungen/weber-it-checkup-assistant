
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.Services.Tasks;

namespace WeberIT.Checkup.App.Views.Dialogs;

public partial class CleanupActionSelectionDialog :
    Window,
    INotifyPropertyChanged
{
    private readonly CheckupTask _task;

    private readonly CleanupPotentialInformation
        _cleanupInformation;

    private readonly CheckupTaskList _taskList;

    private readonly ICleanupActionPlanBuilder
        _planBuilder;

    private string _selectionStatusText =
        "Noch keine Bereinigungskategorie ausgewählt.";

    public CleanupActionSelectionDialog(
        CheckupTask task,
        CleanupPotentialInformation cleanupInformation,
        CheckupTaskList taskList)
    {
        ArgumentNullException.ThrowIfNull(
            task);

        ArgumentNullException.ThrowIfNull(
            cleanupInformation);

        ArgumentNullException.ThrowIfNull(
            taskList);

        _task =
            task;

        _cleanupInformation =
            cleanupInformation;

        _taskList =
            taskList;

        _planBuilder =
            new CleanupActionPlanBuilder();

        AvailableCategories =
            _planBuilder.GetSelectableCategories(
                cleanupInformation);

        if (AvailableCategories.Count == 0)
        {
            _selectionStatusText =
                "Im gespeicherten Checkup ist keine ausreichend "
                + "gemessene und sicher freigegebene "
                + "Bereinigungskategorie enthalten.";
        }

        InitializeComponent();

        DataContext =
            this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<CleanupActionCategory>
        AvailableCategories
    { get; }

    public bool HasSelectableCategories =>
        AvailableCategories.Count > 0;

    // =============================================
    // SCHRITT 55.14.30e
    // KATEGORIEN FÜR PLANPRÜFUNG
    // =============================================

    /*
     * Der Zähler beschreibt ausschließlich
     * die Kategorien, für die ein Plan
     * geprüft werden kann.
     *
     * Die tatsächliche technische
     * Ausführbarkeit wird separat in
     * CleanupActionPlanSnapshot geprüft.
     *
     * Insbesondere Windows-Temp darf hier
     * mitzählen, obwohl seine produktive
     * Ausführung noch gesperrt ist.
     */
    public string SelectableCategoryCountText =>
        AvailableCategories.Count switch
        {
            0 =>
                "Keine Kategorie für die Planprüfung verfügbar",

            1 =>
                "1 Kategorie für die Planprüfung verfügbar",

            _ =>
                $"{AvailableCategories.Count} Kategorien "
                + "für die Planprüfung verfügbar"
        };

    public string SelectionStatusText
    {
        get =>
            _selectionStatusText;

        private set
        {
            if (_selectionStatusText == value)
            {
                return;
            }

            _selectionStatusText =
                value;

            OnPropertyChanged();
        }
    }

    private void SelectAllButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        CategoriesListBox.SelectAll();
    }

    private void ClearSelectionButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        CategoriesListBox.UnselectAll();
    }

    private void CategoriesListBox_OnSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        var selectedCount =
            CategoriesListBox.SelectedItems.Count;

        PreviewPlanButton.IsEnabled =
            selectedCount > 0;

        SelectionStatusText =
            selectedCount switch
            {
                0 when AvailableCategories.Count == 0 =>
                    "Im gespeicherten Checkup ist keine "
                    + "ausreichend gemessene und sicher "
                    + "freigegebene Bereinigungskategorie enthalten.",

                0 =>
                    "Noch keine Bereinigungskategorie ausgewählt.",

                1 =>
                    "Eine Bereinigungskategorie ist für "
                    + "die Planvorschau ausgewählt.",

                _ =>
                    $"{selectedCount} Bereinigungskategorien "
                    + "sind für die Planvorschau ausgewählt."
            };
    }

    // =============================================
    // BEREINIGUNGSPLAN ÖFFNEN
    // =============================================

    private void PreviewPlanButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        CheckupTaskActionPlan plan;

        try
        {
            plan =
                BuildPlan();
        }
        catch (Exception exception)
        {
            ShowMessage(
                "Aktionsplan nicht erstellt",
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Der Bereinigungsplan konnte nicht "
                      + "sicher erstellt werden."
                    : exception.Message);

            return;
        }

        CleanupActionPlanPreviewDialog previewDialog;

        try
        {
            previewDialog =
                new CleanupActionPlanPreviewDialog(
                    plan)
                {
                    Owner =
                        this
                };
        }
        catch (Exception exception)
        {
            ShowMessage(
                "Aktionsplan nicht geprüft",
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Der Bereinigungsplan konnte nicht "
                      + "sicher geprüft werden."
                    : exception.Message);

            return;
        }

        var previewResult =
            previewDialog.ShowDialog();

        if (previewResult != true
            || previewDialog.ConfirmedPlan is null)
        {
            return;
        }

        try
        {
            ExecuteConfirmedPlan(
                previewDialog.ConfirmedPlan);
        }
        catch (Exception exception)
        {
            ShowMessage(
                "Aktionsplan nicht ausgeführt",
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Der bestätigte Bereinigungsplan "
                      + "konnte nicht ausgeführt werden."
                    : exception.Message);
        }
    }

    // =============================================
    // BEREINIGUNGSPLAN ERSTELLEN
    // =============================================

    private CheckupTaskActionPlan BuildPlan()
    {
        var selections =
            CategoriesListBox
                .SelectedItems
                .Cast<CleanupActionCategory>()
                .Select(
                    category =>
                        new CleanupActionSelection
                        {
                            Category =
                                category.Category
                        })
                .ToList();

        return _planBuilder.Build(
            _task,
            _cleanupInformation,
            selections);
    }

    // =============================================
    // SCHRITT 55.14.34o
    // KONTEXT VOR AUSFÜHRUNG FESTHALTEN
    // =============================================

    private void ExecuteConfirmedPlan(
        CheckupTaskActionPlan confirmedPlan)
    {
        var executablePlan =
            CleanupActionPlanSnapshot
                .CreateExecutableCopy(
                    confirmedPlan);

        var application =
            Application.Current as App
            ?? throw new InvalidOperationException(
                "Der zentrale Anwendungsdienst ist "
                + "nicht verfügbar.");

        var executor =
            application.Services
                .GetRequiredService<
                    ICleanupActionExecutor>();

        /*
         * Die Zuordnung zum Kundencheckup
         * wird vor der technischen Ausführung
         * festgehalten.
         *
         * Der Kontext wird ausdrücklich nicht
         * nachträglich nur anhand der Uhrzeit
         * oder eines Kundennamens geraten.
         *
         * Außerhalb eines eindeutig zugeordneten
         * laufenden Kundencheckups bleibt
         * die Vorgangskennung null.
         */
        Customer? executionCustomer =
            null;

        CustomerDevice? executionDevice =
            null;

        Guid? executionVisitId =
            null;

        if (TryResolveCleanupIntermediateContext(
                out var customer,
                out var device,
                out var visit))
        {
            executionCustomer =
                customer;

            executionDevice =
                device;

            executionVisitId =
                visit.Id;
        }

        // =========================================
        // BESTÄTIGTE BEREINIGUNG AUSFÜHREN
        // =========================================

        var executionDialog =
            new CleanupActionExecutionDialog(
                executablePlan,
                executor)
            {
                Owner =
                    this
            };

        executionDialog.ShowDialog();

        var executionResult =
            executionDialog.ExecutionResult;

        if (executionResult is null
            || executionResult.WasBlocked)
        {
            return;
        }

        if (executionResult.PlanId
            != executablePlan.Id)
        {
            ShowMessage(
                "Ergebnis nicht gespeichert",
                "Das technische Ergebnis konnte nicht "
                + "eindeutig dem bestätigten "
                + "Bereinigungsplan zugeordnet werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Falls bereits Dateien gelöscht wurden, "
                + "darf die Bereinigung nicht ungeprüft "
                + "wiederholt werden.");

            return;
        }

        if (executionResult.WasCancelled
            && !executionResult.WasStarted
            && executionResult.CategoryResults.Count == 0)
        {
            return;
        }

        // =========================================
        // KUNDENKONTEXT NACH AUSFÜHRUNG PRÜFEN
        // =========================================

        /*
         * Eine ursprünglich ermittelte Vorgangs-ID
         * darf nur übernommen werden, wenn
         * derselbe Kunden-, Geräte- und
         * Checkup-Kontext weiterhin aktiv ist.
         *
         * Weicht der Kontext ab, bleibt das
         * technische Ergebnis dokumentierbar,
         * erhält aber keine automatische
         * Kundencheckup-Zuordnung.
         */
        Guid? confirmedVisitId =
            null;

        if (executionVisitId.HasValue
            && TryResolveCleanupIntermediateContext(
                out var currentCustomer,
                out var currentDevice,
                out var currentVisit)
            && ReferenceEquals(
                executionCustomer,
                currentCustomer)
            && ReferenceEquals(
                executionDevice,
                currentDevice)
            && currentVisit.Id
                == executionVisitId.Value)
        {
            confirmedVisitId =
                executionVisitId.Value;
        }

        // =========================================
        // TECHNISCHES ERGEBNIS DOKUMENTIEREN
        // =========================================

        PersistExecutionResult(
            executablePlan,
            executionResult,
            confirmedVisitId);
    }

    // =============================================
    // SCHRITT 55.14.34o
    // STRUKTURIERTES ERGEBNIS SPEICHERN
    // =============================================

    private void PersistExecutionResult(
        CheckupTaskActionPlan plan,
        CleanupActionExecutionResult executionResult,
        Guid? confirmedVisitId)
    {
        string? structuredDocumentationWarning =
            null;

        try
        {
            /*
             * Der bisherige lesbare Nachweis
             * wird unverändert aufgebaut.
             */
            var actionResult =
                CreateTaskActionResult(
                    plan,
                    executionResult);

            /*
             * Nur tatsächlich gestartete
             * Einzelkategorien erhalten
             * einen strukturierten Nachweis.
             *
             * Vor dem Kopieren wird zusätzlich
             * geprüft, ob die vom Executor
             * gemeldete Kategorie tatsächlich
             * der bestätigten Plankategorie
             * entspricht.
             */
            if (executionResult.CategoryResults.Count == 1
                && executionResult.CategoryResults[0]
                    is { WasStarted: true } categoryResult)
            {
                if (plan.CleanupCategories.Count != 1
                    || plan.CleanupCategories[0].Category
                        != categoryResult.Category)
                {
                    structuredDocumentationWarning =
                        "Die technisch gemeldete "
                        + "Bereinigungskategorie stimmt "
                        + "nicht eindeutig mit dem "
                        + "bestätigten Aktionsplan überein.";
                }
                else
                {
                    try
                    {
                        actionResult.CaptureCleanupExecution(
                            executionResult,
                            confirmedVisitId);
                    }
                    catch (Exception exception)
                    {
                        /*
                         * Das bisherige lesbare
                         * Aufgabenprotokoll soll bei
                         * einem Fehler der neuen
                         * strukturierten Erfassung
                         * möglichst erhalten bleiben.
                         *
                         * Die Ausnahme darf nicht
                         * als fehlgeschlagene
                         * Dateibereinigung erscheinen.
                         */
                        structuredDocumentationWarning =
                            exception.Message;
                    }
                }
            }
            else if (executionResult.WasStarted)
            {
                structuredDocumentationWarning =
                    "Das Ausführungsergebnis enthält "
                    + "keine eindeutig gestartete "
                    + "Einzelkategorie.";
            }

            /*
             * Die bestehende Speicherung über
             * CheckupTaskList bleibt erhalten.
             *
             * Das neue strukturierte Ergebnis
             * wird als Bestandteil desselben
             * Aufgabenprotokolls persistiert.
             */
            _taskList.AddTaskActionResult(
                _task,
                actionResult);
        }
        catch (Exception exception)
        {
            ShowMessage(
                "Aktion ausgeführt – Dokumentation fehlgeschlagen",
                "Die Bereinigung wurde bereits technisch "
                + "gestartet, das Ergebnis konnte jedoch nicht "
                + "im Checkup gespeichert werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Die Bereinigung darf nicht ungeprüft "
                + "wiederholt werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Technische Ursache:"
                + Environment.NewLine
                + exception.Message);

            return;
        }

        // =========================================
        // EINGESCHRÄNKTEN NACHWEIS KENNZEICHNEN
        // =========================================

        /*
         * Falls nur die neue strukturierte
         * Erfassung fehlgeschlagen ist,
         * bleibt das lesbare Aufgabenprotokoll
         * nach erfolgreicher Persistierung
         * erhalten.
         *
         * Der Techniker wird ausdrücklich
         * darüber informiert, dass keine
         * automatische Verknüpfung mit einer
         * Zwischenprüfung erfolgen darf.
         */
        if (!string.IsNullOrWhiteSpace(
                structuredDocumentationWarning))
        {
            ShowMessage(
                "Bereinigung dokumentiert – strukturierter Nachweis fehlt",
                "Das bisherige lesbare "
                + "Bereinigungsprotokoll wurde gespeichert."
                + Environment.NewLine
                + Environment.NewLine
                + "Die zusätzlich vorgesehene "
                + "strukturierte Dokumentation konnte "
                + "nicht zuverlässig übernommen werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Eine automatische Zuordnung zu einer "
                + "späteren Zwischenprüfung ist deshalb "
                + "nicht zulässig."
                + Environment.NewLine
                + Environment.NewLine
                + "Technische Ursache:"
                + Environment.NewLine
                + structuredDocumentationWarning);
        }
    }

    // =============================================
    // AUFGABENPROTOKOLL ERSTELLEN
    // =============================================

    private static CheckupTaskActionResult
        CreateTaskActionResult(
        CheckupTaskActionPlan plan,
        CleanupActionExecutionResult executionResult)
    {
        return new CheckupTaskActionResult
        {
            ActionCode =
                plan.ActionCode,

            ActionTitle =
                plan.ActionTitle,

            TargetDescription =
                plan.TargetDescription,

            Status =
                DetermineActionStatus(
                    executionResult),

            Summary =
                BuildResultSummary(
                    executionResult),

            Details =
                BuildTechnicalDetails(
                    plan,
                    executionResult),

            ExitCode =
                null,

            RestartRequired =
                false,

            StartedAt =
                executionResult.StartedAt,

            FinishedAt =
                executionResult.FinishedAt
        };
    }

    // =============================================
    // AKTIONSSTATUS BESTIMMEN
    // =============================================

    private static CheckupTaskActionStatus
        DetermineActionStatus(
        CleanupActionExecutionResult executionResult)
    {
        if (executionResult.IsSuccessful
            || executionResult.IsPartiallySuccessful)
        {
            return
                CheckupTaskActionStatus.Successful;
        }

        if (executionResult.WasCancelled)
        {
            return
                CheckupTaskActionStatus.Cancelled;
        }

        return
            CheckupTaskActionStatus.Failed;
    }

    // =============================================
    // SCHRITT 55.13.8
    // VERSTÄNDLICHE ERGEBNISZUSAMMENFASSUNG
    // =============================================

    private static string BuildResultSummary(
        CleanupActionExecutionResult executionResult)
    {
        var deletionSummary =
            BuildDeletionSummary(
                executionResult);

        /*
         * Erwartete Ausnahmen dürfen
         * weder verschwiegen noch als
         * unerledigte Löschaufgabe
         * dargestellt werden.
         */
        var skippedSummary =
            executionResult.SkippedEntryCount > 0
                ? $" {executionResult.SkippedEntryCount:N0} "
                  + "Einträge wurden kontrolliert "
                  + "übersprungen; Einzelheiten "
                  + "stehen im technischen Protokoll."
                : string.Empty;

        if (executionResult.IsSuccessful)
        {
            return
                "Die kontrollierte Bereinigung wurde "
                + "technisch erfolgreich beendet. "
                + deletionSummary
                + "."
                + skippedSummary
                + " Die Abschlusskontrolle steht aus.";
        }

        if (executionResult.IsPartiallySuccessful)
        {
            return
                "Die kontrollierte Bereinigung wurde "
                + "mit technischen Hinweisen abgeschlossen. "
                + deletionSummary
                + $". {executionResult.FailedEntryCount:N0} "
                + "Einträge konnten nicht gelöscht und "
                + $"{executionResult.SkippedEntryCount:N0} "
                + "Einträge wurden kontrolliert "
                + "übersprungen. Die Abschlusskontrolle "
                + "steht aus.";
        }

        if (executionResult.WasCancelled)
        {
            if (executionResult.DeletedFileCount > 0
                || executionResult.DeletedDirectoryCount > 0)
            {
                return
                    "Die kontrollierte Bereinigung wurde "
                    + "abgebrochen. Bereits ausgeführte "
                    + "Änderungen bleiben bestehen: "
                    + deletionSummary
                    + "."
                    + skippedSummary;
            }

            return
                "Die kontrollierte Bereinigung wurde "
                + "vor einer protokollierten "
                + "Dateiänderung abgebrochen."
                + skippedSummary;
        }

        if (executionResult.WasStarted)
        {
            return
                "Die kontrollierte Bereinigung wurde "
                + "mit technischen Fehlern beendet. "
                + deletionSummary
                + "."
                + skippedSummary;
        }

        return
            "Die kontrollierte Bereinigung konnte "
            + "technisch nicht gestartet werden.";
    }

    // =============================================
    // DATEILÖSCHUNG ZUSAMMENFASSEN
    // =============================================

    private static string BuildDeletionSummary(
        CleanupActionExecutionResult executionResult)
    {
        var fileText =
            executionResult.DeletedFileCount switch
            {
                0 =>
                    "keine Datei gelöscht",

                1 =>
                    "1 Datei gelöscht",

                _ =>
                    $"{executionResult.DeletedFileCount:N0} "
                    + "Dateien gelöscht"
            };

        var directoryText =
            executionResult.DeletedDirectoryCount switch
            {
                0 =>
                    "kein leerer Unterordner entfernt",

                1 =>
                    "1 leerer Unterordner entfernt",

                _ =>
                    $"{executionResult.DeletedDirectoryCount:N0} "
                    + "leere Unterordner entfernt"
            };

        return
            fileText
            + ", "
            + directoryText
            + ", protokollierte Dateigröße "
            + GetDeletedSizeText(
                executionResult);
    }

    private static string GetDeletedSizeText(
        CleanupActionExecutionResult executionResult)
    {
        var categoryResult =
            executionResult.CategoryResults
                .SingleOrDefault();

        if (categoryResult is not null)
        {
            return
                categoryResult.DeletedSizeText;
        }

        return
            $"{executionResult.DeletedSizeBytes:N0} Byte";
    }

    // =============================================
    // SCHRITT 55.13.8
    // TECHNISCHES PROTOKOLL MIT URSACHEN
    // =============================================

    private static string BuildTechnicalDetails(
        CheckupTaskActionPlan plan,
        CleanupActionExecutionResult executionResult)
    {
        var builder =
            new StringBuilder();

        builder.Append("Plan-ID: ");
        builder.AppendLine(
            plan.Id.ToString());

        builder.Append("Ausführung blockiert: ");
        builder.AppendLine(
            executionResult.WasBlocked
                ? "Ja"
                : "Nein");

        builder.Append("Abgebrochen: ");
        builder.AppendLine(
            executionResult.WasCancelled
                ? "Ja"
                : "Nein");

        if (!string.IsNullOrWhiteSpace(
                executionResult.ErrorMessage))
        {
            builder.Append(
                executionResult.IsPartiallySuccessful
                    ? "Technischer Gesamthinweis: "
                    : "Technischer Gesamtfehler: ");

            builder.AppendLine(
                executionResult.ErrorMessage.Trim());
        }

        foreach (var categoryResult
                 in executionResult.CategoryResults)
        {
            builder.AppendLine();

            builder.Append("Kategorie: ");
            builder.AppendLine(
                categoryResult.CategoryTitle);

            builder.Append("Zielpfad: ");
            builder.AppendLine(
                string.IsNullOrWhiteSpace(
                    categoryResult.TargetPath)
                    ? "Nicht ermittelt"
                    : categoryResult.TargetPath);

            builder.Append("Gestartet: ");
            builder.AppendLine(
                categoryResult.WasStarted
                    ? "Ja"
                    : "Nein");

            builder.Append("Abgebrochen: ");
            builder.AppendLine(
                categoryResult.WasCancelled
                    ? "Ja"
                    : "Nein");

            builder.Append("Gelöschte Dateien: ");
            builder.AppendLine(
                categoryResult.DeletedFileCount
                    .ToString("N0"));

            builder.Append("Entfernte leere Unterordner: ");
            builder.AppendLine(
                categoryResult.DeletedDirectoryCount
                    .ToString("N0"));

            builder.Append("Protokollierte Dateigröße: ");
            builder.AppendLine(
                categoryResult.DeletedSizeText);

            builder.Append("Technische Löschfehler: ");
            builder.AppendLine(
                categoryResult.FailedEntryCount
                    .ToString("N0"));

            /*
             * Der bisherige Gesamtzähler
             * bleibt vollständig erhalten.
             */
            builder.Append("Kontrolliert übersprungen: ");
            builder.AppendLine(
                categoryResult.SkippedEntryCount
                    .ToString("N0"));

            /*
             * Aufschlüsselung nach den
             * tatsächlich dokumentierten
             * Überspringursachen.
             *
             * Bei historischen Ergebnissen
             * wird festgehalten, wenn
             * Detaildaten fehlen.
             */
            if (categoryResult.SkippedEntryCount > 0)
            {
                builder.AppendLine();

                builder.AppendLine(
                    "Ursachen der übersprungenen Einträge:");

                builder.AppendLine(
                    categoryResult.SkippedEntryReasonsText);
            }

            if (!string.IsNullOrWhiteSpace(
                    categoryResult.ErrorMessage))
            {
                builder.AppendLine();

                builder.Append(
                    categoryResult.IsPartiallySuccessful
                        ? "Kategoriehinweis: "
                        : "Kategoriefehler: ");

                builder.AppendLine(
                    categoryResult.ErrorMessage.Trim());
            }
        }

        return builder
            .ToString()
            .Trim();
    }

    // =============================================
    // DIALOG SCHLIESSEN
    // =============================================

    private void CloseButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }

    private void ShowMessage(
        string title,
        string message)
    {
        var dialog =
            new MessageDialog(
                title,
                message)
            {
                Owner =
                    this
            };

        dialog.ShowDialog();
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}
