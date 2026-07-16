using System.Runtime.InteropServices;
using System.Text;

namespace WeberIT.Checkup.App.Services.Startup;

public class ShellLinkTargetReader
{
    private const uint StoredRawPathFlag =
        0x00000004;

    public bool TryReadStoredTarget(
        string shortcutPath,
        out string targetPath)
    {
        targetPath =
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                shortcutPath))
        {
            return false;
        }

        try
        {
            var shellLinkType =
                Type.GetTypeFromCLSID(
                    new Guid(
                        "00021401-0000-0000-C000-000000000046"),
                    throwOnError: false);

            if (shellLinkType is null)
            {
                return false;
            }

            var shellLinkObject =
                Activator.CreateInstance(
                    shellLinkType);

            if (shellLinkObject
                is not IShellLinkW shellLink)
            {
                return false;
            }

            try
            {
                if (shellLinkObject
                    is not IPersistFile persistFile)
                {
                    return false;
                }

                persistFile.Load(
                    shortcutPath,
                    0);

                var targetBuffer =
                    new StringBuilder(
                        32768);

                var findData =
                    new Win32FindData();

                shellLink.GetPath(
                    targetBuffer,
                    targetBuffer.Capacity,
                    ref findData,
                    StoredRawPathFlag);

                targetPath =
                    targetBuffer
                        .ToString()
                        .Trim();

                return !string.IsNullOrWhiteSpace(
                    targetPath);
            }
            finally
            {
                if (Marshal.IsComObject(
                        shellLinkObject))
                {
                    Marshal.FinalReleaseComObject(
                        shellLinkObject);
                }
            }
        }
        catch
        {
            targetPath =
                string.Empty;

            return false;
        }
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath(
            [Out]
            [MarshalAs(UnmanagedType.LPWStr)]
            StringBuilder filePath,
            int maximumPathLength,
            ref Win32FindData findData,
            uint flags);

        void GetIDList(
            out IntPtr itemIdentifierList);

        void SetIDList(
            IntPtr itemIdentifierList);

        void GetDescription(
            [Out]
            [MarshalAs(UnmanagedType.LPWStr)]
            StringBuilder description,
            int maximumDescriptionLength);

        void SetDescription(
            [MarshalAs(UnmanagedType.LPWStr)]
            string description);

        void GetWorkingDirectory(
            [Out]
            [MarshalAs(UnmanagedType.LPWStr)]
            StringBuilder workingDirectory,
            int maximumPathLength);

        void SetWorkingDirectory(
            [MarshalAs(UnmanagedType.LPWStr)]
            string workingDirectory);

        void GetArguments(
            [Out]
            [MarshalAs(UnmanagedType.LPWStr)]
            StringBuilder arguments,
            int maximumArgumentsLength);

        void SetArguments(
            [MarshalAs(UnmanagedType.LPWStr)]
            string arguments);

        void GetHotkey(
            out short hotkey);

        void SetHotkey(
            short hotkey);

        void GetShowCmd(
            out int showCommand);

        void SetShowCmd(
            int showCommand);

        void GetIconLocation(
            [Out]
            [MarshalAs(UnmanagedType.LPWStr)]
            StringBuilder iconPath,
            int maximumPathLength,
            out int iconIndex);

        void SetIconLocation(
            [MarshalAs(UnmanagedType.LPWStr)]
            string iconPath,
            int iconIndex);

        void SetRelativePath(
            [MarshalAs(UnmanagedType.LPWStr)]
            string relativePath,
            uint reserved);

        void Resolve(
            IntPtr windowHandle,
            uint flags);

        void SetPath(
            [MarshalAs(UnmanagedType.LPWStr)]
            string filePath);
    }

    [ComImport]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(
            out Guid classIdentifier);

        [PreserveSig]
        int IsDirty();

        void Load(
            [MarshalAs(UnmanagedType.LPWStr)]
            string fileName,
            uint mode);

        void Save(
            [MarshalAs(UnmanagedType.LPWStr)]
            string fileName,
            [MarshalAs(UnmanagedType.Bool)]
            bool remember);

        void SaveCompleted(
            [MarshalAs(UnmanagedType.LPWStr)]
            string fileName);

        void GetCurFile(
            [MarshalAs(UnmanagedType.LPWStr)]
            out string fileName);
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct Win32FindData
    {
        public uint FileAttributes;

        public System.Runtime.InteropServices.ComTypes.FILETIME
            CreationTime;

        public System.Runtime.InteropServices.ComTypes.FILETIME
            LastAccessTime;

        public System.Runtime.InteropServices.ComTypes.FILETIME
            LastWriteTime;

        public uint FileSizeHigh;

        public uint FileSizeLow;

        public uint Reserved0;

        public uint Reserved1;

        [MarshalAs(
            UnmanagedType.ByValTStr,
            SizeConst = 260)]
        public string FileName;

        [MarshalAs(
            UnmanagedType.ByValTStr,
            SizeConst = 14)]
        public string AlternateFileName;
    }
}