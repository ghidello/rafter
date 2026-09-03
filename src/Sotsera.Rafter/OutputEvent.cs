namespace Sotsera.Rafter;

internal sealed record OutputEvent(string Scope, OutputKind Kind, string Text, string? Recovery = null);
