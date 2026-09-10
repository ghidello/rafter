using System.Diagnostics;

namespace Sotsera.Rafter;

internal sealed class SystemProcessAdapter : IProcessAdapter
{
    private readonly Process _process;

    private SystemProcessAdapter(ProcessStartInfo startInfo)
    {
        _process = new Process { StartInfo = startInfo };
    }

    public Stream StandardOutput => _process.StandardOutput.BaseStream;

    public Stream StandardError => _process.StandardError.BaseStream;

    public int Id => _process.Id;

    public bool HasExited => _process.HasExited;

    public int ExitCode => _process.ExitCode;

    internal static IProcessAdapterFactory Factory { get; } = new AdapterFactory();

    public bool Start() => _process.Start();

    public Task WaitForExitAsync() => _process.WaitForExitAsync(CancellationToken.None);

    public void KillTree() => _process.Kill(entireProcessTree: true);

    public void CloseOutput()
    {
        Exception? outputFailure = TryDispose(_process.StandardOutput);
        Exception? errorFailure = TryDispose(_process.StandardError);
        if (outputFailure is not null && errorFailure is not null)
        {
            throw new AggregateException(outputFailure, errorFailure);
        }

        if (outputFailure is not null || errorFailure is not null)
        {
            throw outputFailure ?? errorFailure!;
        }
    }

    public void Dispose() => _process.Dispose();

    private static Exception? TryDispose(StreamReader disposable)
    {
        try
        {
            disposable.Dispose();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private sealed class AdapterFactory : IProcessAdapterFactory
    {
        public IProcessAdapter Create(ProcessStartInfo startInfo) => new SystemProcessAdapter(startInfo);
    }
}
