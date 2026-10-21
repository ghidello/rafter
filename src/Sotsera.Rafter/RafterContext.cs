using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>The invocation's prepared input values, output, and cooperative cancellation scope.</summary>
public sealed class RafterContext
{
    private readonly InvocationOutput _output;
    private readonly InputValues _values;
    private readonly ProcessInvocation _invocation;
    private readonly BodyProcesses? _processes;

    /// <summary>Signals that the invocation should stop and finish its cleanup.</summary>
    public CancellationToken CancellationToken { get; }

    internal string RootDirectory { get; }
    internal bool ProcessFailure => _invocation.Failed;

    internal RafterContext(string rootDirectory, InvocationOutput output, CancellationToken cancellationToken,
        InputValues values, ProcessInvocation invocation, BodyProcesses? processes = null)
    {
        RootDirectory = rootDirectory;
        _output = output;
        _values = values;
        CancellationToken = cancellationToken;
        _invocation = invocation;
        _processes = processes;
    }

    /// <summary>Writes a literal result line to stdout, preserving embedded newlines and indentation.</summary>
    public void WriteLine(string? text = null) => _output.WriteLine(text);

    /// <summary>Configures a child process owned by this body, with the captured invocation root as its directory.</summary>
    public ProcessCommand Process(string executable)
    {
        return new(_processes ?? throw new InvalidOperationException("A process requires an executing body."),
            executable, RootDirectory);
    }

    internal async Task RunBodyAsync(Func<RafterContext, Task> body, string name)
    {
        var processes = new BodyProcesses(_invocation, name);
        var context = new RafterContext(RootDirectory, _output, CancellationToken, _values, _invocation, processes);
        try
        {
            var task = body(context) ?? throw new InvalidOperationException($"{name} returned a null task.");
            await task.ConfigureAwait(false);
        }
        finally
        {
            await processes.FinishAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Returns the value prepared before execution for a required or defaulted input.</summary>
    public T GetValue<T>(ValueOption<T> option) => _values.GetRequired(option);

    /// <summary>Returns the supplied string, or null when the optional input was omitted.</summary>
    public string? GetValue(Option<string> option) => _values.GetString(option);

    /// <summary>Returns the supplied value, or null when the optional input was omitted.</summary>
    public T? GetValue<T>(Option<T> option) where T : struct => _values.GetOptional(option);
}
