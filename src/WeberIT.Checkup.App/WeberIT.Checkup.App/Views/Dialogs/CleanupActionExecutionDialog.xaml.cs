using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.Services.Tasks;

namespace WeberIT.Checkup.App.Views.Dialogs;

public partial class CleanupActionExecutionDialog :
    Window,
    INotifyPropertyChanged
{
    private readonly CheckupTaskActionPlan _plan;

    private readonly ICleanupActionExecutor
        _executor;

    private CancellationTokenSource?
        _cancellationTokenSource;

    private bool _executionWasStarted;

    private bool _cancellationWasRequested;

    private bool _isRunning;

    private string _executionStatusText =
        "Ausführung wird vorbereitet";

    private string _executionDetailText =
        "Der bestätigte Bereinigungsplan wird vor "
        + "dem Start erneut geprüft.";

    private string _footerStatusText =
        "Vorbereitung läuft";

    public CleanupActionExecutionDialog(
        CheckupTaskActionPlan confirmedPlan,
        ICleanupActionExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(
            executor);

        _plan =
            CleanupActionPlanSnapshot
                .CreateExecutableCopy(
                    confirmedPlan);

        _executor =
            executor;

        ActionTitle =
            _plan.ActionTitle;

        InitializeComponent();

        DataContext =
            this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ActionTitle { get; }

    public CleanupActionExecutionResult?
        ExecutionResult
    {
        get;
        private set;
    }

    public ObservableCollection<
        CleanupCategoryExecutionDisplayItem>
        CategoryResults
    { get; } =
        new();

    public bool IsRunning
    {
        get =>
            _isRunning;

        private set
        {
            if (_isRunning == value)
            {
                return;
            }

            _isRunning =
                value;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(CanClose));
            OnPropertyChanged(
                nameof(CanCancel));
        }
    }

    public bool CanClose =>
        !IsRunning;

    public bool CanCancel =>
        IsRunning
        && !_cancellationWasRequested;

    public string ExecutionStatusText
    {
        get =>
            _executionStatusText;

        private set
        {
            if (_executionStatusText == value)
            {
                return;
            }

            _executionStatusText =
                value;

            OnPropertyChanged();
        }
    }

    public string ExecutionDetailText
    {
        get =>
            _executionDetailText;

        private set
        {
            if (_executionDetailText == value)
            {
                return;
            }

            _executionDetailText =
                value;

            OnPropertyChanged();
        }
    }

    public string FooterStatusText
    {
        get =>
            _footerStatusText;

        private set
        {
            if (_footerStatusText == value)
            {
                return;
            }

            _footerStatusText =
                value;

            OnPropertyChanged();
        }
    }

    public string ResultCountText =>
        CategoryResults.Count switch
        {
            0 =>
                "Noch kein Kategorieergebnis protokolliert",

            1 =>
                "Ein Kategorieergebnis wurde protokolliert",

            _ =>
                $"{CategoryResults.Count} Kategorieergebnisse "
                + "wurden protokolliert"
        };

    private async void Window_OnContentRendered(
        object sender,
        EventArgs e)
    {
        if (_executionWasStarted)
        {
            return;
        }

        _executionWasStarted =
            true;

        await ExecutePlanAsync();
    }

    private async Task ExecutePlanAsync()
    {
        _cancellationTokenSource =
            new CancellationTokenSource();

        IsRunning =
            true;

        ExecutionStatusText =
            "Benutzertemporärdateien werden bereinigt";

        ExecutionDetailText =
            "Es werden ausschließlich Inhalte des zuvor "
            + "geprüften Benutzer-Temp-Ordners verarbeitet.";

        FooterStatusText =
            "Ausführung läuft – Fenster nicht schließen";

        try
        {
            var executionResult =
                await _executor.ExecuteAsync(
                    _plan,
                    _cancellationTokenSource.Token);

            ValidateExecutionResult(
                executionResult);

            ExecutionResult =
                executionResult;

            PopulateCategoryResults(
                executionResult);

            ApplyExecutionResult(
                executionResult);
        }
        catch (Exception exception)
        {
            ExecutionResult =
                null;

            ExecutionStatusText =
                "Ausführung unerwartet abgebrochen";

            ExecutionDetailText =
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Der bestätigte Bereinigungsplan "
                      + "konnte nicht sicher ausgeführt werden."
                    : exception.Message;

            FooterStatusText =
                "Technischer Fehler";
        }
        finally
        {
            IsRunning =
                false;

            _cancellationTokenSource.Dispose();

            _cancellationTokenSource =
                null;
        }
    }

    private void ValidateExecutionResult(
        CleanupActionExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(
            executionResult);

        if (executionResult.PlanId
            != _plan.Id)
        {
            throw new InvalidOperationException(
                "Das technische Ergebnis gehört nicht "
                + "zum bestätigten Bereinigungsplan.");
        }

        if (executionResult.CategoryResults.Count > 1)
        {
            throw new InvalidOperationException(
                "Das technische Ergebnis enthält mehr "
                + "Kategorien als der bestätigte Plan.");
        }

        if (executionResult.CategoryResults.Any(
                result =>
                    result.Category
                    != CleanupCategoryType.UserTemporaryFiles))
        {
            throw new InvalidOperationException(
                "Das technische Ergebnis enthält eine "
                + "nicht bestätigte Bereinigungskategorie.");
        }
    }

    private void PopulateCategoryResults(
        CleanupActionExecutionResult executionResult)
    {
        CategoryResults.Clear();

        foreach (var categoryResult
                 in executionResult.CategoryResults)
        {
            CategoryResults.Add(
                new CleanupCategoryExecutionDisplayItem(
                    categoryResult));
        }

        OnPropertyChanged(
            nameof(ResultCountText));
    }

    private void ApplyExecutionResult(
        CleanupActionExecutionResult executionResult)
    {
        if (executionResult.WasBlocked)
        {
            ExecutionStatusText =
                "Ausführung blockiert";

            ExecutionDetailText =
                string.IsNullOrWhiteSpace(
                    executionResult.ErrorMessage)
                    ? "Eine andere Systemaktion wird "
                      + "bereits ausgeführt."
                    : executionResult.ErrorMessage;

            FooterStatusText =
                "Keine Datei wurde verarbeitet";

            return;
        }

        if (executionResult.WasCancelled)
        {
            ExecutionStatusText =
                "Bereinigung abgebrochen";

            ExecutionDetailText =
                executionResult.DeletedFileCount > 0
                || executionResult.DeletedDirectoryCount > 0
                    ? "Der Abbruch wurde übernommen. Bereits "
                      + "gelöschte Einträge bleiben gelöscht; "
                      + "alle weiteren Einträge wurden nicht "
                      + "mehr gestartet."
                    : "Der Abbruch wurde übernommen, bevor "
                      + "eine Dateiänderung protokolliert wurde.";

            FooterStatusText =
                "Kontrolliert abgebrochen";

            return;
        }

        if (executionResult.IsSuccessful)
        {
            ExecutionStatusText =
                "Bereinigung technisch abgeschlossen";

            ExecutionDetailText =
                BuildCompletionText(
                    executionResult);

            FooterStatusText =
                "Technisch erfolgreich – Abschlusskontrolle ausstehend";

            return;
        }

        if (executionResult.IsPartiallySuccessful)
        {
            ExecutionStatusText =
                "Bereinigung mit Hinweisen abgeschlossen";

            ExecutionDetailText =
                BuildPartialCompletionText(
                    executionResult);

            FooterStatusText =
                "Ausgeführt mit Hinweisen – Abschlusskontrolle ausstehend";

            return;
        }

        ExecutionStatusText =
            "Bereinigung mit Fehlern beendet";

        ExecutionDetailText =
            string.IsNullOrWhiteSpace(
                executionResult.ErrorMessage)
                ? "Die Bereinigung konnte nicht ausreichend "
                  + "ausgeführt werden. Die technischen "
                  + "Einzelergebnisse bleiben erhalten."
                : executionResult.ErrorMessage;

        FooterStatusText =
            "Technische Prüfung erforderlich";
    }

    private static string BuildCompletionText(
        CleanupActionExecutionResult executionResult)
    {
        var categoryResult =
            executionResult.CategoryResults
                .Single();

        return
            categoryResult.DeletedFileCountText
            + ", "
            + categoryResult.DeletedDirectoryCountText
            + ". Protokollierte Dateigröße: "
            + categoryResult.DeletedSizeText
            + ".";
    }

    private static string BuildPartialCompletionText(
        CleanupActionExecutionResult executionResult)
    {
        var categoryResult =
            executionResult.CategoryResults
                .Single();

        return
            categoryResult.DeletedFileCountText
            + ", "
            + categoryResult.DeletedDirectoryCountText
            + ". Protokollierte Dateigröße: "
            + categoryResult.DeletedSizeText
            + ". "
            + categoryResult.FailureCountText
            + ". "
            + categoryResult.SkippedEntryCountText
            + ". Gesperrte, bereits entfernte oder "
            + "sicherheitsbedingt nicht freigegebene Einträge "
            + "blieben unverändert.";
    }

    private void CancelButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        RequestCancellation();
    }

    private void RequestCancellation()
    {
        if (!CanCancel
            || _cancellationTokenSource is null)
        {
            return;
        }

        _cancellationWasRequested =
            true;

        OnPropertyChanged(
            nameof(CanCancel));

        ExecutionStatusText =
            "Abbruch wird angefordert";

        ExecutionDetailText =
            "Der aktuell bearbeitete Dateisystemzugriff "
            + "wird beendet. Vor dem nächsten Eintrag "
            + "wird die Verarbeitung abgebrochen.";

        FooterStatusText =
            "Abbruch angefordert – bitte warten";

        _cancellationTokenSource.Cancel();
    }

    private void CloseButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (IsRunning)
        {
            RequestCancellation();

            return;
        }

        Close();
    }

    private void Window_OnClosing(
        object? sender,
        CancelEventArgs e)
    {
        if (!IsRunning)
        {
            return;
        }

        e.Cancel =
            true;

        RequestCancellation();
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }

    public sealed class CleanupCategoryExecutionDisplayItem
    {
        public CleanupCategoryExecutionDisplayItem(
            CleanupActionCategoryExecutionResult result)
        {
            ArgumentNullException.ThrowIfNull(
                result);

            CategoryTitle =
                string.IsNullOrWhiteSpace(
                    result.CategoryTitle)
                    ? "Benutzertemporärdateien"
                    : result.CategoryTitle;

            TargetPathText =
                string.IsNullOrWhiteSpace(
                    result.TargetPath)
                    ? "Zielpfad wurde nicht ermittelt"
                    : result.TargetPath;

            DeletedFileCountText =
                result.DeletedFileCountText;

            DeletedDirectoryCountText =
                result.DeletedDirectoryCountText;

            DeletedSizeText =
                "Protokollierte Dateigröße: "
                + result.DeletedSizeText;

            FailureCountText =
                result.FailureCountText;

            SkippedEntryCountText =
                result.SkippedEntryCountText;

            StatusText =
                DetermineStatusText(
                    result);

            ErrorText =
                string.IsNullOrWhiteSpace(
                    result.ErrorMessage)
                    ? string.Empty
                    : result.ErrorMessage.Trim();

            HasError =
                !string.IsNullOrWhiteSpace(
                    ErrorText);
        }

        public string CategoryTitle { get; }

        public string TargetPathText { get; }

        public string DeletedFileCountText { get; }

        public string DeletedDirectoryCountText { get; }

        public string DeletedSizeText { get; }

        public string FailureCountText { get; }

        public string SkippedEntryCountText { get; }

        public string StatusText { get; }

        public string ErrorText { get; }

        public bool HasError { get; }

        private static string DetermineStatusText(
            CleanupActionCategoryExecutionResult result)
        {
            if (result.WasCancelled)
            {
                return
                    "Abgebrochen";
            }

            if (result.IsSuccessful)
            {
                return
                    "Technisch erfolgreich";
            }

            if (result.IsPartiallySuccessful)
            {
                return
                    "Mit Hinweisen abgeschlossen";
            }

            if (!result.WasStarted)
            {
                return
                    "Nicht gestartet";
            }

            return
                "Mit Fehlern beendet";
        }
    }
}