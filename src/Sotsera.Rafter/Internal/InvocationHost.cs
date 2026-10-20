namespace Sotsera.Rafter.Internal;

// Capture host state once. Injection is internal, so tests need not replace process-wide console writers.
internal sealed record InvocationHost(
    string RootDirectory,
    TextWriter Output,
    TextWriter Error,
    bool OutputRedirected,
    bool ErrorRedirected,
    bool NoColor,
    int Width,
    Func<Action, IDisposable> SubscribeCancellation)
{
    public static InvocationHost Capture()
    {
        var root = Directory.GetCurrentDirectory();
        var output = Console.Out;
        var error = Console.Error;
        var outputRedirected = Console.IsOutputRedirected;
        var errorRedirected = Console.IsErrorRedirected;
        var width = outputRedirected && errorRedirected ? 80 : Math.Max(1, Console.WindowWidth);
        return new(root, output, error, outputRedirected, errorRedirected,
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")), width,
            action => new ConsoleSubscription(action));
    }

    private sealed class ConsoleSubscription : IDisposable
    {
        private readonly ConsoleCancelEventHandler _handler;

        public ConsoleSubscription(Action action)
        {
            _handler = (_, args) =>
            {
                args.Cancel = true;
                action();
            };
            Console.CancelKeyPress += _handler;
        }

        public void Dispose() => Console.CancelKeyPress -= _handler;
    }
}
