#if DEBUG
using Microsoft.Win32;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Startup;

public static class StartupApprovalRegressionTests
{
    public static string RunAll()
    {
        var results = new List<string>();
        void Run(string title, Action test)
        {
            try { test(); results.Add("OK: " + title); }
            catch (Exception ex) { throw new InvalidOperationException(title, ex); }
        }

        Run("HKCU Run und RunOnce nur einmal aus gemeinsamer Registry lesen", () =>
        {
            Require(StartupApprovalReader.GetSourceViews(RegistryHive.CurrentUser, true)
                .SequenceEqual(new[] { RegistryView.Registry64 }));
            Require(StartupApprovalReader.GetSourceViews(RegistryHive.CurrentUser, false)
                .SequenceEqual(new[] { RegistryView.Registry32 }));
        });
        Run("HKLM behaelt beide getrennten Registryansichten auf 64 Bit", () =>
        {
            Require(StartupApprovalReader.GetSourceViews(RegistryHive.LocalMachine, true)
                .SequenceEqual(new[] { RegistryView.Registry64, RegistryView.Registry32 }));
            Require(StartupApprovalReader.GetSourceViews(RegistryHive.LocalMachine, false)
                .SequenceEqual(new[] { RegistryView.Registry32 }));
        });
        Run("HKCU Run verwendet niemals irrtuemlich Run32", () =>
        {
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                Require(StartupApprovalReader.GetApprovalSubkey(RegistryHive.CurrentUser,
                    view, StartupSourceType.RegistryRun, true) == "Run");
        });
        Run("HKLM Run32-Freigabe gehoert zur umgeleiteten Run-Quelle", () =>
        {
            Require(StartupApprovalReader.GetApprovalSubkey(RegistryHive.LocalMachine,
                RegistryView.Registry32, StartupSourceType.RegistryRun, true) == "Run32");
            Require(StartupApprovalReader.GetApprovalSubkey(RegistryHive.LocalMachine,
                RegistryView.Registry64, StartupSourceType.RegistryRun, true) == "Run");
            Require(StartupApprovalReader.GetApprovalSubkey(RegistryHive.LocalMachine,
                RegistryView.Registry32, StartupSourceType.RegistryRun, false) == "Run");
            Require(StartupApprovalReader.NativeView(true) == RegistryView.Registry64);
        });
        Run("Bekannte Windows-Freigaben 02 und 06 sind aktiv", () =>
        {
            Require(Decode(0x02) == StartupEntryState.Enabled);
            Require(Decode(0x06) == StartupEntryState.Enabled);
        });
        Run("Bekannte Windows-Sperren 03 und 07 sind deaktiviert", () =>
        {
            Require(Decode(0x03) == StartupEntryState.Disabled);
            Require(Decode(0x07) == StartupEntryState.Disabled);
        });
        Run("Fehlende Freigabe unterscheidet sich von defekten und unbekannten Werten", () =>
        {
            Require(StartupApprovalReader.Decode(null, true) == StartupEntryState.Enabled);
            Require(StartupApprovalReader.Decode(null, false) == StartupEntryState.Unknown);
            Require(StartupApprovalReader.Decode("02", false) == StartupEntryState.Unknown);
            Require(StartupApprovalReader.Decode(new byte[] { 2 }, false) == StartupEntryState.Unknown);
            Require(Decode(0xFF) == StartupEntryState.Unknown);
            var malformed = new byte[12]; malformed[0] = 2; malformed[1] = 1;
            Require(StartupApprovalReader.Decode(malformed, false) == StartupEntryState.Unknown);
        });
        Run("Autostartordner verwendet eigene Freigabe und ignoriert nur desktop.ini", () =>
        {
            Require(StartupApprovalReader.GetApprovalSubkey(RegistryHive.CurrentUser,
                RegistryView.Registry64, StartupSourceType.StartupFolder, true) == "StartupFolder");
            Require(StartupApprovalReader.GetApprovalSubkey(RegistryHive.LocalMachine,
                RegistryView.Registry64, StartupSourceType.StartupFolder, true) == "StartupFolder");
            Require(StartupApprovalReader.IsFolderMetadata("Desktop.INI"));
            Require(!StartupApprovalReader.IsFolderMetadata("desktop.ini.exe"));
            Require(!StartupApprovalReader.IsFolderMetadata("unknown.dat"));
        });

        return $"Alle {results.Count} Autostart-Freigabe-Regressionstests erfolgreich."
            + Environment.NewLine + string.Join(Environment.NewLine, results);
    }

    private static StartupEntryState Decode(byte value)
    {
        var bytes = new byte[12]; bytes[0] = value;
        if (value is 0x03 or 0x07) bytes[4] = 1;
        return StartupApprovalReader.Decode(bytes, false);
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Erwartung nicht erfüllt.");
    }
}
#endif
