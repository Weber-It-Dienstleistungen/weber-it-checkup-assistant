namespace WeberIT.Checkup.App.Infrastructure.Validation;

public static class ValidationRules
{
    public static string Required(string? value, string fieldName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? $"{fieldName} ist erforderlich."
            : string.Empty;
    }
}