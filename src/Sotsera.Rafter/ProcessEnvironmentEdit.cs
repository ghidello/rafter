namespace Sotsera.Rafter;

internal sealed record ProcessEnvironmentEdit(
    ProcessEnvironmentEditKind Kind,
    string? Name,
    ProcessValue Value,
    long Sequence,
    int Order);
