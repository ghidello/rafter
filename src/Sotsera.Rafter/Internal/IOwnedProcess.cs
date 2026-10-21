namespace Sotsera.Rafter.Internal;

// The lifecycle supervisor observes ownership independently of the root exit and output streams.
internal interface IOwnedProcess : IDisposable
{
    StreamReader Output { get; }
    StreamReader Error { get; }
    int? ExitCode { get; }
    bool IsEmpty { get; }
    bool RequestGracefulStop();
    void ForceStop();
}
