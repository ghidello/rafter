using System.Diagnostics;
using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>Command metadata. Each invocation supplies its own body and owns its execution state.</summary>
public sealed class Command
{
    private readonly string _description;
    private readonly Func<InvocationHost> _captureHost;

    internal Command(string description, Func<InvocationHost>? captureHost = null)
    {
        _description = description;
        _captureHost = captureHost ?? InvocationHost.Capture;
    }

    /// <summary>Validates and executes a synchronous body, returning its invocation exit code.</summary>
    public Task<int> RunAsync(string[] args, Action<RafterContext> body)
    {
        return InvokeAsync(args, body is null ? null : context =>
        {
            body(context);
            return Task.CompletedTask;
        });
    }

    /// <summary>Validates and awaits a body, including its output, returning its invocation exit code.</summary>
    public Task<int> RunAsync(string[] args, Func<RafterContext, Task> body) => InvokeAsync(args, body);

    private async Task<int> InvokeAsync(string[] args, Func<RafterContext, Task>? body)
    {
        InvocationHost? host = null;
        InvocationOutput? output = null;
        InvocationCancellation? cancellation = null;
        IDisposable? subscription = null;
        var code = 1;
        var executed = false;
        var reported = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var elapsed = Stopwatch.StartNew();

        void Report(Exception exception, string scope)
        {
            code = 1;
            if (!reported.Add(exception))
            {
                return;
            }
            try
            {
                if (output is not null)
                {
                    output.Exception(exception, scope);
                }
                else
                {
                    var writer = host?.Error ?? Console.Error;
                    writer.WriteLine($"{scope} failed{Environment.NewLine}{exception}");
                    writer.Flush();
                }
            }
            catch (Exception reportingFailure) when (ExceptionPolicy.IsRecoverable(reportingFailure))
            {
                // A renderer can be closed after a flush failure while the host writer still accepts diagnostics.
                try
                {
                    var writer = host?.Error ?? Console.Error;
                    writer.WriteLine($"{scope} failed{Environment.NewLine}{exception}");
                    writer.Flush();
                }
                catch (Exception fallbackFailure) when (ExceptionPolicy.IsRecoverable(fallbackFailure))
                {
                    // There may be no usable destination. The exit code still communicates failure.
                }
            }
        }

        try
        {
            // This executes synchronously at invocation entry, before the first await or user callback.
            host = _captureHost();
            var declarations = new List<string>();
            if (string.IsNullOrWhiteSpace(_description))
            {
                declarations.Add("Command description must contain non-whitespace text.");
            }
            if (body is null)
            {
                declarations.Add("Command body must not be null.");
            }

            // Honor an explicit presentation switch even when declaration validation prevents parsing.
            var arguments = declarations.Count == 0 ? InvocationArguments.Parse(args)
                : new InvocationArguments(false, false, false, args?.Contains("--plain") == true, []);
            output = new InvocationOutput(host, arguments);
            if (declarations.Count > 0)
            {
                output.Invalid("Invalid command", declarations);
                code = 2;
            }
            else if (arguments.Diagnostics.Length > 0)
            {
                output.Invalid("Invalid input", arguments.Diagnostics);
                code = 2;
            }
            else if (arguments.Help)
            {
                output.Help(_description);
                code = 0;
            }
            else
            {
                executed = true;
                cancellation = new InvocationCancellation(output);
                try
                {
                    subscription = host.SubscribeCancellation(cancellation.Request);
                    output.Start(_description);
                    var context = new RafterContext(host.RootDirectory, output, cancellation.Token);
                    try
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();
                        var task = body!(context)
                            ?? throw new InvalidOperationException("The command body returned a null task.");
                        await task.ConfigureAwait(false);
                        code = 0;
                    }
                    catch (OperationCanceledException exception) when (
                        cancellation.Requested && exception.CancellationToken == cancellation.Token)
                    {
                        code = 130;
                    }
                    catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                    {
                        Report(exception, "Command body");
                    }
                }
                catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                {
                    Report(exception, "Invocation");
                }
                finally
                {
                    try
                    {
                        await cancellation.StopAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                    {
                        Report(exception, "Cancellation callback");
                    }
                }
                if (code == 0 && cancellation.Requested)
                {
                    code = 130;
                }
            }
        }
        catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
        {
            Report(exception, "Invocation");
        }

        if (output is not null)
        {
            if (output.Failure is { } failure)
            {
                Report(failure, "Output");
            }
            try
            {
                await output.CompleteOutputAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
            {
                Report(exception, "Output");
            }
            if (executed)
            {
                if (code == 0 && cancellation?.Requested == true)
                {
                    code = 130;
                }
                try
                {
                    output.Summary(code, elapsed.Elapsed);
                }
                catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                {
                    Report(exception, "Reporting");
                }
            }
            try
            {
                await output.CompleteErrorAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
            {
                Report(exception, "Output");
            }
        }

        try
        {
            subscription?.Dispose();
        }
        catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
        {
            Report(exception, "Cancellation subscription cleanup");
        }
        if (code == 0 && cancellation?.Requested == true)
        {
            code = 130;
        }
        cancellation?.Dispose();
        return code;
    }
}
