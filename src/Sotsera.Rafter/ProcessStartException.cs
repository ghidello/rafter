namespace Sotsera.Rafter;

/// <summary>Represents failure to start a child process.</summary>
public sealed class ProcessStartException : ProcessException
{
    internal ProcessStartException(Exception? innerException)
        : base("The process could not be started.", innerException)
    {
    }
}
