using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace WeberIT.Checkup.App.Services.Cleanup;

internal sealed class WindowsRollbackAvailabilityProbe
{
    private const int RollbackUnavailableErrorCode =
        1168;

    private const int ElevationRequiredErrorCode =
        740;

    private const int AccessDeniedErrorCode =
        5;

    public WindowsRollbackAvailabilityResult Analyze(
        DateTime deadline)
    {
        if (DateTime.UtcNow >= deadline)
        {
            return CreateTimedOutResult();
        }

        try
        {
            var startInfo =
                new ProcessStartInfo
                {
                    FileName =
                        FindDismPath(),

                    UseShellExecute =
                        false,

                    CreateNoWindow =
                        true,

                    RedirectStandardOutput =
                        true,

                    RedirectStandardError =
                        true
                };

            startInfo.ArgumentList.Add(
                "/Online");

            startInfo.ArgumentList.Add(
                "/Get-OSUninstallWindow");

            using var process =
                new Process
                {
                    StartInfo =
                        startInfo
                };

            if (!process.Start())
            {
                return new WindowsRollbackAvailabilityResult
                {
                    Status =
                        WindowsRollbackAvailabilityStatus.NotEvaluable,

                    Message =
                        "Die Windows-Rückkehrfunktion konnte nicht "
                        + "geprüft werden, weil DISM nicht gestartet "
                        + "werden konnte."
                };
            }

            var standardOutputTask =
                process.StandardOutput.ReadToEndAsync();

            var standardErrorTask =
                process.StandardError.ReadToEndAsync();

            var remainingWaitMilliseconds =
                GetRemainingWaitMilliseconds(
                    deadline);

            if (remainingWaitMilliseconds <= 0
                || !process.WaitForExit(
                    remainingWaitMilliseconds))
            {
                TryTerminateProcess(
                    process);

                return CreateTimedOutResult();
            }

            process.WaitForExit();

            var standardOutput =
                standardOutputTask
                    .GetAwaiter()
                    .GetResult();

            var standardError =
                standardErrorTask
                    .GetAwaiter()
                    .GetResult();

            var combinedOutput =
                standardOutput
                + Environment.NewLine
                + standardError;

            if (process.ExitCode == 0)
            {
                return new WindowsRollbackAvailabilityResult
                {
                    Status =
                        WindowsRollbackAvailabilityStatus.Available,

                    UninstallWindowDays =
                        TryParseUninstallWindowDays(
                            standardOutput),

                    Message =
                        "DISM meldet eine verfügbare "
                        + "Windows-Rückkehrfunktion."
                };
            }

            if (process.ExitCode
                    == RollbackUnavailableErrorCode
                || ContainsErrorCodeLine(
                    combinedOutput,
                    RollbackUnavailableErrorCode))
            {
                return new WindowsRollbackAvailabilityResult
                {
                    Status =
                        WindowsRollbackAvailabilityStatus.Unavailable,

                    Message =
                        "DISM meldet Fehler 1168. "
                        + "Die Windows-Rückkehrfunktion ist "
                        + "nicht mehr verfügbar."
                };
            }

            if (process.ExitCode
                    is ElevationRequiredErrorCode
                    or AccessDeniedErrorCode
                || ContainsErrorCodeLine(
                    combinedOutput,
                    ElevationRequiredErrorCode)
                || ContainsErrorCodeLine(
                    combinedOutput,
                    AccessDeniedErrorCode))
            {
                return new WindowsRollbackAvailabilityResult
                {
                    Status =
                        WindowsRollbackAvailabilityStatus.NotEvaluable,

                    Message =
                        "DISM konnte die Windows-Rückkehrfunktion "
                        + "ohne ausreichende Administratorrechte "
                        + "nicht zuverlässig auswerten."
                };
            }

            return new WindowsRollbackAvailabilityResult
            {
                Status =
                    WindowsRollbackAvailabilityStatus.NotEvaluable,

                Message =
                    $"DISM konnte die Windows-Rückkehrfunktion "
                    + $"nicht zuverlässig auswerten. "
                    + $"Fehlercode: {process.ExitCode}."
            };
        }
        catch (Exception exception)
        {
            return new WindowsRollbackAvailabilityResult
            {
                Status =
                    WindowsRollbackAvailabilityStatus.NotEvaluable,

                Message =
                    "Die Windows-Rückkehrfunktion konnte nicht "
                    + "zuverlässig geprüft werden. "
                    + $"Technische Ursache: {exception.Message}"
            };
        }
    }

    private static string FindDismPath()
    {
        var windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        if (!string.IsNullOrWhiteSpace(
                windowsDirectory))
        {
            var candidatePath =
                Path.Combine(
                    windowsDirectory,
                    "System32",
                    "dism.exe");

            if (File.Exists(
                    candidatePath))
            {
                return candidatePath;
            }
        }

        return "dism.exe";
    }

    private static int GetRemainingWaitMilliseconds(
        DateTime deadline)
    {
        var remaining =
            deadline
            - DateTime.UtcNow;

        if (remaining <= TimeSpan.Zero)
        {
            return 0;
        }

        if (remaining.TotalMilliseconds
            >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return Math.Max(
            1,
            (int)Math.Ceiling(
                remaining.TotalMilliseconds));
    }

    private static int? TryParseUninstallWindowDays(
        string output)
    {
        if (string.IsNullOrWhiteSpace(
                output))
        {
            return null;
        }

        var matches =
            Regex.Matches(
                output,
                @"(?m)^\s*[^:\r\n]+:\s*(\d{1,3})\s*$",
                RegexOptions.CultureInvariant);

        foreach (Match match in matches)
        {
            if (!match.Success
                || match.Groups.Count < 2)
            {
                continue;
            }

            if (!int.TryParse(
                    match.Groups[1].Value,
                    out var days))
            {
                continue;
            }

            if (days is >= 2 and <= 60)
            {
                return days;
            }
        }

        return null;
    }

    private static bool ContainsErrorCodeLine(
        string output,
        int errorCode)
    {
        if (string.IsNullOrWhiteSpace(
                output))
        {
            return false;
        }

        var pattern =
            $@"(?m)^\s*[^:\r\n]+:\s*"
            + Regex.Escape(
                errorCode.ToString())
            + @"\s*$";

        return Regex.IsMatch(
            output,
            pattern,
            RegexOptions.CultureInvariant);
    }

    private static void TryTerminateProcess(
        Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(
                    entireProcessTree: true);

                process.WaitForExit(
                    2000);
            }
        }
        catch
        {
            // Das Beenden einer festhängenden Prüfinstanz
            // darf das eigentliche Analyseergebnis nicht
            // überschreiben.
        }
    }

    private static WindowsRollbackAvailabilityResult
        CreateTimedOutResult()
    {
        return new WindowsRollbackAvailabilityResult
        {
            Status =
                WindowsRollbackAvailabilityStatus.TimedOut,

            Message =
                "Die Prüfung der Windows-Rückkehrfunktion "
                + "hat ihr eigenes Sicherheitszeitlimit erreicht."
        };
    }
}

internal sealed class WindowsRollbackAvailabilityResult
{
    public WindowsRollbackAvailabilityStatus Status
    {
        get;
        init;
    }

    public int? UninstallWindowDays
    {
        get;
        init;
    }

    public string Message
    {
        get;
        init;
    } = string.Empty;
}

internal enum WindowsRollbackAvailabilityStatus
{
    Available,
    Unavailable,
    NotEvaluable,
    TimedOut
}