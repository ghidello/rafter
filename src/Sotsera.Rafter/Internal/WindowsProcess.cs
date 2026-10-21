using System.Collections;
using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Sotsera.Rafter.Internal;

/// <summary>Owns a job from process creation, including every inherited descendant and both output pipes.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsProcess : IOwnedProcess
{
    private const uint KillOnJobClose = 0x2000;
    private const uint UseStandardHandles = 0x100;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint CreateNewProcessGroup = 0x00000200;
    private const uint CreateNoWindow = 0x08000000;
    private const nuint AttributeJobList = 0x2000D;
    private const nuint AttributeHandleList = 0x20002;
    private readonly KernelHandle _job;
    private readonly KernelHandle _process;
    private readonly uint _group;
    private readonly bool _console;

    public StreamReader Output { get; }
    public StreamReader Error { get; }

    public int? ExitCode
    {
        get
        {
            var wait = Native.WaitForSingleObject(_process, 0);
            if (wait == 258)
            {
                return null;
            }
            Check(wait == 0);
            Check(Native.GetExitCodeProcess(_process, out var code));
            return unchecked((int)code);
        }
    }

    public bool IsEmpty
    {
        get
        {
            Check(Native.QueryInformationJobObject(_job, 1, out var accounting,
                (uint)Marshal.SizeOf<Native.Accounting>(), IntPtr.Zero));
            return accounting.ActiveProcesses == 0;
        }
    }

    private WindowsProcess(KernelHandle job, KernelHandle process, uint group, bool console,
        NamedPipeServerStream output, NamedPipeServerStream error)
    {
        _job = job;
        _process = process;
        _group = group;
        _console = console;
        Output = new(output, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        Error = new(error, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
    }

    public static WindowsProcess Start(string executable, string[] arguments, string directory,
        IReadOnlyDictionary<string, string> overrides)
    {
        using var stdout = new OutputPipe();
        using var stderr = new OutputPipe();
        using var stdin = InputHandle();
        var job = Native.CreateJobObjectW(IntPtr.Zero, null);
        KernelHandle? process = null;
        try
        {
            Check(!job.IsInvalid);
            var limits = new Native.ExtendedLimits();
            limits.Basic.LimitFlags = KillOnJobClose; // Breakaway remains disabled.
            Check(Native.SetInformationJobObject(job, 9, ref limits,
                (uint)Marshal.SizeOf<Native.ExtendedLimits>()));
            using var attributes = new Attributes(job, stdin, stdout.Client.SafePipeHandle, stderr.Client.SafePipeHandle);
            var startup = new Native.StartupInfoEx();
            startup.Startup.Size = Marshal.SizeOf<Native.StartupInfoEx>();
            startup.Startup.Flags = UseStandardHandles;
            startup.Startup.Input = stdin.DangerousGetHandle();
            startup.Startup.Output = stdout.Client.SafePipeHandle.DangerousGetHandle();
            startup.Startup.Error = stderr.Client.SafePipeHandle.DangerousGetHandle();
            startup.Attributes = attributes.Pointer;
            var console = Native.GetConsoleProcessList(new uint[1], 1) != 0;
            var flags = ExtendedStartupInfoPresent | CreateUnicodeEnvironment | CreateNewProcessGroup;
            if (!console)
            {
                flags |= CreateNoWindow;
            }
            var environment = EnvironmentBlock(overrides);
            var commandLine = new StringBuilder(string.Join(" ", new[] { executable }.Concat(arguments).Select(Quote)));
            Check(Native.CreateProcessW(null, commandLine, IntPtr.Zero, IntPtr.Zero, true, flags,
                environment, directory, ref startup, out var info));
            process = new(info.Process);
            using var thread = new KernelHandle(info.Thread);
            return new(job, process, info.ProcessId, console, stdout.TakeServer(), stderr.TakeServer());
        }
        catch
        {
            // Closing the non-inherited job is also the last-resort cleanup if construction fails after launch.
            job.Dispose();
            process?.Dispose();
            throw;
        }
    }

    public bool RequestGracefulStop() => _console && Native.GenerateConsoleCtrlEvent(1, _group);

    public void ForceStop() => Check(Native.TerminateJobObject(_job, 1));

    public void Dispose()
    {
        _job.Dispose();
        _process.Dispose();
        Output.Dispose();
        Error.Dispose();
    }

    private static KernelHandle InputHandle()
    {
        var input = Native.GetStdHandle(-10);
        if (input != IntPtr.Zero && input != new IntPtr(-1))
        {
            if (Native.DuplicateHandle(new IntPtr(-1), input, new IntPtr(-1), out var copy, 0, true, 2))
            {
                return copy;
            }
            var error = Marshal.GetLastPInvokeError();
            copy.Dispose();
            if (error != 6) // ERROR_INVALID_HANDLE: a detached console can leave an unusable stdin handle.
            {
                throw new Win32Exception(error);
            }
        }
        var security = new Native.SecurityAttributes { Length = Marshal.SizeOf<Native.SecurityAttributes>(), Inherit = true };
        var empty = Native.CreateFileW("NUL", 0x80000000, 3, ref security, 3, 0, IntPtr.Zero);
        Check(!empty.IsInvalid);
        return empty;
    }

    private static string EnvironmentBlock(IReadOnlyDictionary<string, string> overrides)
    {
        var values = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            values[(string)entry.Key] = (string)entry.Value!;
        }
        foreach (var entry in overrides)
        {
            values[entry.Key] = entry.Value;
        }
        return string.Join('\0', values.Select(entry => entry.Key + "=" + entry.Value)) + "\0\0";
    }

    // Windows argv quoting: double backslashes before quotes and before the final closing quote.
    private static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\')
            {
                slashes++;
                continue;
            }
            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            result.Append(character);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }

    private static void Check(bool succeeded)
    {
        if (!succeeded)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    private sealed class OutputPipe : IDisposable
    {
        private NamedPipeServerStream? _server;
        public NamedPipeClientStream Client { get; }

        public OutputPipe()
        {
            var name = "rafter-" + Guid.NewGuid().ToString("N");
            _server = new(name, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            Client = new(".", name, PipeDirection.Out, PipeOptions.None,
                System.Security.Principal.TokenImpersonationLevel.Anonymous, HandleInheritability.Inheritable);
            var connected = _server.WaitForConnectionAsync();
            try
            {
                Client.Connect(1000);
                connected.GetAwaiter().GetResult();
            }
            catch
            {
                Client.Dispose();
                _server.Dispose();
                try
                {
                    connected.GetAwaiter().GetResult();
                }
                catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                {
                    // Closing the server interrupts its pending connection; observe its completion before leaving.
                }
                throw;
            }
        }

        public NamedPipeServerStream TakeServer()
        {
            var server = _server!;
            _server = null;
            return server;
        }

        public void Dispose()
        {
            Client.Dispose();
            _server?.Dispose();
        }
    }

    private sealed class Attributes : IDisposable
    {
        private readonly IntPtr _handles;
        private readonly IntPtr _jobs;
        private bool _initialized;
        public IntPtr Pointer { get; }

        public Attributes(KernelHandle job, KernelHandle input, SafePipeHandle output, SafePipeHandle error)
        {
            nuint size = 0;
            Native.InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
            Pointer = Marshal.AllocHGlobal(checked((int)size));
            _handles = Marshal.AllocHGlobal(IntPtr.Size * 3);
            _jobs = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                Check(Native.InitializeProcThreadAttributeList(Pointer, 2, 0, ref size));
                _initialized = true;
                Marshal.WriteIntPtr(_jobs, job.DangerousGetHandle());
                Marshal.WriteIntPtr(_handles, input.DangerousGetHandle());
                Marshal.WriteIntPtr(_handles, IntPtr.Size, output.DangerousGetHandle());
                Marshal.WriteIntPtr(_handles, IntPtr.Size * 2, error.DangerousGetHandle());
                Check(Native.UpdateProcThreadAttribute(Pointer, 0, AttributeJobList, _jobs, (nuint)IntPtr.Size,
                    IntPtr.Zero, IntPtr.Zero));
                Check(Native.UpdateProcThreadAttribute(Pointer, 0, AttributeHandleList, _handles, (nuint)(IntPtr.Size * 3),
                    IntPtr.Zero, IntPtr.Zero));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_initialized)
            {
                Native.DeleteProcThreadAttributeList(Pointer);
            }
            Marshal.FreeHGlobal(Pointer);
            Marshal.FreeHGlobal(_handles);
            Marshal.FreeHGlobal(_jobs);
        }
    }

    private sealed class KernelHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public KernelHandle() : base(true)
        {
        }

        public KernelHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle() => Native.CloseHandle(handle);
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct StartupInfo
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
        internal struct StartupInfoEx
        {
            public StartupInfo Startup;
            public IntPtr Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ProcessInformation
        {
            public IntPtr Process;
            public IntPtr Thread;
            public uint ProcessId;
            public uint ThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BasicLimits
        {
            public long ProcessTime;
            public long JobTime;
            public uint LimitFlags;
            public nuint MinimumWorkingSet;
            public nuint MaximumWorkingSet;
            public uint ActiveProcessLimit;
            public nuint Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ExtendedLimits
        {
            public BasicLimits Basic;
            public ulong ReadOperations;
            public ulong WriteOperations;
            public ulong OtherOperations;
            public ulong ReadBytes;
            public ulong WriteBytes;
            public ulong OtherBytes;
            public nuint ProcessMemory;
            public nuint JobMemory;
            public nuint PeakProcessMemory;
            public nuint PeakJobMemory;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Accounting
        {
            public long UserTime;
            public long KernelTime;
            public long PeriodUserTime;
            public long PeriodKernelTime;
            public uint PageFaults;
            public uint TotalProcesses;
            public uint ActiveProcesses;
            public uint TerminatedProcesses;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SecurityAttributes
        {
            public int Length;
            public IntPtr Descriptor;
            [MarshalAs(UnmanagedType.Bool)] public bool Inherit;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern KernelHandle CreateJobObjectW(IntPtr attributes, string? name);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetInformationJobObject(KernelHandle job, int type, ref ExtendedLimits limits, uint size);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool QueryInformationJobObject(KernelHandle job, int type, out Accounting accounting, uint size, IntPtr length);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool TerminateJobObject(KernelHandle job, uint code);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, uint flags, ref nuint size);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, nuint attribute, IntPtr value, nuint size, IntPtr previous, IntPtr returned);
        [DllImport("kernel32.dll")]
        internal static extern void DeleteProcThreadAttributeList(IntPtr list);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool CreateProcessW(string? application, StringBuilder command, IntPtr processAttributes,
            IntPtr threadAttributes, bool inherit, uint flags, string environment, string directory,
            ref StartupInfoEx startup, out ProcessInformation info);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetExitCodeProcess(KernelHandle process, out uint code);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint WaitForSingleObject(KernelHandle handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GenerateConsoleCtrlEvent(uint signal, uint group);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint GetConsoleProcessList([Out] uint[] processes, uint size);
        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetStdHandle(int kind);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool DuplicateHandle(IntPtr sourceProcess, IntPtr source, IntPtr targetProcess,
            out KernelHandle target, uint access, bool inherit, uint options);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern KernelHandle CreateFileW(string name, uint access, uint share,
            ref SecurityAttributes security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll")]
        internal static extern bool CloseHandle(IntPtr handle);
    }
}
