using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public sealed class CheckupTaskActionCommandPreview
{
    public string FileName { get; init; } =
        string.Empty;

    public List<string> Arguments { get; init; } =
        new();

    public bool RequiresAdministrator { get; init; }

    [JsonIgnore]
    public string DisplayText
    {
        get
        {
            var displayedArguments =
                Arguments
                    .Select(
                        FormatArgumentForDisplay)
                    .ToList();

            if (displayedArguments.Count == 0)
            {
                return FileName;
            }

            return
                FileName
                + " "
                + string.Join(
                    " ",
                    displayedArguments);
        }
    }

    [JsonIgnore]
    public string AdministratorRequirementText =>
        RequiresAdministrator
            ? "Administratorrechte erforderlich"
            : "Keine Administratorrechte angefordert";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(
                FileName))
        {
            throw new InvalidOperationException(
                "Die Befehlsvorschau benötigt einen "
                + "eindeutigen Programmpfad oder Dateinamen.");
        }

        if (Arguments.Any(
                argument =>
                    argument is null))
        {
            throw new InvalidOperationException(
                "Die Argumentliste enthält einen "
                + "ungültigen Nullwert.");
        }
    }

    private static string FormatArgumentForDisplay(
        string argument)
    {
        if (string.IsNullOrEmpty(
                argument))
        {
            return "\"\"";
        }

        var requiresQuotation =
            argument.Any(
                character =>
                    char.IsWhiteSpace(character)
                    || character
                        is '"'
                        or '\'');

        if (!requiresQuotation)
        {
            return argument;
        }

        var escapedArgument =
            argument.Replace(
                "\"",
                "\\\"",
                StringComparison.Ordinal);

        return
            $"\"{escapedArgument}\"";
    }
}