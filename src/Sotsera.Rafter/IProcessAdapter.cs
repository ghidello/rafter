namespace Sotsera.Rafter;

internal interface IProcessAdapter : IDisposable
{
    Stream StandardOutput { get; }

    Stream StandardError { get; }

    int Id { get; }

    bool HasExited { get; }

    int ExitCode { get; }

    bool Start();

    Task WaitForExitAsync();

    void KillTree();

    void CloseOutput();
}
