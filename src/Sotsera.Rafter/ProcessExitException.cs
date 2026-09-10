namespace Sotsera.Rafter;

/// <summary>Represents a child process that returned an invalid exit code.</summary>
public sealed class ProcessExitException : ProcessException
{
    internal ProcessExitException(int exitCode, ProcessCapture? capture)
        : base($"The process exited with invalid code {exitCode}.")
    {
        ExitCode = exitCode;
        Capture = capture;
    }

    /// <summary>Gets the invalid exit code.</summary>
    public int ExitCode { get; }

    /// <summary>Gets the complete capture when capture mode was used.</summary>
    public ProcessCapture? Capture { get; }
}
