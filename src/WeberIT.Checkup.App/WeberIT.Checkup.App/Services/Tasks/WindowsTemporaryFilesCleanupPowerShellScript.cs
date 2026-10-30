
using System;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.13.24
///
/// Erstellt das kontrollierte PowerShell-Skript
/// für die erhöhte Windows-Temp-Bereinigung.
///
/// Die Klasse führt selbst keine Befehle aus.
///
/// Sicherheitsregeln:
/// - Nur der direkte Windows-Temp-Ordner.
/// - Kein Löschen des Stammordners.
/// - Keine rekursive Zwangslöschung.
/// - Keine Verfolgung erkannter Reparse Points.
/// - Kein Ändern von Dateiattributen.
/// - Keine Übernahme von Berechtigungen.
/// - Keine Beendigung laufender Prozesse.
/// - Kontrollierter Abbruch über Markerdatei.
/// - Getrennte Fehler- und Überspringzähler.
/// </summary>
internal static class WindowsTemporaryFilesCleanupPowerShellScript
{
    private const string CancellationMarkerPlaceholder =
        "__WEBERIT_CANCELLATION_MARKER__";

    private const string ResultFilePlaceholder =
        "__WEBERIT_RESULT_FILE__";

    public static string Build(
        string cancellationMarkerPath,
        string resultFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            cancellationMarkerPath);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            resultFilePath);

        var escapedCancellationMarkerPath =
            EscapePowerShellLiteral(
                cancellationMarkerPath);

        var escapedResultFilePath =
            EscapePowerShellLiteral(
                resultFilePath);

        /*
         * Der geprüfte Klassifizierungshelfer
         * wird vor dem eigentlichen Skript
         * eingebunden.
         *
         * Keine dynamisch erzeugten
         * Löschbefehle oder Zielpfade.
         */
        var script =
            WindowsTemporaryFilesSkipReasonPowerShellScript.Source
            + Environment.NewLine
            + CleanupScript;

        return script
            .Replace(
                CancellationMarkerPlaceholder,
                escapedCancellationMarkerPath,
                StringComparison.Ordinal)
            .Replace(
                ResultFilePlaceholder,
                escapedResultFilePath,
                StringComparison.Ordinal);
    }

    private static string EscapePowerShellLiteral(
        string value)
    {
        return value.Replace(
            "'",
            "''",
            StringComparison.Ordinal);
    }

    private const string CleanupScript =
        """
        $ErrorActionPreference = 'Stop'

        $cancellationMarkerPath = '__WEBERIT_CANCELLATION_MARKER__'
        $resultFilePath = '__WEBERIT_RESULT_FILE__'

        $result = [ordered]@{
            WasStarted = $false
            WasCancelled = $false
            TargetPath = ''
            DeletedFileCount = [int64]0
            DeletedDirectoryCount = [int64]0
            DeletedSizeBytes = [uint64]0
            FailedEntryCount = [int64]0
            SkippedEntryCount = [int64]0
            SkippedEntryReasons = @()
            ErrorMessage = ''
        }

        $script:skipReasonCounts = @{}

        function Test-CancellationRequested
        {
            return [System.IO.File]::Exists(
                $cancellationMarkerPath)
        }

        function Write-WorkerResult
        {
            param(
                [System.Collections.IDictionary]$WorkerResult
            )

            $json =
                $WorkerResult |
                    ConvertTo-Json -Compress -Depth 4

            [System.IO.File]::WriteAllText(
                $resultFilePath,
                $json,
                [System.Text.Encoding]::UTF8)

            [System.Console]::Out.WriteLine(
                $json)
        }

        $exitCode = 0

        try
        {
            # =====================================
            # WINDOWS-VERZEICHNIS BESTIMMEN
            # =====================================

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

            $trimCharacters = [char[]]@(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar)

            $normalizedWindowsDirectory =
                [System.IO.Path]::GetFullPath(
                    $windowsDirectory).TrimEnd(
                        $trimCharacters)

            $targetPath =
                [System.IO.Path]::GetFullPath(
                    [System.IO.Path]::Combine(
                        $normalizedWindowsDirectory,
                        'Temp')).TrimEnd(
                            $trimCharacters)

            # =====================================
            # ZIELPFAD SICHERN
            # =====================================

            $targetRoot =
                [System.IO.Path]::GetPathRoot(
                    $targetPath)

            if ([string]::IsNullOrWhiteSpace(
                $targetRoot))
            {
                throw 'Der Windows-Temp-Pfad besitzt kein Stammverzeichnis.'
            }

            if ([string]::Equals(
                $targetPath,
                $targetRoot,
                [System.StringComparison]::OrdinalIgnoreCase))
            {
                throw 'Der Windows-Temp-Pfad entspricht einem Laufwerksstamm.'
            }

            $targetParent =
                [System.IO.Directory]::GetParent(
                    $targetPath)

            if ($null -eq $targetParent)
            {
                throw 'Der übergeordnete Windows-Ordner konnte nicht bestimmt werden.'
            }

            $normalizedTargetParent =
                $targetParent.FullName.TrimEnd(
                    $trimCharacters)

            if (-not [string]::Equals(
                $normalizedTargetParent,
                $normalizedWindowsDirectory,
                [System.StringComparison]::OrdinalIgnoreCase))
            {
                throw 'Der Zielbereich liegt nicht direkt im Windows-Verzeichnis.'
            }

            $targetDirectoryName =
                [System.IO.Path]::GetFileName(
                    $targetPath)

            if (-not [string]::Equals(
                $targetDirectoryName,
                'Temp',
                [System.StringComparison]::OrdinalIgnoreCase))
            {
                throw 'Der Zielbereich entspricht nicht dem Windows-Temp-Ordner.'
            }

            $result.TargetPath = $targetPath
            $result.WasStarted = $true

            # =====================================
            # ABRUCH VOR DEM BEGINN
            # =====================================

            if (Test-CancellationRequested)
            {
                $result.WasCancelled = $true
            }
            elseif ([System.IO.Directory]::Exists(
                $targetPath))
            {
                # =================================
                # STAMMORDNER SICHERN
                # =================================

                $targetAttributes =
                    [System.IO.File]::GetAttributes(
                        $targetPath)

                if (($targetAttributes -band
                    [System.IO.FileAttributes]::ReparsePoint) -ne 0)
                {
                    throw 'Der Windows-Temp-Stammordner ist ein Umleitungs- oder Verknüpfungspunkt.'
                }

                $rootPrefix =
                    $targetPath +
                    [System.IO.Path]::DirectorySeparatorChar

                # =================================
                # ITERATIVE VERARBEITUNG
                # =================================

                $pendingEntries =
                    New-Object System.Collections.Stack

                try
                {
                    $rootEntries =
                        [System.IO.Directory]::GetFileSystemEntries(
                            $targetPath)
                }
                catch
                {
                    $rootEntries = @()

                    Record-FileSystemException `
                        -Exception $_.Exception

                    if ($result.FailedEntryCount -gt 0)
                    {
                        $result.ErrorMessage =
                            'Der Inhalt des Windows-Temp-Ordners konnte nicht vollständig aufgelistet werden.'
                    }
                }

                foreach ($rootEntry in $rootEntries)
                {
                    $workItem = [pscustomobject]@{
                        Path = $rootEntry
                        DeleteAfterChildren = $false
                    }

                    $pendingEntries.Push(
                        $workItem)
                }

                while ($pendingEntries.Count -gt 0)
                {
                    if (Test-CancellationRequested)
                    {
                        $result.WasCancelled = $true
                        break
                    }

                    $workItem =
                        $pendingEntries.Pop()

                    $candidatePath =
                        [System.IO.Path]::GetFullPath(
                            [string]$workItem.Path)

                    # =============================
                    # PFADGRENZE PRÜFEN
                    # =============================

                    if (-not $candidatePath.StartsWith(
                        $rootPrefix,
                        [System.StringComparison]::OrdinalIgnoreCase))
                    {
                        throw 'Ein zu verarbeitender Eintrag liegt außerhalb des Windows-Temp-Ordners.'
                    }

                    # =============================
                    # LEEREN ORDNER ENTFERNEN
                    # =============================

                    if ([bool]$workItem.DeleteAfterChildren)
                    {
                        $finalDirectoryAttributes = $null

                        try
                        {
                            # Erneute Attributprüfung
                            # direkt vor der Entfernung.

                            $finalDirectoryAttributes =
                                [System.IO.File]::GetAttributes(
                                    $candidatePath)

                            $protectedReason =
                                Get-ProtectedEntryReason `
                                    -Attributes $finalDirectoryAttributes

                            if (-not [string]::IsNullOrWhiteSpace(
                                $protectedReason))
                            {
                                Add-SkippedEntry `
                                    -Reason $protectedReason

                                continue
                            }

                            [System.IO.Directory]::Delete(
                                $candidatePath,
                                $false)

                            $result.DeletedDirectoryCount =
                                [int64]$result.DeletedDirectoryCount + 1
                        }
                        catch
                        {
                            Record-FileSystemException `
                                -Exception $_.Exception `
                                -KnownAttributes $finalDirectoryAttributes `
                                -AllowDirectoryNotEmpty $true
                        }

                        continue
                    }

                    # =============================
                    # ATTRIBUTE ERMITTELN
                    # =============================

                    try
                    {
                        $attributes =
                            [System.IO.File]::GetAttributes(
                                $candidatePath)
                    }
                    catch
                    {
                        Record-FileSystemException `
                            -Exception $_.Exception

                        continue
                    }

                    # =============================
                    # GESCHÜTZTE EINTRÄGE
                    # =============================

                    $protectedReason =
                        Get-ProtectedEntryReason `
                            -Attributes $attributes

                    if (-not [string]::IsNullOrWhiteSpace(
                        $protectedReason))
                    {
                        Add-SkippedEntry `
                            -Reason $protectedReason

                        continue
                    }

                    $isDirectory =
                        ($attributes -band
                        [System.IO.FileAttributes]::Directory) -ne 0

                    # =============================
                    # DATEI BEHANDELN
                    # =============================

                    if (-not $isDirectory)
                    {
                        [uint64]$fileSize = 0

                        try
                        {
                            # Attributprüfung unmittelbar
                            # vor dem Löschversuch.

                            $finalFileAttributes =
                                [System.IO.File]::GetAttributes(
                                    $candidatePath)

                            $finalProtectionReason =
                                Get-ProtectedEntryReason `
                                    -Attributes $finalFileAttributes

                            if (-not [string]::IsNullOrWhiteSpace(
                                $finalProtectionReason))
                            {
                                Add-SkippedEntry `
                                    -Reason $finalProtectionReason

                                continue
                            }

                            try
                            {
                                $fileInformation =
                                    New-Object System.IO.FileInfo(
                                        $candidatePath)

                                if ($fileInformation.Exists -and
                                    $fileInformation.Length -gt 0)
                                {
                                    $fileSize =
                                        [uint64]$fileInformation.Length
                                }
                            }
                            catch
                            {
                                $fileSize = 0
                            }

                            [System.IO.File]::Delete(
                                $candidatePath)

                            $result.DeletedFileCount =
                                [int64]$result.DeletedFileCount + 1

                            [uint64]$currentDeletedSize =
                                [uint64]$result.DeletedSizeBytes

                            if ($fileSize -gt
                                ([uint64]::MaxValue -
                                $currentDeletedSize))
                            {
                                $result.DeletedSizeBytes =
                                    [uint64]::MaxValue
                            }
                            else
                            {
                                $result.DeletedSizeBytes =
                                    [uint64](
                                        $currentDeletedSize +
                                        $fileSize)
                            }
                        }
                        catch
                        {
                            Record-FileSystemException `
                                -Exception $_.Exception `
                                -KnownAttributes $attributes
                        }

                        continue
                    }

                    # =============================
                    # UNTERORDNER AUFLISTEN
                    # =============================

                    try
                    {
                        # Auch vor dem Betreten
                        # eines Ordners erneut prüfen.

                        $currentDirectoryAttributes =
                            [System.IO.File]::GetAttributes(
                                $candidatePath)

                        $currentProtectionReason =
                            Get-ProtectedEntryReason `
                                -Attributes $currentDirectoryAttributes

                        if (-not [string]::IsNullOrWhiteSpace(
                            $currentProtectionReason))
                        {
                            Add-SkippedEntry `
                                -Reason $currentProtectionReason

                            continue
                        }

                        $childEntries =
                            [System.IO.Directory]::GetFileSystemEntries(
                                $candidatePath)
                    }
                    catch
                    {
                        Record-FileSystemException `
                            -Exception $_.Exception `
                            -KnownAttributes $attributes

                        continue
                    }

                    # =============================
                    # NACHKONTROLLE EINPLANEN
                    # =============================

                    $directoryWorkItem =
                        [pscustomobject]@{
                            Path = $candidatePath
                            DeleteAfterChildren = $true
                        }

                    $pendingEntries.Push(
                        $directoryWorkItem)

                    foreach ($childEntry in $childEntries)
                    {
                        $childWorkItem =
                            [pscustomobject]@{
                                Path = $childEntry
                                DeleteAfterChildren = $false
                            }

                        $pendingEntries.Push(
                            $childWorkItem)
                    }
                }
            }
        }
        catch
        {
            $exitCode = 1

            $technicalMessage =
                $_.Exception.Message

            if ([string]::IsNullOrWhiteSpace(
                $technicalMessage))
            {
                $result.ErrorMessage =
                    'Die Windows-Temp-Bereinigung wurde unerwartet abgebrochen.'
            }
            else
            {
                $result.ErrorMessage =
                    $technicalMessage
            }
        }
        finally
        {
            # =====================================
            # URSACHENAUFSCHLÜSSELUNG
            # =====================================

            try
            {
                $result.SkippedEntryReasons =
                    @(Get-SkippedReasonRecords)
            }
            catch
            {
                $exitCode = 1

                $result.ErrorMessage =
                    'Die Überspringursachen konnten nicht vollständig dokumentiert werden.'
            }

            # =====================================
            # ERGEBNIS ÜBERTRAGEN
            # =====================================

            try
            {
                Write-WorkerResult `
                    -WorkerResult $result
            }
            catch
            {
                $resultWriteError =
                    $_.Exception.Message

                $resultWriteMessage =
                    'Das technische Bereinigungsergebnis konnte nicht gespeichert werden: {0}' `
                    -f $resultWriteError

                [System.Console]::Error.WriteLine(
                    $resultWriteMessage)

                $exitCode = 1
            }
        }

        exit $exitCode
        """;
}
