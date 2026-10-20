
using System.Windows;
using System.Windows.Controls;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Assessment;
using WeberIT.Checkup.App.Services.Tasks;
using WeberIT.Checkup.App.ViewModels;
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
                    currentTask.Id == task.Id))
        {
            ShowPreparationError(
                "Die ausgewählte Aufgabe gehört nicht mehr "
                + "zur aktuellen Aufgabenliste.");

            return;
        }

        // =============================================
        // WARTUNGSAKTIONEN
        // =============================================

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

        // =============================================
        // PROGRAMMUPDATES
        // =============================================

        if (string.Equals(
                definition.ActionCode,
                ProgramUpdateActionCode,
                StringComparison.Ordinal))
        {
            OpenProgramUpdateSelectionWithExceptions(
                task);

            return;
        }

        // =============================================
        // BEREINIGUNG
        // =============================================

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

        // =============================================
        // GEFÜHRTE UND MANUELLE AUFGABEN
        // =============================================

        var detailsDialog =
            new TaskActionDetailsDialog(
                definition,
                allowStatusDecision: true)
            {
                Owner =
                    Window.GetWindow(this)
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

    // =====================================================
    // SCHRITT 29.1:
    // PROGRAMMUPDATE-BEHEBUNG MIT AUSNAHMEN
    // =====================================================

    /// <summary>
    /// Öffnet die Programmupdate-Auswahl für
    /// den universellen Beheben-Button.
    ///
    /// Bei einem laufenden Kundencheckup
    /// werden dokumentierte Ausnahmen aus
    /// der Installationsauswahl entfernt.
    ///
    /// Der allgemeine Diagnosecheckup
    /// behält seinen bisherigen Ablauf.
    /// </summary>
    private void OpenProgramUpdateSelectionWithExceptions(
        CheckupTask task)
    {
        if (CheckupSession is null)
        {
            ShowPreparationError(
                "Der zugehörige Checkup-Kontext "
                + "ist nicht verfügbar.");

            return;
        }

        try
        {
            var ownerWindow =
                Window.GetWindow(this);

            var session =
                CheckupSession;

            var information =
                session.ProgramUpdateInformation;

            var selectionInformation =
                information;

            // =========================================
            // KUNDENGERÄT ERKENNEN
            // =========================================

            if (ownerWindow?.DataContext
                is CustomerDevicesViewModel viewModel)
            {
                var customer =
                    viewModel.SelectedCustomer;

                var device =
                    viewModel.SelectedDevice;

                if (customer is null
                    || device is null
                    || !customer.Devices.Any(
                        storedDevice =>
                            ReferenceEquals(
                                storedDevice,
                                device))
                    || !ReferenceEquals(
                        device.CheckupSession,
                        session)
                    || viewModel.IsScanRunning)
                {
                    throw new InvalidOperationException(
                        "Die Programmupdate-Aufgabe "
                        + "konnte keinem eindeutig "
                        + "bearbeitbaren Kundengerät "
                        + "zugeordnet werden.");
                }

                var visit =
                    session.CurrentCustomerCheckupVisit;

                if (visit is null
                    || !visit.IsInProgress
                    || visit.IsCompletionPrepared
                    || !session.CustomerCheckupVisits.Any(
                        storedVisit =>
                            ReferenceEquals(
                                storedVisit,
                                visit)))
                {
                    throw new InvalidOperationException(
                        "Für dieses Kundengerät ist "
                        + "kein bearbeitbarer laufender "
                        + "Kundencheckup vorhanden.");
                }

                // =====================================
                // WINGET-DATEN PRÜFEN
                // =====================================

                var evaluation =
                    ProgramUpdateExceptionEvaluationService
                        .Evaluate(
                            information,
                            visit);

                if (!evaluation.IsAnalysisReliable)
                {
                    throw new InvalidOperationException(
                        "Die Programmaktualisierungen "
                        + "konnten nicht zuverlässig "
                        + "analysiert werden.");
                }

                // =====================================
                // AUSNAHMEN ABGLEICHEN
                // =====================================

                var applicableExceptions =
                    visit.ProgramUpdateExceptions
                        .Where(approval =>
                            approval is not null
                            && approval.CheckupVisitId
                                == visit.Id)
                        .ToList();

                /*
                 * Nur exakt passende Ausnahmen
                 * werden berücksichtigt.
                 *
                 * Paket-ID, Paketquelle sowie
                 * installierte und verfügbare
                 * Version müssen übereinstimmen.
                 */
                var selectableUpdates =
                    information.AvailableUpdates
                        .Where(update =>
                            update is not null
                            && !applicableExceptions.Any(
                                approval =>
                                    approval.AppliesTo(
                                        visit.Id,
                                        update)))
                        .Select(update =>
                            new AvailableProgramUpdate
                            {
                                PackageId =
                                    update.PackageId,

                                Name =
                                    update.Name,

                                InstalledVersion =
                                    update.InstalledVersion,

                                AvailableVersion =
                                    update.AvailableVersion,

                                Source =
                                    update.Source
                            })
                        .ToList();

                // =====================================
                // KEINE ZULÄSSIGEN UPDATES
                // =====================================

                if (selectableUpdates.Count == 0)
                {
                    ShowPreparationError(
                        "Für den aktuellen Kundencheckup "
                        + "stehen keine Programmupdates "
                        + "zur Installation bereit."
                        + Environment.NewLine
                        + Environment.NewLine
                        + "Die gespeicherten "
                        + "Aktualisierungen sind "
                        + "begründet zurückgestellt "
                        + "oder es liegen keine "
                        + "auswählbaren Pakete vor."
                        + Environment.NewLine
                        + Environment.NewLine
                        + "Eine Ausnahme muss zunächst "
                        + "aufgehoben werden, bevor "
                        + "das betreffende Programm "
                        + "wieder installiert "
                        + "werden darf.");

                    return;
                }

                // =====================================
                // UNABHÄNGIGE ARBEITSLISTE
                // =====================================

                /*
                 * Der Originalscan bleibt
                 * vollständig unverändert.
                 *
                 * Nur die an den Update-Dialog
                 * übergebene Arbeitsliste
                 * wird gefiltert.
                 */
                selectionInformation =
                    new ProgramUpdateInformation
                    {
                        IsWingetAvailable =
                            information.IsWingetAvailable,

                        WingetVersion =
                            information.WingetVersion,

                        IsAnalysisPerformed =
                            information.IsAnalysisPerformed,

                        IsAnalysisSuccessful =
                            information.IsAnalysisSuccessful,

                        AnalysisDate =
                            information.AnalysisDate,

                        AnalysisDetails =
                            information.AnalysisDetails,

                        AvailableUpdateCount =
                            selectableUpdates.Count,

                        IsResultTruncated =
                            information.IsResultTruncated,

                        AvailableUpdates =
                            selectableUpdates
                    };
            }

            // =========================================
            // UPDATE-DIALOG ÖFFNEN
            // =========================================

            /*
             * Der Auswahl-Dialog und der
             * Aktionsplan-Builder bekommen
             * dieselbe gefilterte Liste.
             *
             * "Alle auswählen" kann damit
             * keine zurückgestellten Programme
             * in den Installationsplan aufnehmen.
             */
            var dialog =
                new ProgramUpdateSelectionDialog(
                    task,
                    selectionInformation,
                    session.TaskList)
                {
                    Owner =
                        ownerWindow
                };

            dialog.ShowDialog();
        }
        catch (Exception exception)
        {
            ShowPreparationError(
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Die Programmupdate-Auswahl "
                      + "konnte nicht sicher "
                      + "vorbereitet werden."
                    : exception.Message);
        }
    }
}
