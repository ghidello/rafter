namespace Sotsera.Rafter;

/// <summary>Identifies a process-output failure.</summary>
public enum ProcessOutputReason
{
    /// <summary>The configured capture limit was exceeded.</summary>
    CaptureLimitExceeded,

    /// <summary>The process emitted invalid UTF-8.</summary>
    InvalidUtf8,

    /// <summary>A descendant retained a redirected pipe after the direct child exited.</summary>
    RetainedPipe,
}
