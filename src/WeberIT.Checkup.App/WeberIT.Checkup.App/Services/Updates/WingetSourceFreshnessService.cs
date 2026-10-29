
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;

namespace WeberIT.Checkup.App.Services.Updates;

/// <summary>
/// Prüft vor einer WinGet-Zwischenprüfung,
/// ob die offizielle WinGet-Paketquelle
/// erreichbar ist und gezielt aktualisiert
/// werden konnte.
///
/// Die Prüfung verändert keine installierten
/// Anwendungsprogramme.
///
/// Sie ersetzt ausdrücklich nicht die spätere
/// Auswertung der verfügbaren Updates.
/// </summary>
public sealed class WingetSourceFreshnessService
{
    private static readonly Uri SourceAddress =
        new("https://cdn.winget.microsoft.com/cache");

    private static readonly TimeSpan
        NetworkTimeout =
            TimeSpan.FromSeconds(10);

    private static readonly TimeSpan
        SourceUpdateTimeout =
            TimeSpan.FromSeconds(120);

    private const int MaximumDiagnosticLength =
        1600;

    /// <summary>
    /// Überprüft die Erreichbarkeit der
    /// offiziellen WinGet-Quelle und führt
    /// anschließend eine gezielte
    /// Quellenaktualisierung aus.
    ///
    /// Fehler werden als negatives
    /// Prüfergebnis zurückgegeben.
    ///
    /// Eine vom Aufrufer ausdrücklich
    /// abgebrochene Prüfung propagiert
    /// dagegen die Cancellation.
    /// </summary>
    public async Task<WingetSourceFreshnessResult>
        CheckAsync(
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var networkResult =
            await CheckSourceReachabilityAsync(
                cancellationToken);

        if (!networkResult.IsSuccessful)
        {
            return WingetSourceFreshnessResult
                .Failed(
                    "Die offizielle WinGet-Quelle "
                    + "konnte über HTTPS nicht "
                    + "zuverlässig erreicht werden. "
                    + networkResult.Details);
        }

        return await RefreshSourceAsync(
            cancellationToken);
    }

    // =============================================
    // ERREICHBARKEIT DER WINGET-QUELLE
    // =============================================

    private static async Task<NetworkCheckResult>
        CheckSourceReachabilityAsync(
            CancellationToken cancellationToken)
    {
        using var timeout =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        timeout.CancelAfter(
            NetworkTimeout);

        try
        {
            using var client =
                new HttpClient
                {
                    Timeout =
                        Timeout.InfiniteTimeSpan
                };

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Head,
                    SourceAddress);

            using var response =
                await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token);

            /*
             * Hier prüfen wir ausschließlich,
             * ob der HTTPS-Endpunkt erreichbar ist.
             *
             * Der Basis-Pfad /cache kann für
             * HEAD-Anfragen beispielsweise
             * HTTP 404 oder 405 zurückgeben.
             *
             * Solche Antworten belegen immerhin,
             * dass der HTTPS-Endpunkt geantwortet
             * hat. Sie bestätigen noch NICHT
             * die Verfügbarkeit der Paketdaten.
             *
             * Diese Aufgabe übernimmt erst der
             * anschließende WinGet-Quellenrefresh.
             */

            if (response.StatusCode
                    is HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden
                    or HttpStatusCode.ProxyAuthenticationRequired
                || (int)response.StatusCode >= 500)
            {
                return NetworkCheckResult
                    .Failed(
                        "Der HTTPS-Endpunkt antwortete "
                        + "mit HTTP "
                        + (int)response.StatusCode
                        + ".");
            }

