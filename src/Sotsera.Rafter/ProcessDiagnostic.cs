namespace Sotsera.Rafter;

internal sealed record ProcessDiagnostic(string Code, string Message, long Sequence, int Order = 0);
