namespace Sotsera.Rafter.RunFixture;

internal static partial class SignalFixture
{
    private const int InterruptSignal = 2;
    private const uint ControlBreakEvent = 1;

    internal static void PrepareIsolatedConsole()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _ = FreeConsole();
        if (!AllocConsole())
        {
            throw new InvalidOperationException("An isolated console could not be allocated.");
        }
    }

    internal static void SendCancellation()
    {
        if (OperatingSystem.IsWindows())
        {
            if (!GenerateConsoleCtrlEvent(ControlBreakEvent, 0))
            {
                throw new InvalidOperationException("The isolated console signal could not be generated.");
            }

            return;
        }

        if (Kill(GetProcessId(), InterruptSignal) != 0)
        {
            throw new InvalidOperationException("The process signal could not be generated.");
        }
    }

    [System.Runtime.InteropServices.LibraryImport("kernel32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool AllocConsole();

    [System.Runtime.InteropServices.LibraryImport("kernel32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool FreeConsole();

    [System.Runtime.InteropServices.LibraryImport("kernel32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool GenerateConsoleCtrlEvent(uint controlEvent, uint processGroupId);

    [System.Runtime.InteropServices.LibraryImport("libc", EntryPoint = "getpid")]
    private static partial int GetProcessId();

    [System.Runtime.InteropServices.LibraryImport("libc", EntryPoint = "kill")]
    private static partial int Kill(int processId, int signal);
}
