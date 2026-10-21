namespace Sotsera.Rafter;

/// <summary>The observed exit of a process whose owned descendants and output have finished.</summary>
public sealed class ProcessResult
{
    public bool Succeeded { get; }
    public int ExitCode { get; }

    internal ProcessResult(int exitCode, bool succeeded)
    {
        ExitCode = exitCode;
        Succeeded = succeeded;
    }
}

/// <summary>How a completed process with a rejected exit code affects its body and invocation.</summary>
public enum ProcessFailureAction
{
    Continue,
    Fail,
    Throw
}
