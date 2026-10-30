
namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.13.20
///
/// PowerShell-Hilfsfunktionen zur
/// Klassifizierung von Dateisystemproblemen
/// während der erhöhten Windows-Temp-
/// Bereinigung.
///
/// Diese Klasse führt selbst keine
/// PowerShell-Befehle aus.
///
/// Der Quelltext wird erst in einem
/// späteren Schritt in das bestehende
/// Bereinigungsskript eingebunden.
///
/// Die Funktionen:
/// - verändern keine Dateiattribute,
/// - ändern keine Berechtigungen,
/// - beenden keine Prozesse,
/// - erzwingen keine Löschoperationen.
/// </summary>
internal static class
    WindowsTemporaryFilesSkipReasonPowerShellScript
{
    public const string Source =
        """
        function Get-ProtectedEntryReason
        {
            param(
                [System.IO.FileAttributes]$Attributes
            )

            if (($Attributes -band
                 [System.IO.FileAttributes]::ReparsePoint) -ne 0)
            {
                return 'ReparsePoint'
            }

            if (($Attributes -band
                 [System.IO.FileAttributes]::System) -ne 0)
            {
                return 'SystemProtected'
            }

            if (($Attributes -band
                 [System.IO.FileAttributes]::ReadOnly) -ne 0)
            {
                return 'ReadOnly'
            }

            return $null
        }

        function Get-SkipReasonFromException
        {
            param(
                [Parameter(Mandatory = $true)]
                [System.Exception]$Exception,

                [object]$KnownAttributes = $null,

                [bool]$AllowDirectoryNotEmpty = $false
            )

            $actualException = $Exception

            # PowerShell kann Ausnahmen statischer
            # .NET-Aufrufe in eine
            # MethodInvocationException einpacken.
            #
            # Wir betrachten die eigentliche
            # zugrunde liegende Ausnahme.
            while (
                $null -ne $actualException.InnerException -and
                (
                    $actualException -is
                        [System.Management.Automation.MethodInvocationException] -or
                    $actualException -is
                        [System.Reflection.TargetInvocationException]
                )
            )
            {
                $actualException =
                    $actualException.InnerException
            }

            if (
                $actualException -is
                    [System.IO.FileNotFoundException] -or
                $actualException -is
                    [System.IO.DirectoryNotFoundException]
            )
            {
                return 'AlreadyMissing'
            }

            if ($null -ne $KnownAttributes)
            {
                $protectionReason =
                    Get-ProtectedEntryReason `
                        -Attributes (
                            [System.IO.FileAttributes]$KnownAttributes)

                if (
                    -not [string]::IsNullOrWhiteSpace(
                        $protectionReason)
                )
                {
                    return $protectionReason
                }
            }

            if (
                $actualException -is
                    [System.IO.IOException]
            )
            {
                $win32ErrorCode =
                    [int](
                        $actualException.HResult -band 65535
                    )

                # ERROR_SHARING_VIOLATION = 32
                # ERROR_LOCK_VIOLATION    = 33

                if (
                    $win32ErrorCode -eq 32 -or
                    $win32ErrorCode -eq 33
                )
                {
                    return 'LockedByProcess'
                }

                # ERROR_DIR_NOT_EMPTY = 145
                #
                # Dieser Zustand ist nur bei
                # einer ausdrücklich nichtrekursiven
                # Ordnerlöschung ein erwarteter
                # Ausschluss.
                if (
                    $AllowDirectoryNotEmpty -and
                    $win32ErrorCode -eq 145
                )
                {
                    return 'DirectoryNotEmpty'
                }
            }

            # Ungeklärte Berechtigungsprobleme,
            # unbekannte E/A-Fehler und andere
            # Ausnahmen bleiben technische Fehler.
            return $null
        }

        function Add-SkippedEntry
        {
            param(
                [Parameter(Mandatory = $true)]
                [ValidateSet(
                    'AlreadyMissing',
                    'ReparsePoint',
                    'ReadOnly',
                    'SystemProtected',
                    'LockedByProcess',
                    'DirectoryNotEmpty',
                    'OtherSafetySkip'
                )]
                [string]$Reason
            )

            if ($null -eq $script:skipReasonCounts)
            {
                throw 'Die Überspringzähler wurden nicht initialisiert.'
            }

            if (
                -not $script:skipReasonCounts.ContainsKey(
                    $Reason)
            )
            {
                $script:skipReasonCounts[$Reason] =
                    [int64]0
            }

            $script:skipReasonCounts[$Reason] =
                [int64]$script:skipReasonCounts[$Reason] + 1

            $result.SkippedEntryCount =
                [int64]$result.SkippedEntryCount + 1
        }

        function Record-FileSystemException
        {
            param(
                [Parameter(Mandatory = $true)]
                [System.Exception]$Exception,

                [object]$KnownAttributes = $null,

                [bool]$AllowDirectoryNotEmpty = $false
            )

            $reason =
                Get-SkipReasonFromException `
                    -Exception $Exception `
                    -KnownAttributes $KnownAttributes `
                    -AllowDirectoryNotEmpty $AllowDirectoryNotEmpty

            if (
                -not [string]::IsNullOrWhiteSpace(
                    $reason)
            )
            {
                Add-SkippedEntry -Reason $reason
                return
            }

            # Nur ungeklärte beziehungsweise
            # echte technische Fehler erhöhen
            # diesen Zähler.
            $result.FailedEntryCount =
                [int64]$result.FailedEntryCount + 1
        }

        function Get-SkippedReasonRecords
        {
            if ($null -eq $script:skipReasonCounts)
            {
                throw 'Die Überspringzähler wurden nicht initialisiert.'
            }

            $records = @(
                foreach (
                    $reason in (
                        $script:skipReasonCounts.Keys |
                            Sort-Object
                    )
                )
                {
                    [pscustomobject]@{
                        Reason = [string]$reason
                        Count =
                            [int64]$script:skipReasonCounts[$reason]
                    }
                }
            )

            return $records
        }
        """;
}
