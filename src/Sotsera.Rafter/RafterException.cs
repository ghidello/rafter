namespace Sotsera.Rafter;

/// <summary>Base class for Rafter operational failures.</summary>
public abstract class RafterException : Exception
{
    private protected RafterException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
