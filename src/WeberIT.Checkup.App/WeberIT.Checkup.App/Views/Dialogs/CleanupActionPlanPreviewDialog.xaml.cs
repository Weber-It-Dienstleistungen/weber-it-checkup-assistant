using System.Windows;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Views.Dialogs;

public partial class CleanupActionPlanPreviewDialog : Window
{
    private const string SupportedActionCode =
        "action.cleanup.selected-safe-categories";

    public CleanupActionPlanPreviewDialog(
        CheckupTaskActionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(
            plan);

        plan.Validate();

        if (!string.Equals(
                plan.ActionCode,
                SupportedActionCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Der dargestellte Aktionsplan ist kein "
                + "freigegebener Bereinigungsplan.");
        }

        if (plan.HasCommands)
        {
            throw new InvalidOperationException(
                "Eine reine Bereinigungsvorschau darf keine "
                + "externen Befehle enthalten.");
        }

        if (!plan.HasCleanupCategories)
        {
            throw new InvalidOperationException(
                "Der Bereinigungsplan enthält keine "
                + "validierte Bereinigungskategorie.");
        }

        InitializeComponent();

        DataContext =
            plan;
    }

    private void CloseButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }
}