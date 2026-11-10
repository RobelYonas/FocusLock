using FocusLock.Core.Abstractions;

namespace FocusLock.Tests.Fakes;

public sealed class FakeAppEnvironment : IAppEnvironment
{
    public string DataDirectory { get; set; } = @"C:\Users\test\AppData\Roaming\FocusLock";

    public string LogDirectory { get; set; } = @"C:\Users\test\AppData\Roaming\FocusLock\logs";

    public string ProgramDataDirectory { get; set; } = @"C:\ProgramData";

    public string ProgramFilesX86Directory { get; set; } = @"C:\Program Files (x86)";

    public string WindowsDirectory { get; set; } = @"C:\Windows";

    public string ExecutablePath { get; set; } = @"C:\Apps\FocusLock\FocusLock.exe";

    public int ProcessId { get; set; } = 4242;
}
