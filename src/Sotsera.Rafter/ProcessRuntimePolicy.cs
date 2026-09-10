namespace Sotsera.Rafter;

internal sealed record ProcessRuntimePolicy(
    TimeSpan DirectExitDrainCompletion,
    TimeSpan TreeKillRequest,
    TimeSpan ForcedKillVerification,
    TimeSpan ForcedCloseDrainSettlement,
    TimeProvider TimeProvider)
{
    internal static ProcessRuntimePolicy Default { get; } = new(
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(2),
        TimeProvider.System);
}
