using Microsoft.Win32;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Startup;

/// <summary>
/// Read-only interpretation of the observed Windows StartupApproved format.
/// Unknown or malformed records are not treated as enabled.
/// </summary>
internal static class StartupApprovalReader
{
    private const string ApprovalRoot =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    internal static RegistryView NativeView(bool is64BitOperatingSystem) =>
        is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32;

    internal static IReadOnlyList<RegistryView> GetSourceViews(
        RegistryHive hive, bool is64BitOperatingSystem)
    {
        if (hive == RegistryHive.CurrentUser || !is64BitOperatingSystem)
            return new[] { NativeView(is64BitOperatingSystem) };

        return new[] { RegistryView.Registry64, RegistryView.Registry32 };
    }

    internal static string GetApprovalSubkey(RegistryHive hive, RegistryView sourceView,
        StartupSourceType sourceType, bool is64BitOperatingSystem) =>
        sourceType switch
        {
            StartupSourceType.StartupFolder => "StartupFolder",
            StartupSourceType.RegistryRun when hive == RegistryHive.LocalMachine
                && is64BitOperatingSystem && sourceView == RegistryView.Registry32 => "Run32",
            StartupSourceType.RegistryRun => "Run",
            _ => throw new ArgumentException("Keine StartupApproved-Zuordnung für diese Quelle.")
        };

    internal static StartupEntryState Read(RegistryHive hive, RegistryView sourceView,
        StartupSourceType sourceType, string valueName)
    {
        if (sourceType == StartupSourceType.RegistryRunOnce)
            return StartupEntryState.Enabled;

        if (string.IsNullOrWhiteSpace(valueName))
            return StartupEntryState.Unknown;

        try
        {
            var is64 = Environment.Is64BitOperatingSystem;
            // The shell's approval metadata is read from the native view,
            // not the redirected view of the application Run value.
            using var baseKey = RegistryKey.OpenBaseKey(hive, NativeView(is64));
            using var approvalKey = baseKey.OpenSubKey(
                ApprovalRoot + GetApprovalSubkey(hive, sourceView, sourceType, is64),
                writable: false);
            if (approvalKey is null)
                return StartupEntryState.Enabled;

            var missing = new object();
            var value = approvalKey.GetValue(valueName, missing,
                RegistryValueOptions.DoNotExpandEnvironmentNames);
            return Decode(ReferenceEquals(value, missing) ? null : value,
                ReferenceEquals(value, missing));
        }
        catch
        {
            return StartupEntryState.Unknown;
        }
    }

    internal static StartupEntryState Decode(object? value, bool isMissing)
    {
        if (isMissing)
            return StartupEntryState.Enabled;

        if (value is not byte[] bytes || bytes.Length != 12
            || bytes[1] != 0 || bytes[2] != 0 || bytes[3] != 0)
            return StartupEntryState.Unknown;

        return bytes[0] switch
        {
            0x02 or 0x06 => StartupEntryState.Enabled,
            0x03 or 0x07 => StartupEntryState.Disabled,
            _ => StartupEntryState.Unknown
        };
    }

    internal static bool IsFolderMetadata(string fileName) =>
        string.Equals(fileName, "desktop.ini", StringComparison.OrdinalIgnoreCase);
}
