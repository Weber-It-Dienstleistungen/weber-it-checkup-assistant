namespace WeberIT.Checkup.App.Models;

public enum StartupTargetType
{
    Unknown,
    Executable,
    Shortcut,
    Script,
    CommandInterpreter,
    PowerShell,
    DynamicLibrary,
    StoreApplication,
    IndirectTarget
}