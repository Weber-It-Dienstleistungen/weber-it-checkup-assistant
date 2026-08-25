using System.Windows;
using System.Windows.Controls;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Tasks;
using WeberIT.Checkup.App.Views.Dialogs;

namespace WeberIT.Checkup.App.Views.Controls.Checkup;

public partial class CheckupTaskListCard
{
    private const string ProgramUpdateActionCode =
        "action.program-updates.selected-upgrades";

    private const string CleanupActionCode =
        "action.cleanup.selected-safe-categories";

    private async void ResolveTaskButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            ShowPreparationError(
                "Die ausgewählte Aufgabe konnte nicht "
                + "eindeutig bestimmt werden.");

            return;
        }

        if (button.CommandParameter
            is not CheckupTask task)
        {
            ShowPreparationError(
                "Die zugehörige Aufgabe konnte nicht "
                + "eindeutig bestimmt werden.");

            return;
        }

        if (button.DataContext
            is not CheckupTaskActionDefinition definition)
        {
            ShowPreparationError(
                "Für die ausgewählte Aufgabe konnte keine "
                + "Behebungsstrategie bestimmt werden.");

            return;
        }

        if (CheckupSession is null
            || DataContext
                is not CheckupTaskList taskList
            || !ReferenceEquals(
                CheckupSession.TaskList,
                taskList))
        {
            ShowPreparationError(
                "Der zugehörige Checkup- und "
                + "Aufgabenkontext ist nicht vollständig "
                + "verfügbar.");

            return;
        }

        if (!taskList.Tasks.Any(
                currentTask =>
                    currentTask.Id
                    == task.Id))
        {
            ShowPreparationError(
                "Die ausgewählte Aufgabe gehört nicht mehr "
                + "zur aktuellen Aufgabenliste.");

            return;
        }

        /*
         * Wartungsaktionen:
         *
         * SFC und DISM verwenden weiterhin ausschließlich
         * die vorhandene technische Ausführungsstrecke.
         */
        if (CheckupStandardTaskCatalog
            .IsMaintenanceActionCode(
                definition.ActionCode))
        {
            await ExecuteMaintenanceActionAsync(
                button,
                task,
                taskList,
                definition);

            return;
        }

        /*
         * Programmupdates:
         *
         * Der universelle Beheben-Button öffnet weiterhin
         * die vorhandene Auswahl-, Plan- und
         * Ausführungsstrecke.
         */
        if (string.Equals(
                definition.ActionCode,
                ProgramUpdateActionCode,
                StringComparison.Ordinal))
        {
            ProgramUpdateSelectionButton_OnClick(
                button,
                e);

            return;
        }

        /*
         * Bereinigung:
         *
         * Auch hier bleibt die vorhandene Auswahl- und
         * Ausführungslogik maßgeblich.
         */
        if (string.Equals(
                definition.ActionCode,
                CleanupActionCode,
                StringComparison.Ordinal))
        {
            CleanupSelectionButton_OnClick(
                button,
                e);

            return;
        }

        /*
         * Geführte und manuelle Aufgaben:
         *
         * Der Dialog zeigt Anleitung beziehungsweise
         * Prüfansicht und sammelt anschließend ausschließlich
         * die Entscheidung des Technikers.
         *
         * Die eigentliche Statusänderung erfolgt weiterhin
         * zentral über CheckupTaskList.ChangeTaskStatus(...).
         * Damit bleiben Validierung, Benachrichtigung und
         * Persistenz an genau einer Stelle.
         */
        var detailsDialog =
            new TaskActionDetailsDialog(
                definition,
                allowStatusDecision: true)
            {
                Owner =
                    Window.GetWindow(
                        this)
            };

        var dialogResult =
            detailsDialog.ShowDialog();

        if (dialogResult != true
            || !detailsDialog
                .SelectedTaskStatus
                .HasValue)
        {
            return;
        }

        try
        {
            taskList.ChangeTaskStatus(
                task,
                detailsDialog
                    .SelectedTaskStatus
                    .Value,
                detailsDialog.StatusReason,
                detailsDialog.TechnicianNote);
        }
        catch (Exception exception)
        {
            ShowPreparationError(
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Das dokumentierte Ergebnis konnte "
                      + "nicht gespeichert werden."
                    : exception.Message);
        }
    }
}