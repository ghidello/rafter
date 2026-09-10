namespace Sotsera.Rafter;

/// <summary>Represents a redirected process-output failure.</summary>
public sealed class ProcessOutputException : ProcessException
{
    internal ProcessOutputException(
        ProcessOutputReason reason,
        ProcessOutputStream stream,
        long? limitBytes = null,
        Exception? innerException = null)
        : base(GetMessage(reason), innerException)
    {
        Reason = reason;
        Stream = stream;
        LimitBytes = limitBytes;
    }

    /// <summary>Gets the output failure reason.</summary>
    public ProcessOutputReason Reason { get; }

    /// <summary>Gets the affected redirected stream.</summary>
    public ProcessOutputStream Stream { get; }

    /// <summary>Gets the configured per-stream byte limit for capture overflow.</summary>
    public long? LimitBytes { get; }

    private static string GetMessage(ProcessOutputReason reason)
        => reason switch
        {
            ProcessOutputReason.CaptureLimitExceeded => "The process exceeded its capture limit.",
            ProcessOutputReason.InvalidUtf8 => "The process emitted invalid UTF-8.",
            ProcessOutputReason.RetainedPipe => "Process output did not close after the direct child exited.",
            _ => "Process output failed.",
        };
}
