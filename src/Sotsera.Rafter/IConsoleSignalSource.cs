namespace Sotsera.Rafter;

internal interface IConsoleSignalSource
{
    IDisposable Subscribe(Action<ConsoleSignal> handler);
}
