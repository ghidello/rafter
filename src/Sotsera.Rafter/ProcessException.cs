namespace Sotsera.Rafter;

/// <summary>Represents an unclassified process failure or invalid process specification.</summary>
public class ProcessException : RafterException
{
    internal ProcessException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
