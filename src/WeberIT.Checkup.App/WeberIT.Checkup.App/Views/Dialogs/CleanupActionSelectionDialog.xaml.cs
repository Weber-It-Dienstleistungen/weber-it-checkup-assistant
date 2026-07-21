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
                "Im gespeicherten Checkup ist keine vollständig "
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

    public string SelectableCategoryCountText =>
        AvailableCategories.Count switch
        {
            0 =>
                "Keine sicher auswählbare Kategorie",

            1 =>
                "1 sicher auswählbare Kategorie",

            _ =>
                $"{AvailableCategories.Count} "
                + "sicher auswählbare Kategorien"
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
                    + "vollständig gemessene und sicher "
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
                + "Bereinigungsplan zugeordnet werden.");

            return;
        }

        if (executionResult.WasCancelled
            && !executionResult.WasStarted
            && executionResult.CategoryResults.Count == 0)
        {
            return;
        }

        PersistExecutionResult(
            executablePlan,
            executionResult);
    }

    private void PersistExecutionResult(
        CheckupTaskActionPlan plan,
        CleanupActionExecutionResult executionResult)
    {
        var actionResult =
            CreateTaskActionResult(
                plan,
                executionResult);

        try
        {
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
        }
    }

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

    private static CheckupTaskActionStatus
        DetermineActionStatus(
            CleanupActionExecutionResult executionResult)
    {
        if (executionResult.IsSuccessful)
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

    private static string BuildResultSummary(
        CleanupActionExecutionResult executionResult)
    {
        var deletionSummary =
            BuildDeletionSummary(
                executionResult);

        if (executionResult.IsSuccessful)
        {
            return
                "Die kontrollierte Bereinigung wurde "
                + "technisch erfolgreich beendet. "
                + deletionSummary
                + ". Die Abschlusskontrolle steht aus.";
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
                    + ".";
            }

            return
                "Die kontrollierte Bereinigung wurde "
                + "vor einer protokollierten "
                + "Dateiänderung abgebrochen.";
        }

        if (executionResult.WasStarted)
        {
            return
                "Die kontrollierte Bereinigung wurde "
                + "mit technischen Fehlern beendet. "
                + deletionSummary
                + ".";
        }

        return
            "Die kontrollierte Bereinigung konnte "
            + "technisch nicht gestartet werden.";
    }

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
            builder.Append("Technischer Gesamtfehler: ");
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

            builder.Append("Sicherheitsbedingt übersprungen: ");
            builder.AppendLine(
                categoryResult.SkippedEntryCount
                    .ToString("N0"));

            if (!string.IsNullOrWhiteSpace(
                    categoryResult.ErrorMessage))
            {
                builder.Append("Kategoriefehler: ");
                builder.AppendLine(
                    categoryResult.ErrorMessage.Trim());
            }
        }

        return builder
            .ToString()
            .Trim();
    }

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