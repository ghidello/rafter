namespace Sotsera.Rafter;

internal sealed class OutputScope
{
    private readonly string? _targetName;
    private int _closed;

    internal OutputScope(string? targetName)
    {
        _targetName = targetName;
    }

    internal string GetName()
        => _targetName is not null && Volatile.Read(ref _closed) == 0 ? _targetName : "command";

    internal void Close() => Volatile.Write(ref _closed, 1);
}
