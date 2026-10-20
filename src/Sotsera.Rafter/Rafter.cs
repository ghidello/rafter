namespace Sotsera.Rafter;

/// <summary>Declares commands for small automation scripts.</summary>
public static class Rafter
{
    /// <summary>Declares a command. Its description is validated when RunAsync begins.</summary>
    public static Command Command(string description) => new(description);
}
