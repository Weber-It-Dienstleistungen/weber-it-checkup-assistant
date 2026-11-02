
#if DEBUG

using System;
using System.IO;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.13.36a
///
/// Erzeugt ausschließlich für Debug-Tests eine
/// isolierte Variante des produktiven
/// Windows-Temp-Bereinigungsskripts.
///
/// Der produktive Skriptgenerator selbst
/// bleibt unverändert.
///
/// Diese Klasse führt keinen Löschvorgang
/// und keinen PowerShell-Prozess aus.
///
/// Erlaubte Struktur:
///
/// %TEMP%
///   WeberIT.Checkup
///     WindowsTempSandbox
///       GUID
///         Windows
///           Temp
///
/// Marker- und Ergebnisdatei müssen direkt
/// im GUID-Verzeichnis liegen.
///
/// Diese Testhilfe wird in Release-Builds
/// nicht kompiliert.
/// </summary>
internal static class WindowsTemporaryFilesCleanupSandboxScript
{
    /*
     * Der fachliche Originalblock wird ohne
     * umgebende Einrückung hinterlegt.
     *
     * Vor dem Vergleich wird die tatsächliche
     * Einrückung innerhalb des produktiven
     * try-Blocks ergänzt.
     *
     * Der Inhalt muss weiterhin exakt
     * übereinstimmen.
     */
    private const string OriginalWindowsDirectoryBlock =
        """
        $windowsDirectory =
            [System.Environment]::GetFolderPath(
                [System.Environment+SpecialFolder]::Windows)

        if ([string]::IsNullOrWhiteSpace(
            $windowsDirectory))
        {
            $windowsDirectory = $env:WINDIR
        }

        if ([string]::IsNullOrWhiteSpace(
            $windowsDirectory))
        {
            throw 'Das Windows-Verzeichnis konnte nicht bestimmt werden.'
        }
        """;

    private const string SandboxWindowsDirectoryBlock =
        """
        $windowsDirectory =
            '__WEBERIT_SANDBOX_WINDOWS_PATH__'

        if ([string]::IsNullOrWhiteSpace(
            $windowsDirectory))
        {
            throw 'Das isolierte Windows-Testverzeichnis konnte nicht bestimmt werden.'
        }
        """;

    private const string SandboxPathPlaceholder =
        "__WEBERIT_SANDBOX_WINDOWS_PATH__";

    /*
     * Der produktive Originalblock steht
     * innerhalb eines try-Blocks.
     *
     * Nach der C#-Raw-String-Normalisierung
     * besitzt er dort vier zusätzliche
     * Leerzeichen Einrückung.
     */
    private const string ProductionTryIndentation =
        "    ";

