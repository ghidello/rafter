using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace ProcessProbe;

internal static class Breakaway
{
    public static bool TryLaunch()
    {
        var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
        // Keep any unexpectedly escaped child suspended, then terminate it before returning a failed probe.
        if (!CreateProcessW(null, new StringBuilder("dotnet --version"), IntPtr.Zero, IntPtr.Zero, false,
            0x01000000 | 0x00000004 | 0x08000000, IntPtr.Zero, null, ref startup, out var process))
        {
            var error = Marshal.GetLastPInvokeError();
            if (error != 5)
            {
                throw new Win32Exception(error);
            }
            return false;
        }
        try
        {
            if (!TerminateProcess(process.Process, 1) || WaitForSingleObject(process.Process, 5000) != 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
            return true;
        }
        finally
        {
            CloseHandle(process.Thread);
            CloseHandle(process.Process);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved;
        public IntPtr Desktop;
        public IntPtr Title;
        public uint X;
        public uint Y;
        public uint XSize;
        public uint YSize;
        public uint XCountChars;
        public uint YCountChars;
        public uint FillAttribute;
        public uint Flags;
        public ushort ShowWindow;
        public ushort ReservedBytes;
        public IntPtr ReservedPointer;
        public IntPtr Input;
        public IntPtr Output;
        public IntPtr Error;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process;
        public IntPtr Thread;
        public uint ProcessId;
        public uint ThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessW(string? application, StringBuilder command, IntPtr processAttributes,
        IntPtr threadAttributes, bool inherit, uint flags, IntPtr environment, string? directory,
        ref StartupInfo startup, out ProcessInformation info);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr process, uint code);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr process, uint milliseconds);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}
