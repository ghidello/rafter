namespace Sotsera.Rafter;

/// <summary>Represents the successful completion of a streamed child process.</summary>
public readonly record struct ProcessExit(int ExitCode);
