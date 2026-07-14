using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Maintenance;

[SupportedOSPlatform("windows")]
public class MaintenanceProcessRunner : IMaintenanceProcessRunner
{
    private const int OperationCancelledErrorCode = 1223;

    public Task<ProcessExecutionResult> RunAsync(
        string fileName,
        IReadOnlyCollection<string> arguments,
        bool requiresAdministrator)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(
                "Der Dateiname des auszuführenden Programms darf nicht leer sein.",
                nameof(fileName));
        }

        ArgumentNullException.ThrowIfNull(arguments);

        if (requiresAdministrator && !IsRunningAsAdministrator())
        {
            return RunElevatedAsync(
                fileName,
                arguments);
        }

        return RunDirectlyAsync(
            fileName,
            arguments,
            requiresAdministrator);
    }

    private static async Task<ProcessExecutionResult> RunDirectlyAsync(
        string fileName,
        IReadOnlyCollection<string> arguments,
        bool wasElevated)
    {
        var startedAt =
            DateTimeOffset.Now;

        try
        {
            var startInfo =
                new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process =
                new Process
                {
                    StartInfo = startInfo
                };

            if (!process.Start())
            {
                return CreateStartFailure(
                    startedAt,
                    wasElevated,
                    "Der Wartungsprozess konnte nicht gestartet werden.");
            }

            var standardOutputTask =
                process.StandardOutput.ReadToEndAsync();

            var standardErrorTask =
                process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var standardOutput =
                await standardOutputTask;

            var standardError =
                await standardErrorTask;

            return new ProcessExecutionResult
            {
                WasStarted = true,
                WasElevated = wasElevated,
                ExitCode = process.ExitCode,
                StandardOutput = standardOutput,
                StandardError = standardError,
                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.Now
            };
        }
        catch (Exception exception)
        {
            return CreateStartFailure(
                startedAt,
                wasElevated,
                exception.Message);
        }
    }

    private static async Task<ProcessExecutionResult> RunElevatedAsync(
        string fileName,
        IReadOnlyCollection<string> arguments)
    {
        var startedAt =
            DateTimeOffset.Now;

        var executionId =
            Guid.NewGuid().ToString("N");

        var temporaryDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "WeberIT.Checkup",
                executionId);

        var standardOutputPath =
            Path.Combine(
                temporaryDirectory,
                "standard-output.txt");

        var standardErrorPath =
            Path.Combine(
                temporaryDirectory,
                "standard-error.txt");

        var startedMarkerPath =
            Path.Combine(
                temporaryDirectory,
                "process-started.marker");

        try
        {
            Directory.CreateDirectory(
                temporaryDirectory);

            var powerShellPath =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Windows),
                    "System32",
                    "WindowsPowerShell",
                    "v1.0",
                    "powershell.exe");

            var elevatedScript =
                BuildElevatedExecutionScript(
                    fileName,
                    arguments,
                    standardOutputPath,
                    standardErrorPath,
                    startedMarkerPath);

            var encodedScript =
                Convert.ToBase64String(
                    Encoding.Unicode.GetBytes(
                        elevatedScript));

            var startInfo =
                new ProcessStartInfo
                {
                    FileName = powerShellPath,
                    Arguments =
                        "-NoProfile "
                        + "-NonInteractive "
                        + "-ExecutionPolicy Bypass "
                        + "-EncodedCommand "
                        + encodedScript,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

            using var process =
                new Process
                {
                    StartInfo = startInfo
                };

            try
            {
                if (!process.Start())
                {
                    return CreateStartFailure(
                        startedAt,
                        false,
                        "Die Anforderung der Administratorrechte konnte nicht gestartet werden.");
                }
            }
            catch (Win32Exception exception)
                when (exception.NativeErrorCode
                      == OperationCancelledErrorCode)
            {
                return CreateCancelledResult(
                    startedAt);
            }

            await process.WaitForExitAsync();

            var standardOutput =
                await ReadTemporaryFileAsync(
                    standardOutputPath);

            var standardError =
                await ReadTemporaryFileAsync(
                    standardErrorPath);

            var wasStarted =
                File.Exists(startedMarkerPath);

            if (!wasStarted)
            {
                return new ProcessExecutionResult
                {
                    WasStarted = false,
                    WasElevated = false,
                    ExitCode = process.ExitCode,
                    StandardOutput = standardOutput,
                    StandardError = standardError,
                    ErrorMessage =
                        BuildMissingMarkerError(
                            standardError),
                    StartedAt = startedAt,
                    FinishedAt = DateTimeOffset.Now
                };
            }

            return new ProcessExecutionResult
            {
                WasStarted = true,
                WasElevated = true,
                ExitCode = process.ExitCode,
                StandardOutput = standardOutput,
                StandardError = standardError,
                StartedAt = startedAt,
                FinishedAt = DateTimeOffset.Now
            };
        }
        catch (Win32Exception exception)
            when (exception.NativeErrorCode
                  == OperationCancelledErrorCode)
        {
            return CreateCancelledResult(
                startedAt);
        }
        catch (Exception exception)
        {
            return CreateStartFailure(
                startedAt,
                false,
                exception.Message);
        }
        finally
        {
            TryDeleteDirectory(
                temporaryDirectory);
        }
    }

    private static string BuildElevatedExecutionScript(
        string fileName,
        IReadOnlyCollection<string> arguments,
        string standardOutputPath,
        string standardErrorPath,
        string startedMarkerPath)
    {
        var argumentList =
            arguments.Count == 0
                ? "@()"
                : "@("
                  + string.Join(
                      ", ",
                      arguments.Select(
                          argument =>
                              $"'{EscapePowerShellLiteral(argument)}'"))
                  + ")";

        return $$"""
            $ErrorActionPreference = 'Stop'

            try
            {
                [System.IO.File]::WriteAllText(
                    '{{EscapePowerShellLiteral(startedMarkerPath)}}',
                    'started')

                $toolArguments = {{argumentList}}

                & '{{EscapePowerShellLiteral(fileName)}}' `
                    @toolArguments `
                    1> '{{EscapePowerShellLiteral(standardOutputPath)}}' `
                    2> '{{EscapePowerShellLiteral(standardErrorPath)}}'

                $toolExitCode = $LASTEXITCODE

                if ($null -eq $toolExitCode)
                {
                    $toolExitCode = 1
                }

                exit $toolExitCode
            }
            catch
            {
                $errorMessage = $_.Exception.Message

                [System.IO.File]::WriteAllText(
                    '{{EscapePowerShellLiteral(standardErrorPath)}}',
                    $errorMessage)

                exit 1
            }
            """;
    }

    private static string EscapePowerShellLiteral(
        string value)
    {
        return value.Replace(
            "'",
            "''",
            StringComparison.Ordinal);
    }

    private static async Task<string> ReadTemporaryFileAsync(
        string path)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            var bytes =
                await File.ReadAllBytesAsync(
                    path);

            if (bytes.Length == 0)
            {
                return string.Empty;
            }

            return DecodeText(
                bytes);
        }
        catch (Exception exception)
        {
            return "Die temporäre Ausgabedatei konnte nicht gelesen werden: "
                   + exception.Message;
        }
    }

    private static string DecodeText(
        byte[] bytes)
    {
        if (HasUtf8Bom(bytes))
        {
            return Encoding.UTF8.GetString(
                bytes,
                3,
                bytes.Length - 3);
        }

        if (HasUtf16LittleEndianBom(bytes))
        {
            return Encoding.Unicode.GetString(
                bytes,
                2,
                bytes.Length - 2);
        }

        if (HasUtf16BigEndianBom(bytes))
        {
            return Encoding.BigEndianUnicode.GetString(
                bytes,
                2,
                bytes.Length - 2);
        }

        if (LooksLikeUtf16LittleEndian(bytes))
        {
            return Encoding.Unicode.GetString(
                bytes);
        }

        if (LooksLikeUtf16BigEndian(bytes))
        {
            return Encoding.BigEndianUnicode.GetString(
                bytes);
        }

        return Encoding.UTF8.GetString(
            bytes);
    }

    private static bool HasUtf8Bom(
        byte[] bytes)
    {
        return bytes.Length >= 3
               && bytes[0] == 0xEF
               && bytes[1] == 0xBB
               && bytes[2] == 0xBF;
    }

    private static bool HasUtf16LittleEndianBom(
        byte[] bytes)
    {
        return bytes.Length >= 2
               && bytes[0] == 0xFF
               && bytes[1] == 0xFE;
    }

    private static bool HasUtf16BigEndianBom(
        byte[] bytes)
    {
        return bytes.Length >= 2
               && bytes[0] == 0xFE
               && bytes[1] == 0xFF;
    }

    private static bool LooksLikeUtf16LittleEndian(
        byte[] bytes)
    {
        var sampleLength =
            Math.Min(
                bytes.Length,
                2048);

        var examinedPairs =
            0;

        var zeroHighBytes =
            0;

        for (var index = 0;
             index + 1 < sampleLength;
             index += 2)
        {
            examinedPairs++;

            if (bytes[index + 1] == 0)
            {
                zeroHighBytes++;
            }
        }

        return examinedPairs > 0
               && zeroHighBytes
               >= examinedPairs * 0.3;
    }

    private static bool LooksLikeUtf16BigEndian(
        byte[] bytes)
    {
        var sampleLength =
            Math.Min(
                bytes.Length,
                2048);

        var examinedPairs =
            0;

        var zeroLowBytes =
            0;

        for (var index = 0;
             index + 1 < sampleLength;
             index += 2)
        {
            examinedPairs++;

            if (bytes[index] == 0)
            {
                zeroLowBytes++;
            }
        }

        return examinedPairs > 0
               && zeroLowBytes
               >= examinedPairs * 0.3;
    }

    private static string BuildMissingMarkerError(
        string standardError)
    {
        if (!string.IsNullOrWhiteSpace(
            standardError))
        {
            return "Der erhöhte Wartungsprozess wurde nicht vollständig gestartet. "
                   + $"Technische Details: {standardError.Trim()}";
        }

        return "Der erhöhte Wartungsprozess wurde nicht vollständig gestartet.";
    }

    private static ProcessExecutionResult CreateCancelledResult(
        DateTimeOffset startedAt)
    {
        return new ProcessExecutionResult
        {
            WasStarted = false,
            WasElevated = false,
            ElevationWasCancelled = true,
            ErrorMessage =
                "Die Anforderung der Administratorrechte wurde abgebrochen.",
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.Now
        };
    }

    private static ProcessExecutionResult CreateStartFailure(
        DateTimeOffset startedAt,
        bool wasElevated,
        string errorMessage)
    {
        return new ProcessExecutionResult
        {
            WasStarted = false,
            WasElevated = wasElevated,
            ErrorMessage =
                string.IsNullOrWhiteSpace(errorMessage)
                    ? "Der Wartungsprozess konnte nicht gestartet werden."
                    : errorMessage,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.Now
        };
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(identity);

        return principal.IsInRole(
            WindowsBuiltInRole.Administrator);
    }

    private static void TryDeleteDirectory(
        string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(
                    directoryPath,
                    true);
            }
        }
        catch
        {
            // Eine fehlgeschlagene Bereinigung darf das
            // eigentliche Werkzeugergebnis nicht verändern.
        }
    }
}