
#if DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.13.22a
///
/// Verhaltenstests des PowerShell-Helfers
/// für die erhöhte Windows-Temp-Bereinigung.
///
/// Das Testskript wird über stdin übertragen,
/// damit keine Windows-Kommandozeilengrenze
/// überschritten wird.
///
/// Kein produktives Bereinigungsskript.
/// Keine Löschvorgänge.
/// Keine Administratoranforderung.
/// Keine temporären Skriptdateien.
/// </summary>
public static class
    WindowsTemporaryFilesSkipReasonPowerShellRegressionTests
{
    private const int ExpectedTestCount = 16;

    private const int ProcessTimeoutMilliseconds = 30000;

    /// <summary>
    /// Kurzer PowerShell-Startbefehl.
    ///
    /// Das eigentliche Testskript wird als
    /// Base64-kodierter UTF-16-Text über
    /// die Standardeingabe empfangen.
    ///
    /// Dadurch bleiben auch Sonderzeichen
    /// unabhängig von der Windows-Codepage
    /// erhalten.
    ///
    /// Die Ausführung erfolgt direkt im
    /// Speicher ohne Skriptdatei.
    /// </summary>
    private const string BootstrapScript =
        """
        $ErrorActionPreference = 'Stop'

        try
        {
            $encodedScript =
                [System.Console]::In.ReadToEnd()

            $scriptBytes =
                [System.Convert]::FromBase64String(
                    $encodedScript)

            $scriptText =
                [System.Text.Encoding]::Unicode.GetString(
                    $scriptBytes)

            $scriptBlock =
                [System.Management.Automation.ScriptBlock]::Create(
                    $scriptText)

            & $scriptBlock

            exit 0
        }
        catch
        {
            [System.Console]::Error.WriteLine(
                $_.Exception.ToString())

            exit 1
        }
        """;

    public static string RunAll()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Die PowerShell-Verhaltenstests "
                + "benötigen Windows.");
        }

        var powerShellPath =
            ResolvePowerShellPath();

        /*
         * Nur der Klassifizierungshelfer
         * und die künstlichen Testszenarien
         * werden zusammengesetzt.
         *
         * BuildCleanupScript des produktiven
         * Executors wird nicht aufgerufen.
         */
        var testScript =
            WindowsTemporaryFilesSkipReasonPowerShellScript.Source
            + Environment.NewLine
            + TestScenarios;

        /*
         * Großes Testskript:
         * Base64 über stdin.
         *
         * Der kurze Bootstrap-Befehl wird
         * weiterhin über -EncodedCommand
         * übergeben.
         */
        var encodedTestScript =
            Convert.ToBase64String(
                Encoding.Unicode.GetBytes(
                    testScript));

        var encodedBootstrap =
            Convert.ToBase64String(
                Encoding.Unicode.GetBytes(
                    BootstrapScript));

        if (encodedBootstrap.Length > 30000)
        {
            throw new InvalidOperationException(
                "Der PowerShell-Startbefehl "
                + "überschreitet die erlaubte "
                + "Kommandozeilengrenze.");
        }

        var startInfo =
            new ProcessStartInfo
            {
                FileName = powerShellPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(encodedBootstrap);

        using var process =
            new Process
            {
                StartInfo = startInfo
            };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "Die nicht erhöhte PowerShell-"
                + "Testausführung konnte nicht starten.");
        }

        /*
         * Beide Ausgabekanäle sofort
         * parallel lesen.
         *
         * Dadurch vermeiden wir blockierte
         * Ausgabepuffer während der
         * Übertragung über stdin.
         */
        var standardOutputTask =
            process.StandardOutput.ReadToEndAsync();

        var standardErrorTask =
            process.StandardError.ReadToEndAsync();

        /*
         * Das gesamte Testskript wird als
         * reine ASCII-Base64-Zeichenfolge
         * übertragen.
         *
         * Close signalisiert PowerShell
         * das Ende der Eingabe.
         */
        process.StandardInput.Write(
            encodedTestScript);

        process.StandardInput.Close();

        if (!process.WaitForExit(
                ProcessTimeoutMilliseconds))
        {
            try
            {
                process.Kill(
                    entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Prozess ist bereits beendet.
            }

            throw new InvalidOperationException(
                "Die PowerShell-Verhaltenstests "
                + "haben die erlaubte Laufzeit "
                + "überschritten.");
        }

        var standardOutput =
            standardOutputTask
                .GetAwaiter()
                .GetResult();

        var standardError =
            standardErrorTask
                .GetAwaiter()
                .GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Die PowerShell-Verhaltenstests "
                + "wurden mit einem Fehler beendet."
                + Environment.NewLine
                + "Exitcode: "
                + process.ExitCode
                + Environment.NewLine
                + standardError
                + Environment.NewLine
                + standardOutput);
        }

        var outputLines =
            standardOutput
                .Split(
                    [
                        "\r\n",
                        "\n",
                        "\r"
                    ],
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(
                    line => line.Trim())
                .ToList();

        var passedTests =
            outputLines
                .Where(
                    line =>
                        line.StartsWith(
                            "OK: ",
                            StringComparison.Ordinal))
                .ToList();

        if (passedTests.Count != ExpectedTestCount
            || !outputLines.Contains(
                $"SUCCESS: {ExpectedTestCount}"))
        {
            throw new InvalidOperationException(
                "Die PowerShell-Testausführung "
                + "lieferte kein vollständiges "
                + "Erfolgsergebnis."
                + Environment.NewLine
                + standardOutput
                + Environment.NewLine
                + standardError);
        }

        return
            $"Alle {passedTests.Count} PowerShell-"
            + "Verhaltenstests erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                passedTests);
    }

    private static string ResolvePowerShellPath()
    {
        var windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        if (string.IsNullOrWhiteSpace(
                windowsDirectory))
        {
            throw new InvalidOperationException(
                "Das Windows-Verzeichnis konnte "
                + "nicht bestimmt werden.");
        }

        var powerShellPath =
            Path.Combine(
                windowsDirectory,
                "System32",
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");

        if (!File.Exists(powerShellPath))
        {
            throw new FileNotFoundException(
                "Die lokale Windows-PowerShell "
                + "wurde nicht gefunden.",
                powerShellPath);
        }

        return powerShellPath;
    }

    // =============================================
    // ISOLIERTE POWERSHELL-TESTSZENARIEN
    // =============================================

    private const string TestScenarios =
        """
        $ErrorActionPreference = 'Stop'

        $script:passedTests =
            New-Object 'System.Collections.Generic.List[string]'

        function Confirm-Test
        {
            param(
                [string]$Name,
                [bool]$Condition
            )

            if (-not $Condition)
            {
                throw ('PowerShell-Test fehlgeschlagen: ' + $Name)
            }

            [void]$script:passedTests.Add($Name)
        }

        # 1. Normale Attribute

        $reason =
            Get-ProtectedEntryReason `
                -Attributes ([System.IO.FileAttributes]::Normal)

        Confirm-Test `
            -Name 'Normale Dateiattribute' `
            -Condition ($null -eq $reason)

        # 2. Reparse Point

        $reason =
            Get-ProtectedEntryReason `
                -Attributes ([System.IO.FileAttributes]::ReparsePoint)

        Confirm-Test `
            -Name 'Reparse Point erkennen' `
            -Condition ($reason -eq 'ReparsePoint')

        # 3. Systemgeschuetzt

        $reason =
            Get-ProtectedEntryReason `
                -Attributes ([System.IO.FileAttributes]::System)

        Confirm-Test `
            -Name 'Systemschutz erkennen' `
            -Condition ($reason -eq 'SystemProtected')

        # 4. Schreibschutz

        $reason =
            Get-ProtectedEntryReason `
                -Attributes ([System.IO.FileAttributes]::ReadOnly)

        Confirm-Test `
            -Name 'Schreibschutz erkennen' `
            -Condition ($reason -eq 'ReadOnly')

        # 5. Bereits entfernte Datei

        $missingFile =
            New-Object System.IO.FileNotFoundException(
                'Simulierte fehlende Datei')

        $reason =
            Get-SkipReasonFromException `
                -Exception $missingFile

        Confirm-Test `
            -Name 'Fehlende Datei erkennen' `
            -Condition ($reason -eq 'AlreadyMissing')

        # 6. Bereits entfernter Ordner

        $missingDirectory =
            New-Object System.IO.DirectoryNotFoundException(
                'Simulierter fehlender Ordner')

        $reason =
            Get-SkipReasonFromException `
                -Exception $missingDirectory

        Confirm-Test `
            -Name 'Fehlenden Ordner erkennen' `
            -Condition ($reason -eq 'AlreadyMissing')

        # 7. ERROR_SHARING_VIOLATION

        $sharingException =
            New-Object System.IO.IOException(
                'Simulierte Prozesssperre',
                -2147024864)

        $reason =
            Get-SkipReasonFromException `
                -Exception $sharingException

        Confirm-Test `
            -Name 'Prozesssperre HRESULT 32' `
            -Condition ($reason -eq 'LockedByProcess')

        # 8. ERROR_LOCK_VIOLATION

        $lockException =
            New-Object System.IO.IOException(
                'Simulierte Dateisperre',
                -2147024863)

        $reason =
            Get-SkipReasonFromException `
                -Exception $lockException

        Confirm-Test `
            -Name 'Dateisperre HRESULT 33' `
            -Condition ($reason -eq 'LockedByProcess')

        # 9. Nicht leerer Ordner:
        # Ohne besondere Freigabe kein Skip.

        $nonEmptyException =
            New-Object System.IO.IOException(
                'Simulierter nicht leerer Ordner',
                -2147024751)

        $reason =
            Get-SkipReasonFromException `
                -Exception $nonEmptyException

        Confirm-Test `
            -Name 'Nicht leerer Ordner ohne Freigabe bleibt Fehler' `
            -Condition ($null -eq $reason)

        # 10. Nicht leerer Ordner:
        # Nur beim nichtrekursiven Loeschen ein Skip.

        $reason =
            Get-SkipReasonFromException `
                -Exception $nonEmptyException `
                -AllowDirectoryNotEmpty $true

        Confirm-Test `
            -Name 'Nicht leerer Ordner mit Freigabe' `
            -Condition ($reason -eq 'DirectoryNotEmpty')

        # 11. Unklarer Zugriff verweigert

        $accessDenied =
            New-Object System.UnauthorizedAccessException(
                'Simulierter Zugriff verweigert')

        $reason =
            Get-SkipReasonFromException `
                -Exception $accessDenied

        Confirm-Test `
            -Name 'Unklarer Zugriff bleibt Fehler' `
            -Condition ($null -eq $reason)

        # 12. Unbekannter E/A-Fehler

        $unknownIo =
            New-Object System.IO.IOException(
                'Simulierter unbekannter E/A-Fehler',
                -2147024873)

        $reason =
            Get-SkipReasonFromException `
                -Exception $unknownIo

        Confirm-Test `
            -Name 'Unbekannter E/A-Fehler bleibt Fehler' `
            -Condition ($null -eq $reason)

        # 13. Bekannter Schreibschutz
        # bei Zugriffsverweigerung

        $reason =
            Get-SkipReasonFromException `
                -Exception $accessDenied `
                -KnownAttributes ([System.IO.FileAttributes]::ReadOnly)

        Confirm-Test `
            -Name 'Bekannter Schreibschutz bleibt Schutzfall' `
            -Condition ($reason -eq 'ReadOnly')

        # 14. Ursachenzaehler

        $script:skipReasonCounts = @{}

        $result = [ordered]@{
            SkippedEntryCount = [int64]0
            FailedEntryCount = [int64]0
        }

        Add-SkippedEntry -Reason 'ReadOnly'
        Add-SkippedEntry -Reason 'LockedByProcess'
        Add-SkippedEntry -Reason 'ReadOnly'

        $records = @(Get-SkippedReasonRecords)

        $readOnlyRecords = @(
            $records | Where-Object {
                $_.Reason -eq 'ReadOnly'
            }
        )

        $lockedRecords = @(
            $records | Where-Object {
                $_.Reason -eq 'LockedByProcess'
            }
        )

        $validCounts =
            $result.SkippedEntryCount -eq 3 -and
            $records.Count -eq 2 -and
            $readOnlyRecords.Count -eq 1 -and
            $lockedRecords.Count -eq 1 -and
            $readOnlyRecords[0].Count -eq 2 -and
            $lockedRecords[0].Count -eq 1

        Confirm-Test `
            -Name 'Getrennte Ursachenzaehler' `
            -Condition $validCounts

        # 15. Dokumentierter Ausnahmefall
        # darf keinen technischen Fehler erzeugen.

        Record-FileSystemException `
            -Exception $sharingException

        $validSkip =
            $result.SkippedEntryCount -eq 4 -and
            $result.FailedEntryCount -eq 0 -and
            $script:skipReasonCounts['LockedByProcess'] -eq 2

        Confirm-Test `
            -Name 'Erwartete Ausnahme bleibt ohne Fehler' `
            -Condition $validSkip

        # 16. Echter technischer Fehler
        # muss als solcher erhalten bleiben.

        Record-FileSystemException `
            -Exception $unknownIo

        $validFailure =
            $result.SkippedEntryCount -eq 4 -and
            $result.FailedEntryCount -eq 1

        Confirm-Test `
            -Name 'Unbekannter Fehler bleibt technischer Fehler' `
            -Condition $validFailure

        if ($script:passedTests.Count -ne 16)
        {
            throw 'Unerwartete Anzahl PowerShell-Tests.'
        }

        foreach ($testName in $script:passedTests)
        {
            [System.Console]::Out.WriteLine(
                'OK: ' + $testName)
        }

        [System.Console]::Out.WriteLine(
            'SUCCESS: 16')
        """;
}

#endif
