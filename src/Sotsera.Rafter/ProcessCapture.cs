namespace Sotsera.Rafter;

/// <summary>Represents the successful, bounded capture of a child process.</summary>
public sealed record ProcessCapture
{
    /// <summary>Initializes a process capture.</summary>
    public ProcessCapture(int exitCode, string standardOutput, string standardError)
    {
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    /// <summary>Gets the child-process exit code.</summary>
    public int ExitCode { get; }

    /// <summary>Gets the exact decoded standard output.</summary>
    public string StandardOutput { get; }

    /// <summary>Gets the exact decoded standard error.</summary>
    public string StandardError { get; }
}
