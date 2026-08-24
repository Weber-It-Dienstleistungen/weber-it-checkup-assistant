using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.ViewModels;
using WeberIT.Checkup.App.Views.Dialogs;

namespace WeberIT.Checkup.App.Views.Controls.Checkup;

public partial class ProgramUpdatesCard : UserControl
{
    private const string ProgramUpdateTaskCode =
        "task.program-updates.available";

    public ProgramUpdatesCard()
    {
        InitializeComponent();
    }

    private void ResolveProgramUpdatesButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext
            is not ProgramUpdateInformation
                programUpdateInformation)
        {
            ShowError(
                "Programmupdates können nicht geöffnet werden",
                "Die zugehörigen WinGet-Analysedaten "
                + "sind nicht verfügbar.");

            return;
        }

        if (!programUpdateInformation
                .IsAnalysisSuccessful
            || programUpdateInformation
                .AvailableUpdateCount <= 0
            || programUpdateInformation
                .AvailableUpdates.Count == 0)
        {
            ShowError(
                "Keine ausführbaren Programmupdates",
                "Für diese Analyse stehen aktuell keine "
                + "zuverlässig erkannten Programmupdates "
                + "zur Behebung bereit.");

            return;
        }

        var checkupSession =
            ResolveCheckupSession(
                programUpdateInformation);

        if (checkupSession is null)
        {
            ShowError(
                "Checkup-Kontext nicht verfügbar",
                "Die Programmaktualisierungen konnten keinem "
                + "eindeutigen Checkup zugeordnet werden.");

            return;
        }

        var task =
            checkupSession
                .TaskList
                .Tasks
                .SingleOrDefault(
                    currentTask =>
                        string.Equals(
                            currentTask.TaskCode,
                            ProgramUpdateTaskCode,
                            StringComparison.Ordinal));

        if (task is null)
        {
            ShowError(
                "Updateaufgabe nicht verfügbar",
                "Die WinGet-Analyse enthält verfügbare "
                + "Programmupdates, die zugehörige "
                + "Checkup-Aufgabe wurde jedoch nicht gefunden."
                + Environment.NewLine
                + Environment.NewLine
                + "Die Updates wurden nicht verändert.");

            return;
        }

        try
        {
            var dialog =
                new ProgramUpdateSelectionDialog(
                    task,
                    programUpdateInformation,
                    checkupSession.TaskList)
                {
                    Owner =
                        Window.GetWindow(
                            this)
                };

            dialog.ShowDialog();
        }
        catch (Exception exception)
        {
            ShowError(
                "Programmupdate-Auswahl konnte nicht geöffnet werden",
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Die Programmupdate-Auswahl konnte "
                      + "nicht sicher vorbereitet werden."
                    : exception.Message);
        }
    }

    private CheckupSession? ResolveCheckupSession(
        ProgramUpdateInformation
            programUpdateInformation)
    {
        DependencyObject? current =
            this;

        while (current is not null)
        {
            current =
                VisualTreeHelper.GetParent(
                    current);

            if (current
                is not FrameworkElement element)
            {
                continue;
            }

            if (element.DataContext
                    is CheckupSession session
                && ReferenceEquals(
                    session.ProgramUpdateInformation,
                    programUpdateInformation))
            {
                return session;
            }

            if (element.DataContext
                    is CheckupViewModel checkupViewModel
                && ReferenceEquals(
                    checkupViewModel
                        .CurrentCheckup
                        .ProgramUpdateInformation,
                    programUpdateInformation))
            {
                return checkupViewModel
                    .CurrentCheckup;
            }
        }

        return null;
    }

    private void ShowError(
        string title,
        string message)
    {
        MessageBox.Show(
            Window.GetWindow(
                this),
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}