            return NetworkCheckResult
                .Successful();
        }
        catch (OperationCanceledException)
            when (!cancellationToken
                .IsCancellationRequested)
        {
            return NetworkCheckResult
                .Failed(
                    "Die HTTPS-Erreichbarkeitsprüfung "
                    + "hat das Zeitlimit überschritten.");
        }
        catch (HttpRequestException exception)
        {
            return NetworkCheckResult
                .Failed(
                    "Netzwerkfehler: "
                    + LimitDiagnostic(
                        exception.Message));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return NetworkCheckResult
                .Failed(
                    "Die HTTPS-Prüfung konnte nicht "
                    + "ausgeführt werden. "
                    + LimitDiagnostic(
                        exception.Message));
        }
    }

    // =============================================
    // WINGET-QUELLE AKTUALISIEREN
    // =============================================

    private static async Task<WingetSourceFreshnessResult>
        RefreshSourceAsync(
            CancellationToken cancellationToken)
    {
        using var process =
            new Process
            {
                StartInfo =
                    CreateSourceUpdateStartInfo()
            };

        using var timeout =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        timeout.CancelAfter(
            SourceUpdateTimeout);

        try
        {
            if (!process.Start())
            {
                return WingetSourceFreshnessResult
                    .Failed(
                        "Der WinGet-Quellenrefresh "
                        + "konnte nicht gestartet werden.");
            }

            /*
             * Beide Ausgabeströme werden
             * gleichzeitig gelesen.
             *
             * So vermeiden wir blockierende
             * Prozessausgaben.
             */
            var standardOutputTask =
                process.StandardOutput
                    .ReadToEndAsync();

            var standardErrorTask =
                process.StandardError
                    .ReadToEndAsync();

            await process.WaitForExitAsync(
                timeout.Token);

            var standardOutput =
                await standardOutputTask;

            var standardError =
                await standardErrorTask;

            if (process.ExitCode != 0)
            {
                var details =
                    BuildFailureDetails(
                        process.ExitCode,
                        standardOutput,
                        standardError);

                return WingetSourceFreshnessResult
                    .Failed(
                        details,
                        process.ExitCode);
            }

            /*
             * Exitcode 0 bescheinigt hier,
             * dass WinGet den angeforderten
             * Quellenrefresh regulär beendet hat.
             *
             * Eine inhaltlich zuverlässige
             * Updateanalyse ist damit allein
             * noch NICHT nachgewiesen.
             */
            return WingetSourceFreshnessResult
                .Successful(
                    "Die offizielle WinGet-Quelle "
                    + "war über HTTPS erreichbar. "
                    + "WinGet hat die gezielte "
                    + "Quellenaktualisierung ohne "
                    + "Prozessfehler beendet.");
        }
        catch (OperationCanceledException)
            when (!cancellationToken
                .IsCancellationRequested)
        {
            TryTerminateProcess(
                process);

            return WingetSourceFreshnessResult
                .Failed(
                    "Die Aktualisierung der WinGet-Quelle "
                    + "hat das Zeitlimit von "
                    + "120 Sekunden überschritten.");
        }
        catch (OperationCanceledException)
        {
            TryTerminateProcess(
                process);

            throw;
        }
        catch (Exception exception)
        {
            TryTerminateProcess(
                process);

            return WingetSourceFreshnessResult
                .Failed(
                    "Der WinGet-Quellenrefresh "
                    + "konnte nicht zuverlässig "
                    + "ausgeführt werden. "
                    + LimitDiagnostic(
                        exception.Message));
        }
    }

    private static ProcessStartInfo
        CreateSourceUpdateStartInfo()
    {
        var startInfo =
            new ProcessStartInfo
            {
                FileName = "winget.exe",

                UseShellExecute = false,

                CreateNoWindow = true,

                RedirectStandardOutput = true,

                RedirectStandardError = true,

                StandardOutputEncoding =
                    Encoding.UTF8,

                StandardErrorEncoding =
                    Encoding.UTF8
            };

        startInfo.ArgumentList.Add(
            "source");

        startInfo.ArgumentList.Add(
            "update");

        startInfo.ArgumentList.Add(
            "--name");

        startInfo.ArgumentList.Add(
            "winget");

        startInfo.ArgumentList.Add(
            "--disable-interactivity");

        return startInfo;
    }

    // =============================================
    // FEHLERBEHANDLUNG
    // =============================================

    private static void TryTerminateProcess(
        Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(
                    entireProcessTree: true);
            }
        }
        catch
        {
            /*
             * Ein bereits beendeter oder
             * nicht zugänglicher Prozess
             * darf die ursprüngliche
             * Fehlerbehandlung nicht
             * verdecken.
             */
        }
    }

    private static string BuildFailureDetails(
        int exitCode,
        string standardOutput,
        string standardError)
    {
        var builder =
            new StringBuilder();

        builder.Append(
            "Die Aktualisierung der WinGet-Quelle "
            + "wurde nicht erfolgreich beendet.");

        builder.Append(
            Environment.NewLine);

        builder.Append(
            "Exitcode: ");

        builder.Append(
            exitCode);

        if (!string.IsNullOrWhiteSpace(
                standardError))
        {
            builder.Append(
                Environment.NewLine);

            builder.Append(
                "Fehlerausgabe: ");

            builder.Append(
                LimitDiagnostic(
                    standardError));
        }

        if (!string.IsNullOrWhiteSpace(
                standardOutput))
        {
            builder.Append(
                Environment.NewLine);

            builder.Append(
                "Prozessausgabe: ");

            builder.Append(
                LimitDiagnostic(
                    standardOutput));
        }

        return builder.ToString();
    }

    private static string LimitDiagnostic(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        var normalized =
            value.Trim();

        return normalized.Length
            <= MaximumDiagnosticLength
                ? normalized
                : normalized[
                    ..MaximumDiagnosticLength]
                  + " … Ausgabe gekürzt.";
    }

    // =============================================
    // INTERNES NETZWERKERGEBNIS
    // =============================================

    private sealed class NetworkCheckResult
    {
        public bool IsSuccessful
        {
            get;
            private init;
        }

        public string Details
        {
            get;
            private init;
        } = string.Empty;

        public static NetworkCheckResult
            Successful()
        {
            return new NetworkCheckResult
            {
                IsSuccessful = true
            };
        }

        public static NetworkCheckResult
            Failed(
                string details)
        {
            return new NetworkCheckResult
            {
                IsSuccessful = false,

                Details = details
            };
        }
    }
}

/// <summary>
/// Ergebnis einer vorgeschalteten Prüfung
/// der WinGet-Quellenaktualität.
///
/// Nur ein erfolgreiches Ergebnis gestattet,
/// die nachfolgende WinGet-Analyse überhaupt
/// als zuverlässigen aktuellen Befund
/// in Betracht zu ziehen.
/// </summary>
public sealed class WingetSourceFreshnessResult
{
    public bool IsSourceRefreshConfirmed
    {
        get;
        init;
    }

    public DateTimeOffset CheckedAt
    {
        get;
        init;
    } = DateTimeOffset.Now;

    public int? ExitCode
    {
        get;
        init;
    }

    public string Details
    {
        get;
        init;
    } = string.Empty;

    public static WingetSourceFreshnessResult
        Successful()
    {
        return Successful(
            "Die WinGet-Quellenaktualisierung "
            + "wurde erfolgreich durchgeführt.");
    }

    public static WingetSourceFreshnessResult
        Successful(
            string details)
    {
        return new WingetSourceFreshnessResult
        {
            IsSourceRefreshConfirmed = true,

            ExitCode = 0,

            Details = details
        };
    }

    public static WingetSourceFreshnessResult
        Failed(
            string details,
            int? exitCode = null)
    {
        return new WingetSourceFreshnessResult
        {
            IsSourceRefreshConfirmed = false,

            ExitCode = exitCode,

            Details = details
        };
    }
}
