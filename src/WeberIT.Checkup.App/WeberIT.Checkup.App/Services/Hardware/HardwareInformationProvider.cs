
using System.Globalization;
using System.Management;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Hardware;

public class HardwareInformationProvider :
    IHardwareInformationProvider
{
    public string GetManufacturer()
    {
        return GetWmiValue(
            "Win32_ComputerSystem",
            "Manufacturer");
    }

    public string GetModel()
    {
        return GetWmiValue(
            "Win32_ComputerSystem",
            "Model");
    }

    public string GetSerialNumber()
    {
        return GetWmiValue(
            "Win32_BIOS",
            "SerialNumber");
    }

    public string GetDeviceType()
    {
        var pcSystemType =
            GetWmiValue(
                "Win32_ComputerSystem",
                "PCSystemType");

        return pcSystemType switch
        {
            "1" => "Desktop",
            "2" => "Notebook",
            "3" => "Workstation",
            "4" => "Enterprise Server",
            "5" => "Small Office/Home Office Server",
            "6" => "Appliance PC",
            "7" => "Performance Server",
            "8" => "Tablet",
            _ => "Unbekannt"
        };
    }

    public string GetBiosManufacturer()
    {
        return GetWmiValue(
            "Win32_BIOS",
            "Manufacturer");
    }

    public string GetBiosVersion()
    {
        return GetWmiValue(
            "Win32_BIOS",
            "SMBIOSBIOSVersion");
    }

    public string GetProcessorName()
    {
        return GetWmiValue(
            "Win32_Processor",
            "Name");
    }

    // =====================================================
    // V1.1 – KORREKTE RAM-KAPAZITÄT
    // =====================================================

    public string GetInstalledMemory()
    {
        /*
         * Primärquelle:
         * Win32_PhysicalMemory.Capacity
         *
         * Diese WMI-Klasse liefert die Kapazität der
         * erkannten physischen Speichermodule in Bytes.
         *
         * Bei mehreren RAM-Modulen werden sämtliche
         * Kapazitäten addiert.
         *
         * Vorteil:
         * Hardwareseitig reservierter Arbeitsspeicher
         * wird nicht fälschlich von der installierten
         * Speicherkapazität abgezogen.
         */
        if (TryGetPhysicalMemoryModuleBytes(
                out var installedMemoryBytes))
        {
            return FormatMemoryCapacity(
                installedMemoryBytes);
        }

        /*
         * Rückfalllösung:
         *
         * Sollte die Abfrage der physischen Module
         * nicht verfügbar sein, verwenden wir die
         * bisherige WMI-Ermittlung.
         *
         * Dieser Wert kann etwas geringer sein
         * als die nominell installierte Kapazität.
         *
         * Die bisherige Funktionalität bleibt damit
         * auch bei eingeschränkter WMI-Verfügbarkeit
         * erhalten.
         */
        var totalPhysicalMemory =
            GetWmiValue(
                "Win32_ComputerSystem",
                "TotalPhysicalMemory");

        if (!ulong.TryParse(
                totalPhysicalMemory,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var fallbackBytes)
            || fallbackBytes == 0)
        {
            return "Unbekannt";
        }

        return FormatMemoryCapacity(
            fallbackBytes);
    }

    /// <summary>
    /// Ermittelt die Summe der Kapazitäten aller über
    /// Win32_PhysicalMemory erkannten RAM-Module.
    ///
    /// Es werden ausschließlich positive und
    /// vollständig lesbare Modulkapazitäten akzeptiert.
    ///
    /// Sobald eine Modulkapazität unplausibel ist
    /// oder die WMI-Abfrage fehlschlägt, wird false
    /// zurückgegeben.
    ///
    /// So verhindern wir, dass ein nur teilweise
    /// erfolgreicher Scan eine zu geringe installierte
    /// Speicherkapazität meldet.
    /// </summary>
    private static bool TryGetPhysicalMemoryModuleBytes(
        out ulong installedMemoryBytes)
    {
        installedMemoryBytes = 0;

        try
        {
            using var searcher =
                new ManagementObjectSearcher(
                    "SELECT Capacity "
                    + "FROM Win32_PhysicalMemory");

            using var results =
                searcher.Get();

            ulong totalBytes = 0;
            var moduleCount = 0;

            foreach (ManagementObject module in results)
            {
                /*
                 * Capacity wird von WMI in Bytes
                 * bereitgestellt.
                 *
                 * Einzelne Module können bei Problemen
                 * mit der Firmware oder WMI fehlende
                 * Werte liefern.
                 */
                var capacityValue =
                    module["Capacity"];

                if (capacityValue is null)
                {
                    return false;
                }

                var capacityText =
                    Convert.ToString(
                        capacityValue,
                        CultureInfo.InvariantCulture);

                if (!ulong.TryParse(
                        capacityText,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var moduleBytes)
                    || moduleBytes == 0)
                {
                    return false;
                }

                /*
                 * checked verhindert, dass bei
                 * ungewöhnlichen oder fehlerhaften
                 * WMI-Werten ein Überlauf unbemerkt
                 * eine falsche Gesamtkapazität erzeugt.
                 */
                totalBytes = checked(
                    totalBytes + moduleBytes);

                moduleCount++;
            }

            if (moduleCount == 0
                || totalBytes == 0)
            {
                return false;
            }

            installedMemoryBytes = totalBytes;
            return true;
        }
        catch
        {
            /*
             * Fehler bei WMI, Firmwaredaten oder
             * Überläufen dürfen den kompletten
             * Hardware-Scan nicht abbrechen.
             *
             * GetInstalledMemory verwendet dann
             * die bisherige Rückfalllösung.
             */
            installedMemoryBytes = 0;
            return false;
        }
    }

    /// <summary>
    /// Wandelt eine Speicherkapazität in Bytes in
    /// eine verständliche GB-Anzeige um.
    ///
    /// Die bestehende Formatierung bleibt erhalten,
    /// damit die nachfolgenden Bewertungsregeln
    /// den Wert weiterhin verarbeiten können.
    ///
    /// Technisch handelt es sich bei der Umrechnung
    /// durch 1024^3 um GiB. Die bestehende Anzeige
    /// verwendet aus Kompatibilitätsgründen "GB".
    /// </summary>
    private static string FormatMemoryCapacity(
        ulong bytes)
    {
        var gibibytes =
            bytes
            / 1024d
            / 1024d
            / 1024d;

        return $"{gibibytes:0.#} GB";
    }

    // =====================================================
    // WEITERE HARDWAREINFORMATIONEN
    // =====================================================

    public string GetMainboardManufacturer()
    {
        return GetWmiValue(
            "Win32_BaseBoard",
            "Manufacturer");
    }

    public string GetMainboardProduct()
    {
        return GetWmiValue(
            "Win32_BaseBoard",
            "Product");
    }

    public List<string> GetGraphicsCards()
    {
        var graphicsCards =
            GetWmiValues(
                "Win32_VideoController",
                "Name");

        return graphicsCards.Count > 0
            ? graphicsCards
            : new List<string>
            {
                "Unbekannt"
            };
    }

    public string GetTpmStatus()
    {
        var isEnabled =
            GetTpmValue(
                "IsEnabled_InitialValue");

        var isActivated =
            GetTpmValue(
                "IsActivated_InitialValue");

        if (isEnabled == "Unbekannt"
            && isActivated == "Unbekannt")
        {
            return "Nicht erkannt";
        }

        if (isEnabled.Equals(
                "True",
                StringComparison.OrdinalIgnoreCase)
            && isActivated.Equals(
                "True",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Aktiv";
        }

        return "Vorhanden, aber nicht aktiv";
    }

    public string GetTpmVersion()
    {
        var specificationVersion =
            GetTpmValue(
                "SpecVersion");

        return string.IsNullOrWhiteSpace(
            specificationVersion)
                ? "Unbekannt"
                : specificationVersion;
    }

    // =====================================================
    // ALLGEMEINE WMI-HILFSMETHODEN
    // =====================================================

    private static string GetWmiValue(
        string className,
        string propertyName)
    {
        try
        {
            using var searcher =
                new ManagementObjectSearcher(
                    $"SELECT {propertyName} "
                    + $"FROM {className}");

            foreach (var result in searcher.Get())
            {
                var value =
                    result[propertyName]
                        ?.ToString();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }
        catch
        {
            // WMI darf den Scan nicht abbrechen.
        }

        return "Unbekannt";
    }

    private static List<string> GetWmiValues(
        string className,
        string propertyName)
    {
        var values =
            new List<string>();

        try
        {
            using var searcher =
                new ManagementObjectSearcher(
                    $"SELECT {propertyName} "
                    + $"FROM {className}");

            foreach (var result in searcher.Get())
            {
                var value =
                    result[propertyName]
                        ?.ToString();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value.Trim());
                }
            }
        }
        catch
        {
            // WMI darf den Scan nicht abbrechen.
        }

        return values;
    }

    private static string GetTpmValue(
        string propertyName)
    {
        try
        {
            var scope =
                new ManagementScope(
                    @"\\.\root\CIMV2\Security\MicrosoftTpm");

            scope.Connect();

            using var searcher =
                new ManagementObjectSearcher(
                    scope,
                    new ObjectQuery(
                        $"SELECT {propertyName} "
                        + "FROM Win32_Tpm"));

            foreach (var result in searcher.Get())
            {
                var value =
                    result[propertyName]
                        ?.ToString();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }
        catch
        {
            // TPM-Abfrage darf den Scan nicht abbrechen.
        }

        return "Unbekannt";
    }
}
