
using System;
using System.IO;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.13.51
///
/// Erzeugt einen PowerShell-Starter, der
/// ausschließlich den zuvor erfolgreich
/// verifizierten Skriptinhalt ausführt.
///
/// Sicherheitsprinzip:
///
/// 1. Skriptinhalt einmal lesen.
/// 2. SHA-256 des gelesenen Inhalts prüfen.
/// 3. Genau diesen Inhalt als ScriptBlock
///    ausführen.
///
/// Kein erneutes Öffnen der Skriptdatei
/// zwischen Hashprüfung und Ausführung.
///
/// Die Klasse startet selbst keinen Prozess
/// und führt keinen PowerShell-Code aus.
/// </summary>
internal static class
    WindowsTemporaryFilesVerifiedLauncherScript
{
    public static string Build(
        string cleanupScriptPath,
        string expectedScriptHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            cleanupScriptPath);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            expectedScriptHash);

        var normalizedPath =
            Path.GetFullPath(
                cleanupScriptPath);

        /*
         * Externe Netzwerkpfade sind für
         * die erhöhte Ausführung verboten.
         */
        if (normalizedPath.StartsWith(
                @"\\",
                StringComparison.Ordinal)
            || normalizedPath.StartsWith(
                "//",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Das erhöhte Bereinigungsskript "
                + "darf nicht über einen "
                + "Netzwerkpfad gestartet werden.");
        }

        /*
         * Ausschließlich ein vollständiger
         * SHA-256-Hash mit 32 Bytes ist
         * zulässig.
         */
        var normalizedHash =
            expectedScriptHash.Trim();

        if (normalizedHash.Length != 64)
        {
            throw new ArgumentException(
                "Der erwartete SHA-256-Hash "
                + "besitzt keine gültige Länge.",
                nameof(expectedScriptHash));
        }

        try
        {
            var hashBytes =
                Convert.FromHexString(
                    normalizedHash);

            if (hashBytes.Length != 32)
            {
                throw new ArgumentException(
                    "Der erwartete SHA-256-Hash "
                    + "ist ungültig.",
                    nameof(expectedScriptHash));
            }
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                "Der erwartete SHA-256-Hash "
                + "enthält ungültige Zeichen.",
                nameof(expectedScriptHash),
                exception);
        }

        var escapedScriptPath =
            EscapePowerShellLiteral(
                normalizedPath);

        var escapedExpectedHash =
            EscapePowerShellLiteral(
                normalizedHash.ToUpperInvariant());

        /*
         * Windows-PowerShell 5.1 verwendet
         * die klassischen SHA256-Methoden.
         *
         * SHA256.HashData steht dort nicht
         * auf allen Zielsystemen zur Verfügung.
         */
        return
            "$ErrorActionPreference = 'Stop'; "

            + "$scriptPath = '"
            + escapedScriptPath
            + "'; "

            + "$expectedHash = '"
            + escapedExpectedHash
            + "'; "

            /*
             * Einmaliger Lesezugriff.
             */
            + "$scriptContent = "
            + "[System.IO.File]::ReadAllText("
            + "$scriptPath, "
            + "[System.Text.Encoding]::Unicode); "

            + "$scriptBytes = "
            + "[System.Text.Encoding]::Unicode.GetBytes("
            + "$scriptContent); "

            /*
             * Hashprüfung auf genau
             * diesem Skriptinhalt.
             */
            + "$sha256 = "
            + "[System.Security.Cryptography.SHA256]::Create(); "

            + "try { "
            + "$actualHash = "
            + "[System.BitConverter]::ToString("
            + "$sha256.ComputeHash($scriptBytes))"
            + ".Replace('-', ''); "
            + "} finally { "
            + "$sha256.Dispose(); "
            + "} "

            + "if (-not [string]::Equals("
            + "$actualHash, "
            + "$expectedHash, "
            + "[System.StringComparison]::OrdinalIgnoreCase)) "
            + "{ "
            + "throw 'Die Integritätsprüfung des erhöhten "
            + "Bereinigungsskripts ist fehlgeschlagen.'; "
            + "} "

            /*
             * ENTSCHEIDENDE SICHERHEITSÄNDERUNG
             *
             * Nicht:
             *
             * & $scriptPath
             *
             * Sondern:
             *
             * Den zuvor verifizierten
             * Speicherinhalt direkt ausführen.
             *
             * Damit wird die Skriptdatei
             * nicht erneut geöffnet.
             */
            + "$verifiedScriptBlock = "
            + "[System.Management.Automation.ScriptBlock]::Create("
            + "$scriptContent); "

            + "& $verifiedScriptBlock;";
    }

    private static string EscapePowerShellLiteral(
        string value)
    {
        return value.Replace(
            "'",
            "''",
            StringComparison.Ordinal);
    }
}
