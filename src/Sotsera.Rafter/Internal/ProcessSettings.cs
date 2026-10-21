namespace Sotsera.Rafter.Internal;

internal sealed record ProcessSettings(string Executable, string[] Arguments, string Directory,
    IReadOnlyDictionary<string, string> Environment, int[] AcceptedExitCodes,
    Func<ProcessResult, ProcessFailureAction>? FailureHandler);
