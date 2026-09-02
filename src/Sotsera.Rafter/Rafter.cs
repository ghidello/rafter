namespace Sotsera.Rafter;

/// <summary>Creates Rafter commands.</summary>
public static class Rafter
{
    /// <summary>Creates a command using the supplied root policy.</summary>
    /// <param name="root">The command root policy.</param>
    /// <returns>A mutable command definition.</returns>
    public static Command Command(Root root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return new Command(root);
    }
}
