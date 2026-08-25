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
         * SFC und DISM verwenden vollständig die bereits
         * vorhandene Ausführungs-, Bestätigungs-,
         * Koordinations- und Persistenzlogik.
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
         * Der universelle Beheben-Button öffnet die bereits
         * vorhandene Auswahl-, Plan- und Ausführungsstrecke.
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
         * Auch hier wird ausschließlich der bereits vorhandene
         * Auswahl- und Ausführungsworkflow wiederverwendet.
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
         * In diesem ersten Schritt öffnet "Beheben" die bereits
         * vorhandenen Aktionsdetails mit Risiko- und
         * Handlungshinweisen.
         *
         * Die abschließende manuelle Statusdokumentation bleibt
         * zunächst weiterhin über den bestehenden Statuseditor
         * möglich. Dadurch führen wir an dieser Stelle keine
         * zweite Status- oder Geschäftslogik ein.
         */
        var detailsDialog =
            new TaskActionDetailsDialog(
                definition)
            {
                Owner =
                    Window.GetWindow(
                        this)
            };

        detailsDialog.ShowDialog();
    }
}