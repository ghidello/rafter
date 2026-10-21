namespace Sotsera.Rafter.Internal;

internal sealed class ProcessLaunchException(string executable, string directory, Exception inner)
    : Exception($"Process launch failed | {executable}", inner)
{
    public string Directory { get; } = directory;
}

internal sealed class ProcessHandlerException(ProcessFailureException failure, Exception inner)
    : Exception($"Process failure handler threw | {failure.Executable}", inner)
{
    public ProcessFailureException Failure { get; } = failure;
}

internal sealed class ProcessCompletionException(string executable, int? exitCode, bool terminationUnknown,
    Exception? inner = null)
    : Exception($"{(terminationUnknown ? "Termination unknown" : "Output incomplete")} | {executable}", inner)
{
    public int? ExitCode { get; } = exitCode;
}
