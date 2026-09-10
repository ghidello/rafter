namespace Sotsera.Rafter;

/// <summary>Represents expiration of an authored process timeout.</summary>
public sealed class ProcessTimeoutException : ProcessException
{
    internal ProcessTimeoutException(TimeSpan timeout, Exception? innerException = null)
        : base("The process exceeded its configured timeout.", innerException)
    {
        Timeout = timeout;
    }

    /// <summary>Gets the authored timeout.</summary>
    public TimeSpan Timeout { get; }
}
