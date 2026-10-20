namespace Sotsera.Rafter.Internal;

internal static class ExceptionPolicy
{
    public static bool IsRecoverable(Exception exception) =>
        exception is not (OutOfMemoryException or StackOverflowException or AccessViolationException);
}