    public static string Build(
        string sandboxWindowsDirectory,
        string cancellationMarkerPath,
        string resultFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            sandboxWindowsDirectory);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            cancellationMarkerPath);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            resultFilePath);

        // =====================================
        // TESTPFADE VALIDIEREN
        // =====================================

        var sandboxWindows =
            NormalizeDirectory(
                sandboxWindowsDirectory);

        var sandboxRoot =
            ValidateSandboxStructure(
                sandboxWindows);

        ValidateCommunicationFile(
            cancellationMarkerPath,
            sandboxRoot);

        ValidateCommunicationFile(
            resultFilePath,
            sandboxRoot);

        if (string.Equals(
                Path.GetFullPath(cancellationMarkerPath),
                Path.GetFullPath(resultFilePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Abbruchmarker und Ergebnisdatei "
                + "dürfen nicht identisch sein.");
        }

        // =====================================
        // PRODUKTIVES SKRIPT ERZEUGEN
        // =====================================

        /*
         * Die produktive Bereinigungslogik
         * bleibt unsere einzige Quelle.
         *
         * Für den Sandboxmodus wird keine
         * zweite Löschlogik gepflegt.
         */
        var productionScript =
            NormalizeLineEndings(
                WindowsTemporaryFilesCleanupPowerShellScript
                    .Build(
                        cancellationMarkerPath,
                        resultFilePath));

        // =====================================
        // ORIGINALBLOCK EXAKT ERKENNEN
        // =====================================

        /*
         * Die eigentliche Korrektur:
         *
         * Beide Textblöcke erhalten die
         * zusätzliche Einrückung, die
         * innerhalb des produktiven
         * try-Blocks tatsächlich vorliegt.
         *
         * Leere Zeilen bleiben leer.
         */
        var expectedOriginalBlock =
            IndentForProductionTryBlock(
                OriginalWindowsDirectoryBlock);

        var firstOccurrence =
            productionScript.IndexOf(
                expectedOriginalBlock,
                StringComparison.Ordinal);

        if (firstOccurrence < 0)
        {
            throw new InvalidOperationException(
                "Die Windows-Pfadermittlung des "
                + "produktiven Skripts entspricht "
                + "nicht mehr der geprüften Struktur.");
        }

        /*
         * Mehrfach vorhandene Originalblöcke
         * dürfen nicht stillschweigend
         * ersetzt werden.
         */
        var secondOccurrence =
            productionScript.IndexOf(
                expectedOriginalBlock,
                firstOccurrence
                + expectedOriginalBlock.Length,
                StringComparison.Ordinal);

        if (secondOccurrence >= 0)
        {
            throw new InvalidOperationException(
                "Die Windows-Pfadermittlung ist "
                + "mehrfach vorhanden. Der isolierte "
                + "Test wird verweigert.");
        }

        // =====================================
        // SANDBOXPFAD EINSETZEN
        // =====================================

        var escapedSandboxWindows =
            sandboxWindows.Replace(
                "'",
                "''",
                StringComparison.Ordinal);

        var replacementBlock =
            IndentForProductionTryBlock(
                SandboxWindowsDirectoryBlock);

        replacementBlock =
            replacementBlock.Replace(
                SandboxPathPlaceholder,
                escapedSandboxWindows,
                StringComparison.Ordinal);

        /*
         * Ausschließlich der exakt geprüfte
         * Originalblock wird ausgetauscht.
         *
         * Keine anderen PowerShell-Befehle
         * und keine Löschoperationen
         * werden verändert.
         */
        var testScript =
            productionScript.Replace(
                expectedOriginalBlock,
                replacementBlock,
                StringComparison.Ordinal);

        // =====================================
        // ABSCHLIESSENDE SICHERHEITSPRÜFUNG
        // =====================================

        /*
         * Nach der Ersetzung darf das Skript
         * weder auf die produktive Windows-
         * Pfaderkennung zurückgreifen noch
         * einen offenen Platzhalter besitzen.
         */
        if (testScript.Contains(
                "[System.Environment+SpecialFolder]::Windows",
                StringComparison.Ordinal)
            || testScript.Contains(
                "$env:WINDIR",
                StringComparison.Ordinal)
            || testScript.Contains(
                SandboxPathPlaceholder,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Das Testskript enthält noch eine "
                + "produktive Windows-Pfaderkennung "
                + "oder einen nicht ersetzten Platzhalter.");
        }

        if (!testScript.Contains(
                escapedSandboxWindows,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Das Testskript enthält den "
                + "freigegebenen Sandboxpfad nicht.");
        }

        if (string.Equals(
                productionScript,
                testScript,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Der Sandboxmodus hat die "
                + "produktive Pfaderkennung "
                + "nicht verändert.");
        }

        return testScript;
    }

    // =============================================
    // SKRIPTTEXT NORMALISIEREN
    // =============================================

    private static string NormalizeLineEndings(
        string value)
    {
        /*
         * Die Originalprüfung soll nicht
         * davon abhängen, ob Quellcodedateien
         * CRLF oder LF verwenden.
         *
         * Der eigentliche PowerShell-Inhalt
         * wird dadurch nicht verändert.
         */
        return value
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                "\r",
                "\n",
                StringComparison.Ordinal);
    }

    private static string IndentForProductionTryBlock(
        string block)
    {
        var lines =
            NormalizeLineEndings(block)
                .Split('\n');

        for (var index = 0;
             index < lines.Length;
             index++)
        {
            /*
             * Leere Zeilen nicht einrücken.
             *
             * Das ist entscheidend:
             * Im produktiven Raw-String
             * besitzen Leerzeilen keine
             * führenden Leerzeichen.
             */
            if (lines[index].Length == 0)
            {
                continue;
            }

            lines[index] =
                ProductionTryIndentation
                + lines[index];
        }

        return string.Join(
            "\n",
            lines);
    }

    // =============================================
    // TESTBEREICH VALIDIEREN
    // =============================================

    private static string ValidateSandboxStructure(
        string sandboxWindows)
    {
        var temporaryRoot =
            NormalizeDirectory(
                Path.GetTempPath());

        if (IsNetworkPath(temporaryRoot))
        {
            throw new InvalidOperationException(
                "Ein isolierter Bereinigungstest "
                + "darf keinen Netzwerkpfad verwenden.");
        }

        var allowedBase =
            NormalizeDirectory(
                Path.Combine(
                    temporaryRoot,
                    "WeberIT.Checkup",
                    "WindowsTempSandbox"));

        /*
         * Die Struktur muss exakt aus
         * GUID + Windows bestehen.
         */
        var relative =
            Path.GetRelativePath(
                allowedBase,
                sandboxWindows);

        var segments =
            relative.Split(
                [
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                ],
                StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length != 2
            || !Guid.TryParseExact(
                segments[0],
                "N",
                out _)
            || !string.Equals(
                segments[1],
                "Windows",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Der Windows-Testordner liegt nicht "
                + "innerhalb der vorgeschriebenen "
                + "isolierten Sandboxstruktur.");
        }

        var sandboxRoot =
            NormalizeDirectory(
                Path.Combine(
                    allowedBase,
                    segments[0]));

        var expectedWindows =
            NormalizeDirectory(
                Path.Combine(
                    sandboxRoot,
                    "Windows"));

        if (!string.Equals(
                sandboxWindows,
                expectedWindows,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Der angegebene Windows-Testordner "
                + "ist nicht eindeutig freigegeben.");
        }

        var targetDirectory =
            NormalizeDirectory(
                Path.Combine(
                    expectedWindows,
                    "Temp"));

        var realWindowsTemp =
            NormalizeDirectory(
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Windows),
                    "Temp"));

        if (string.Equals(
                targetDirectory,
                realWindowsTemp,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Ein Test darf niemals den echten "
                + "Windows-Temp-Ordner verwenden.");
        }

        /*
         * Jeder beteiligte Testordner muss
         * bereits vorhanden sein.
         *
         * Reparse Points werden ausdrücklich
         * abgelehnt.
         */
        ValidateRealDirectory(
            temporaryRoot);

        ValidateRealDirectory(
            Path.Combine(
                temporaryRoot,
                "WeberIT.Checkup"));

        ValidateRealDirectory(
            allowedBase);

        ValidateRealDirectory(
            sandboxRoot);

        ValidateRealDirectory(
            expectedWindows);

        ValidateRealDirectory(
            targetDirectory);

        return sandboxRoot;
    }

    private static void ValidateRealDirectory(
        string path)
    {
        if (!Directory.Exists(path))
        {
            throw new InvalidOperationException(
                "Ein erforderlicher Sandboxordner "
                + "ist nicht vorhanden: "
                + path);
        }

        var attributes =
            File.GetAttributes(path);

        if ((attributes
            & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Der isolierte Testbereich "
                + "enthält einen nicht erlaubten "
                + "Dateisystemverweis: "
                + path);
        }

        if ((attributes
            & FileAttributes.Directory) == 0)
        {
            throw new InvalidOperationException(
                "Der erwartete Sandboxordner "
                + "ist kein Verzeichnis: "
                + path);
        }
    }

    private static void ValidateCommunicationFile(
        string filePath,
        string sandboxRoot)
    {
        var fullPath =
            Path.GetFullPath(
                filePath);

        var parent =
            Path.GetDirectoryName(
                fullPath);

        if (string.IsNullOrWhiteSpace(parent)
            || !string.Equals(
                NormalizeDirectory(parent),
                sandboxRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Kommunikationsdateien müssen "
                + "direkt im isolierten "
                + "Sandbox-Stammordner liegen.");
        }

        if (File.Exists(fullPath)
            && (File.GetAttributes(fullPath)
                & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Eine Kommunikationsdatei "
                + "ist ein nicht erlaubter "
                + "Dateisystemverweis.");
        }
    }

    private static string NormalizeDirectory(
        string path)
    {
        var fullPath =
            Path.GetFullPath(
                path);

        var root =
            Path.GetPathRoot(
                fullPath);

        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException(
                "Der Sandboxpfad besitzt "
                + "kein Stammverzeichnis.");
        }

        return string.Equals(
                fullPath,
                root,
                StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
    }

    private static bool IsNetworkPath(
        string path)
    {
        return path.StartsWith(
                   @"\\",
                   StringComparison.Ordinal)
               || path.StartsWith(
                   "//",
                   StringComparison.Ordinal);
    }
}

#endif
