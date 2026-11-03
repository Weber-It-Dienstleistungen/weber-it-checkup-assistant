
using System;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.26a
///
/// Autorisiert ausschließlich das lokale
/// Windows-Temp-Verzeichnis als Stammordner
/// für die handle-basierte Bereinigung.
///
/// Sicherheitsregeln:
/// - Keine frei übergebenen Zielpfade.
/// - Kein Benutzer-Temp-Verzeichnis.
/// - Keine Netzwerk- oder Gerätepfade.
/// - Keine Laufwerksstämme.
/// - Keine Reparse Points.
/// - Keine Pfadumleitungen.
/// - Keine geschützten Stammverzeichnisse.
///
/// Die eigentliche Bereinigung wird
/// hier ausdrücklich nicht gestartet.
///
/// Das geöffnete Handle bleibt bis zur
/// Freigabe durch Dispose bestehen.
/// </summary>
internal sealed class WindowsTemporaryFilesAuthorizedTarget
    : IDisposable
{
    // =============================================
    // AUTORISIERTES ZIEL
    // =============================================

    internal string TargetPath { get; }

    internal SafeFileHandle RootHandle { get; }

    private WindowsTemporaryFilesAuthorizedTarget(
        string targetPath,
        SafeFileHandle rootHandle)
    {
        TargetPath = targetPath;
        RootHandle = rootHandle;
    }

    // =============================================
    // WINDOWS-TEMP AUTORISIERT ÖFFNEN
    // =============================================

    internal static WindowsTemporaryFilesAuthorizedTarget
        Open()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Die Windows-Temp-Autorisierung "
                + "ist ausschließlich unter "
                + "Windows verfügbar.");
        }

        /*
         * Der Zielbereich wird ausschließlich
         * aus dem Windows-Systemverzeichnis
         * abgeleitet.
         *
         * Kein vom Benutzer übergebener
         * Bereinigungspfad.
         *
         * Insbesondere kein Path.GetTempPath(),
         * da dieser normalerweise auf das
         * Benutzer-Temp-Verzeichnis verweist.
         */
        var windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        if (string.IsNullOrWhiteSpace(
                windowsDirectory))
        {
            throw new InvalidOperationException(
                "Das Windows-Systemverzeichnis "
                + "konnte nicht bestimmt werden.");
        }

        var normalizedWindowsDirectory =
            NormalizeLocalDirectory(
                windowsDirectory);

        /*
         * Das Windows-Verzeichnis selbst
         * darf niemals ein Laufwerksstamm sein.
         */
        var volumeRoot =
            Path.GetPathRoot(
                normalizedWindowsDirectory);

        if (string.IsNullOrWhiteSpace(volumeRoot)
            || string.Equals(
                normalizedWindowsDirectory,
                Path.TrimEndingDirectorySeparator(
                    volumeRoot),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Das Windows-Systemverzeichnis "
                + "besitzt keinen zulässigen "
                + "Verzeichnisstamm.");
        }

        /*
         * Ausschließlich der direkte
         * Unterordner Windows\Temp
         * ist autorisiert.
         */
        var targetPath =
            NormalizeLocalDirectory(
                Path.Combine(
                    normalizedWindowsDirectory,
                    "Temp"));

        var targetParent =
            Path.GetDirectoryName(targetPath);

        if (!string.Equals(
                targetParent,
                normalizedWindowsDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Der Windows-Temp-Zielordner "
                + "liegt nicht unmittelbar "
                + "unter dem Windows-Verzeichnis.");
        }

        /*
         * Die bestehende HandleAccess-Klasse
         * übernimmt:
         *
         * - Öffnung ohne Reparse-Verfolgung
         * - Prüfung des Verzeichnistyps
         * - Ablehnung von Reparse Points
         * - Prüfung des tatsächlich
         *   geöffneten Zielpfades
         *
         * Das Stammhandle wird nicht über
         * einen späteren Pfad erneut geöffnet.
         */
        var rootHandle =
            WindowsTemporaryFilesHandleAccess
                .OpenRootDirectory(targetPath);

        try
        {
            var snapshot =
                WindowsTemporaryFilesHandleAccess
                    .ReadSnapshot(rootHandle);

            /*
             * Zusätzliche Schutzprüfung.
             *
             * Die Engine prüft diese Merkmale
             * später erneut am selben Handle.
             */
            if (!snapshot.IsDirectory
                || snapshot.IsReparsePoint
                || snapshot.IsReadOnly
                || snapshot.IsSystemProtected
                || snapshot.FileIndex == 0)
            {
                throw new InvalidOperationException(
                    "Der Windows-Temp-Stammordner "
                    + "erfüllt die erforderlichen "
                    + "Sicherheitsbedingungen nicht.");
            }

            return new WindowsTemporaryFilesAuthorizedTarget(
                targetPath,
                rootHandle);
        }
        catch
        {
            /*
             * Bei jeder fehlgeschlagenen
             * Autorisierung wird das bereits
             * geöffnete Handle geschlossen.
             */
            rootHandle.Dispose();
            throw;
        }
    }

    // =============================================
    // LOKALEN PFAD NORMALISIEREN
    // =============================================

    private static string NormalizeLocalDirectory(
        string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            directoryPath);

        if (!Path.IsPathFullyQualified(
                directoryPath)
            || IsNetworkOrDevicePath(
                directoryPath))
        {
            throw new InvalidOperationException(
                "Für die Windows-Temp-Bereinigung "
                + "ist ausschließlich ein lokaler, "
                + "vollständig qualifizierter "
                + "Verzeichnispfad zulässig.");
        }

        var normalizedPath =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    directoryPath));

        if (IsNetworkOrDevicePath(
                normalizedPath))
        {
            throw new InvalidOperationException(
                "Der normalisierte Zielpfad "
                + "ist kein zulässiger "
                + "lokaler Verzeichnispfad.");
        }

        return normalizedPath;
    }

    private static bool IsNetworkOrDevicePath(
        string path)
    {
        return path.StartsWith(
                   @"\\",
                   StringComparison.Ordinal)
               || path.StartsWith(
                   "//",
                   StringComparison.Ordinal);
    }

    // =============================================
    // HANDLE FREIGEBEN
    // =============================================

    public void Dispose()
    {
        RootHandle.Dispose();
    }
}
