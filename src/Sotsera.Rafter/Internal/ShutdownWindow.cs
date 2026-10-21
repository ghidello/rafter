using System.Diagnostics;

namespace Sotsera.Rafter.Internal;

/// <summary>One shared grace deadline and one shared verification/drain deadline; neither restarts.</summary>
internal sealed class ShutdownWindow
{
    private readonly object _gate = new();
    private long _grace = long.MaxValue;
    private long _verification = long.MaxValue;

    public bool Started => GraceDeadline != long.MaxValue;

    public long GraceDeadline
    {
        get
        {
            lock (_gate)
            {
                return _grace;
            }
        }
    }

    public long VerificationDeadline
    {
        get
        {
            lock (_gate)
            {
                return _verification;
            }
        }
    }

    public void Request(bool force = false)
    {
        lock (_gate)
        {
            var now = Stopwatch.GetTimestamp();
            _grace = Math.Min(_grace, now + 5 * Stopwatch.Frequency);
            if (force)
            {
                _grace = Math.Min(_grace, now);
                _verification = Math.Min(_verification, now + 5 * Stopwatch.Frequency);
            }
        }
    }

    public void BeginVerification()
    {
        lock (_gate)
        {
            _verification = Math.Min(_verification, Stopwatch.GetTimestamp() + 5 * Stopwatch.Frequency);
        }
    }
}
