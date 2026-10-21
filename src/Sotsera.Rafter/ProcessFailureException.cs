namespace Sotsera.Rafter;

/// <summary>A completed process returned an exit code outside its accepted set.</summary>
public sealed class ProcessFailureException : Exception
{
    public ProcessResult Result { get; }
    public string Executable { get; }
    public IReadOnlyList<int> AcceptedExitCodes { get; }

    internal ProcessFailureException(string executable, int[] accepted, ProcessResult result)
        : base($"Process '{executable}' exited with code {result.ExitCode}; accepted: {string.Join(", ", accepted)}.")
    {
        Executable = executable;
        Result = result;
        AcceptedExitCodes = Array.AsReadOnly(accepted);
    }
}
