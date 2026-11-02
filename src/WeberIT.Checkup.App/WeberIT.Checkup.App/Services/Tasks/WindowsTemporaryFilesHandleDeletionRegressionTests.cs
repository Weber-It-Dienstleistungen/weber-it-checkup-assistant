
#if DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.7
///
/// Gezielte Regressionstests der echten
/// handle-basierten Dateilöschung.
///
/// Ausschließlich selbst erzeugte Dateien
/// innerhalb einer GUID-Sandbox.
///
/// Sicherheitsregeln:
/// - Keine Administratorrechte.
/// - Kein Zugriff auf Windows-Temp.
/// - Keine produktiven Benutzerdaten.
/// - Keine rekursive Löschung.
/// - Keine Berechtigungsänderungen.
/// - Nur bekannte Testdateien entfernen.
///
/// Der produktive Executor wird durch
/// diese Tests nicht aufgerufen.
/// </summary>
public static class
    WindowsTemporaryFilesHandleDeletionRegressionTests
{
    private const string NormalContent =
        "WEBER_IT_HANDLE_DELETE_NORMAL";

    private const string ReadOnlyContent =
        "WEBER_IT_HANDLE_KEEP_READONLY";

    private const string SystemContent =
        "WEBER_IT_HANDLE_KEEP_SYSTEM";

    private const string LockedContent =
        "WEBER_IT_HANDLE_KEEP_LOCKED";

    private const string OriginalContent =
        "WEBER_IT_HANDLE_SWAP_ORIGINAL";

    private const string ReplacementContent =
        "WEBER_IT_HANDLE_SWAP_REPLACEMENT";

    public static string RunAll()
    {
        EnsureNonElevated();

        using var sandbox = new TestSandbox();

        var results = new List<string>();

        Execute(
            "Normale Datei wird über Handle gelöscht",
            () => TestNormalDeletion(sandbox),
            results);

        Execute(
            "Schreibgeschützte Datei bleibt erhalten",
            () => TestReadOnlyProtection(sandbox),
            results);

        Execute(
            "Systemgeschützte Datei bleibt erhalten",
            () => TestSystemProtection(sandbox),
            results);

        Execute(
            "Exklusiv gesperrte Datei bleibt erhalten",
            () => TestLockedFileProtection(sandbox),
            results);

        Execute(
            "Ausgetauschte Datei wird nicht gelöscht",
            () => TestIdentityMismatch(sandbox),
            results);

        Execute(
            "Verzeichnis kann nicht als Datei gelöscht werden",
            () => TestDirectoryProtection(sandbox),
            results);

        return
            $"Alle {results.Count} Handle-Löschtests "
            + "erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // TEST 1: NORMALE DATEI
    // =============================================

    private static void TestNormalDeletion(
        TestSandbox sandbox)
    {
        using var root =
            OpenRoot(sandbox);

        var expected =
            Inspect(root, "delete-normal.txt");

        Require(
            !expected.IsDirectory
            && !expected.IsReparsePoint,
            "Die Testdatei wurde nicht "
            + "als reguläre Datei erkannt.");

        var deleted =
            WindowsTemporaryFilesHandleDeletion
                .DeleteInspectedFile(
                    root,
                    "delete-normal.txt",
                    expected);

        Require(
            !File.Exists(sandbox.NormalFile),
            "Die normale Testdatei "
            + "wurde nicht gelöscht.");

        Require(
            deleted.VolumeSerialNumber
                == expected.VolumeSerialNumber,
            "Die Volume-Identität "
            + "stimmt nicht überein.");

        Require(
            deleted.FileIndex == expected.FileIndex,
            "Die gelöschte Datei besitzt "
            + "eine andere Identität.");

        Require(
            deleted.SizeBytes == expected.SizeBytes,
            "Die gemeldete Dateigröße "
            + "ist nicht korrekt.");
    }

    // =============================================
    // TEST 2: SCHREIBSCHUTZ
    // =============================================

    private static void TestReadOnlyProtection(
        TestSandbox sandbox)
    {
        using var root =
            OpenRoot(sandbox);

        var expected =
            Inspect(root, "keep-readonly.txt");

        Require(
            expected.IsReadOnly,
            "Das ReadOnly-Attribut "
            + "wurde nicht erkannt.");

        ExpectSafetyRejection(
            () =>
                WindowsTemporaryFilesHandleDeletion
                    .DeleteInspectedFile(
                        root,
                        "keep-readonly.txt",
                        expected),
            "schreibgeschützt");

        Require(
            File.Exists(sandbox.ReadOnlyFile),
            "Die schreibgeschützte Datei "
            + "wurde gelöscht.");

        Require(
            File.ReadAllText(sandbox.ReadOnlyFile)
                == ReadOnlyContent,
            "Die schreibgeschützte Datei "
            + "wurde verändert.");

        Require(
            (File.GetAttributes(sandbox.ReadOnlyFile)
                & FileAttributes.ReadOnly) != 0,
            "Der Schreibschutz "
            + "wurde entfernt.");
    }

    // =============================================
    // TEST 3: SYSTEMSCHUTZ
    // =============================================

    private static void TestSystemProtection(
        TestSandbox sandbox)
    {
        using var root =
            OpenRoot(sandbox);

        var expected =
            Inspect(root, "keep-system.txt");

        Require(
            expected.IsSystemProtected,
            "Das Systemattribut "
            + "wurde nicht erkannt.");

        ExpectSafetyRejection(
            () =>
                WindowsTemporaryFilesHandleDeletion
                    .DeleteInspectedFile(
                        root,
                        "keep-system.txt",
                        expected),
            "systemgeschützt");

        Require(
            File.Exists(sandbox.SystemFile),
            "Die systemgeschützte Datei "
            + "wurde gelöscht.");

        Require(
            File.ReadAllText(sandbox.SystemFile)
                == SystemContent,
            "Die systemgeschützte Datei "
            + "wurde verändert.");

        Require(
            (File.GetAttributes(sandbox.SystemFile)
                & FileAttributes.System) != 0,
            "Das Systemattribut "
            + "wurde entfernt.");
    }

    // =============================================
    // TEST 4: DATEISPERRE
    // =============================================

    private static void TestLockedFileProtection(
        TestSandbox sandbox)
    {
        using var root =
            OpenRoot(sandbox);

        /*
         * Zuerst untersuchen, danach
         * die Datei exklusiv sperren.
         *
         * Dadurch testen wir tatsächlich
         * den späteren Löschversuch.
         */
        var expected =
            Inspect(root, "keep-locked.txt");

        using (var exclusiveLock =
            new FileStream(
                sandbox.LockedFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None))
        {
            var rejected = false;

            try
            {
                WindowsTemporaryFilesHandleDeletion
                    .DeleteInspectedFile(
                        root,
                        "keep-locked.txt",
                        expected);
            }
            catch (IOException)
            {
                rejected = true;
            }

            Require(
                rejected,
                "Die exklusiv gesperrte Datei "
                + "wurde nicht als gesperrt abgewiesen.");

            Require(
                File.Exists(sandbox.LockedFile),
                "Die gesperrte Datei "
                + "wurde entfernt.");
        }

        Require(
            File.ReadAllText(sandbox.LockedFile)
                == LockedContent,
            "Die gesperrte Datei "
            + "wurde inhaltlich verändert.");
    }

    // =============================================
    // TEST 5: DATEI WIRD AUSGETAUSCHT
    // =============================================

    private static void TestIdentityMismatch(
        TestSandbox sandbox)
    {
        using var root =
            OpenRoot(sandbox);

        /*
         * Schritt 1:
         * Identität der ursprünglichen
         * Datei feststellen.
         */
        var originalSnapshot =
            Inspect(root, "swap.txt");

        /*
         * Schritt 2:
         * Ursprüngliche Datei umbenennen.
         *
         * Die ursprüngliche Datei bleibt
         * tatsächlich vorhanden.
         *
         * Dadurch kann Windows ihre
         * Dateiidentität nicht einfach
         * für die Ersatzdatei wiederverwenden.
         */
        File.Move(
            sandbox.SwapFile,
            sandbox.MovedOriginalFile);

        /*
         * Schritt 3:
         * Unter dem ursprünglichen
         * Dateinamen eine andere
         * Datei erzeugen.
         */
        File.WriteAllText(
            sandbox.SwapFile,
            ReplacementContent);

        var replacementSnapshot =
            Inspect(root, "swap.txt");

        Require(
            originalSnapshot.VolumeSerialNumber
                != replacementSnapshot.VolumeSerialNumber
            || originalSnapshot.FileIndex
                != replacementSnapshot.FileIndex,
            "Der Test konnte keinen tatsächlichen "
            + "Dateiidentitätswechsel nachweisen.");

        /*
         * Schritt 4:
         * Die Löschfunktion erhält
         * weiterhin die alte Identität.
         *
         * Sie muss den Austausch bemerken
         * und die neue Datei erhalten.
         */
        ExpectSafetyRejection(
            () =>
                WindowsTemporaryFilesHandleDeletion
                    .DeleteInspectedFile(
                        root,
                        "swap.txt",
                        originalSnapshot),
            "Dateiidentität");

        Require(
            File.Exists(sandbox.SwapFile),
            "Die neue Datei wurde "
            + "versehentlich gelöscht.");

        Require(
            File.ReadAllText(sandbox.SwapFile)
                == ReplacementContent,
            "Die Ersatzdatei wurde verändert.");

        Require(
            File.Exists(sandbox.MovedOriginalFile),
            "Die ursprüngliche Datei "
            + "ist verschwunden.");

        Require(
            File.ReadAllText(
                sandbox.MovedOriginalFile)
                == OriginalContent,
            "Die ursprüngliche Datei "
            + "wurde verändert.");
    }

    // =============================================
    // TEST 6: VERZEICHNIS
    // =============================================

    private static void TestDirectoryProtection(
        TestSandbox sandbox)
    {
        using var root =
            OpenRoot(sandbox);

        var expected =
            Inspect(root, "KeepDirectory");

        Require(
            expected.IsDirectory,
            "Das Testverzeichnis wurde "
            + "nicht als Ordner erkannt.");

        ExpectSafetyRejection(
            () =>
                WindowsTemporaryFilesHandleDeletion
                    .DeleteInspectedFile(
                        root,
                        "KeepDirectory",
                        expected),
            "Verzeichnis");

        Require(
            Directory.Exists(
                sandbox.KeepDirectory),
            "Das Testverzeichnis "
            + "wurde entfernt.");
    }

    // =============================================
    // HANDLE-HILFSMETHODEN
    // =============================================

    private static SafeFileHandle OpenRoot(
        TestSandbox sandbox)
    {
        return
            WindowsTemporaryFilesHandleAccess
                .OpenRootDirectory(
                    sandbox.TargetDirectory);
    }

    private static
        WindowsTemporaryFilesHandleAccess.EntrySnapshot
        Inspect(
            SafeFileHandle root,
            string entryName)
    {
        using var file =
            WindowsTemporaryFilesHandleAccess
                .OpenChildForInspection(
                    root,
                    entryName);

        return
            WindowsTemporaryFilesHandleAccess
                .ReadSnapshot(file);
    }

    private static void ExpectSafetyRejection(
        Action action,
        string expectedMessagePart)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains(
                expectedMessagePart,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            "Die erwartete Sicherheitsablehnung "
            + "ist ausgeblieben: "
            + expectedMessagePart);
    }

    // =============================================
    // BERECHTIGUNGEN
    // =============================================

    private static void EnsureNonElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Die Tests benötigen Windows.");
        }

        using var identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(identity);

        if (principal.IsInRole(
                WindowsBuiltInRole.Administrator))
        {
            throw new InvalidOperationException(
                "SICHERHEITSSTOPP: "
                + "Diese Löschtests dürfen "
                + "nicht erhöht ausgeführt werden.");
        }
    }

    // =============================================
    // SANDBOX-TESTDATEN
    // =============================================

    private sealed class TestSandbox : IDisposable
    {
        public string RootDirectory { get; }

        public string TargetDirectory { get; }

        public string NormalFile { get; }

        public string ReadOnlyFile { get; }

        public string SystemFile { get; }

        public string LockedFile { get; }

        public string SwapFile { get; }

        public string MovedOriginalFile { get; }

        public string KeepDirectory { get; }

        public TestSandbox()
        {
            var tempRoot =
                Path.GetFullPath(
                    Path.GetTempPath());

            var sharedRoot =
                Path.Combine(
                    tempRoot,
                    "WeberIT.Checkup");

            var sandboxParent =
                Path.Combine(
                    sharedRoot,
                    "WindowsTempSandbox");

            RootDirectory =
                Path.Combine(
                    sandboxParent,
                    "Deletion-"
                    + Guid.NewGuid().ToString("N"));

            TargetDirectory =
                Path.Combine(
                    RootDirectory,
                    "Temp");

            NormalFile =
                Path.Combine(
                    TargetDirectory,
                    "delete-normal.txt");

            ReadOnlyFile =
                Path.Combine(
                    TargetDirectory,
                    "keep-readonly.txt");

            SystemFile =
                Path.Combine(
                    TargetDirectory,
                    "keep-system.txt");

            LockedFile =
                Path.Combine(
                    TargetDirectory,
                    "keep-locked.txt");

            SwapFile =
                Path.Combine(
                    TargetDirectory,
                    "swap.txt");

            MovedOriginalFile =
                Path.Combine(
                    TargetDirectory,
                    "swap-original-moved.txt");

            KeepDirectory =
                Path.Combine(
                    TargetDirectory,
                    "KeepDirectory");

            Require(
                Directory.Exists(tempRoot)
                && !IsReparsePoint(tempRoot),
                "Der temporäre Stammordner "
                + "ist nicht zulässig.");

            EnsurePlainDirectory(sharedRoot);
            EnsurePlainDirectory(sandboxParent);

            Require(
                !Directory.Exists(RootDirectory)
                && !File.Exists(RootDirectory),
                "Der GUID-Testordner "
                + "existiert bereits.");

            EnsurePlainDirectory(RootDirectory);
            EnsurePlainDirectory(TargetDirectory);
            EnsurePlainDirectory(KeepDirectory);

            File.WriteAllText(
                NormalFile,
                NormalContent);

            File.WriteAllText(
                ReadOnlyFile,
                ReadOnlyContent);

            File.SetAttributes(
                ReadOnlyFile,
                File.GetAttributes(ReadOnlyFile)
                    | FileAttributes.ReadOnly);

            File.WriteAllText(
                SystemFile,
                SystemContent);

            File.SetAttributes(
                SystemFile,
                File.GetAttributes(SystemFile)
                    | FileAttributes.System);

            File.WriteAllText(
                LockedFile,
                LockedContent);

            File.WriteAllText(
                SwapFile,
                OriginalContent);
        }

        public void Dispose()
        {
            /*
             * Ausschließlich bekannte
             * Testdateien entfernen.
             *
             * Keine rekursive Löschung.
             *
             * Attribute werden nur an
             * unseren eigenen Dateien
             * zurückgesetzt und erst
             * nach Inhaltsprüfung.
             */

            TryDeleteKnownFile(
                NormalFile,
                NormalContent);

            TryDeleteKnownFile(
                ReadOnlyFile,
                ReadOnlyContent);

            TryDeleteKnownFile(
                SystemFile,
                SystemContent);

            TryDeleteKnownFile(
                LockedFile,
                LockedContent);

            TryDeleteKnownFile(
                SwapFile,
                OriginalContent);

            TryDeleteKnownFile(
                SwapFile,
                ReplacementContent);

            TryDeleteKnownFile(
                MovedOriginalFile,
                OriginalContent);

            TryDeleteEmptyDirectory(KeepDirectory);
            TryDeleteEmptyDirectory(TargetDirectory);
            TryDeleteEmptyDirectory(RootDirectory);
        }

        private static void EnsurePlainDirectory(
            string path)
        {
            if (!Directory.Exists(path))
            {
                Require(
                    !File.Exists(path),
                    "Ein Sandboxpfad ist "
                    + "bereits durch eine Datei belegt.");

                Directory.CreateDirectory(path);
            }

            Require(
                !IsReparsePoint(path),
                "Ein Testverzeichnis "
                + "ist ein Reparse Point: "
                + path);
        }

        private static bool IsReparsePoint(
            string path)
        {
            return
                (File.GetAttributes(path)
                    & FileAttributes.ReparsePoint) != 0;
        }

        private static void TryDeleteKnownFile(
            string path,
            string expectedContent)
        {
            try
            {
                if (!File.Exists(path)
                    || IsReparsePoint(path))
                {
                    return;
                }

                if (File.ReadAllText(path)
                    != expectedContent)
                {
                    return;
                }

                var attributes =
                    File.GetAttributes(path);

                if ((attributes & FileAttributes.ReadOnly) != 0
                    || (attributes & FileAttributes.System) != 0)
                {
                    File.SetAttributes(
                        path,
                        attributes
                            & ~FileAttributes.ReadOnly
                            & ~FileAttributes.System);
                }

                File.Delete(path);
            }
            catch (IOException)
            {
                // Keine erzwungene Entfernung.
            }
            catch (UnauthorizedAccessException)
            {
                // Keine Rechteausweitung.
            }
        }

        private static void TryDeleteEmptyDirectory(
            string path)
        {
            try
            {
                if (Directory.Exists(path)
                    && !IsReparsePoint(path))
                {
                    Directory.Delete(
                        path,
                        recursive: false);
                }
            }
            catch (IOException)
            {
                // Kein erzwungenes Entfernen.
            }
            catch (UnauthorizedAccessException)
            {
                // Keine Rechteausweitung.
            }
        }
    }

    // =============================================
    // TESTHILFSMETHODEN
    // =============================================

    private static void Execute(
        string title,
        Action test,
        ICollection<string> results)
    {
        try
        {
            test();

            results.Add(
                "OK: " + title);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Handle-Löschtest fehlgeschlagen: "
                + title
                + Environment.NewLine
                + exception.Message,
                exception);
        }
    }

    private static void Require(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

#endif
