using System.Collections.Immutable;

namespace Sotsera.Rafter;

internal sealed record ProcessSpecification(
    ProcessValue Executable,
    ImmutableArray<ProcessValue> Arguments,
    ImmutableArray<ProcessEnvironmentEdit> EnvironmentEdits,
    ImmutableArray<ProcessDiagnostic> Diagnostics,
    string? WorkingDirectory,
    TimeSpan? Timeout,
    long? CaptureLimitBytes,
    ImmutableArray<int> ValidExitCodes,
    bool HasEnvironment,
    bool HasWorkingDirectory,
    bool HasTimeout,
    bool HasCaptureLimit,
    bool HasValidExitCodes,
    long NextSequence);
