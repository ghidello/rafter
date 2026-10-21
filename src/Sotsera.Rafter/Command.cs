using System.Diagnostics;
using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>Command metadata. Each invocation supplies its own body and owns its execution state.</summary>
public sealed class Command
{
    private readonly string _description;
    private readonly Func<InvocationHost> _captureHost;
    private readonly object _declarationGate = new();
    private readonly List<OptionDeclaration> _options = [];
    private readonly List<CombinedRule> _rules = [];
    private readonly List<Target> _targets = [];

    internal Command(string description, Func<InvocationHost>? captureHost = null)
    {
        _description = description;
        _captureHost = captureHost ?? InvocationHost.Capture;
    }

    /// <summary>Declares an optional typed input. Presence and default policies can finalize its value contract.</summary>
    public Option<T> Option<T>(string name, string description)
    {
        lock (_declarationGate)
        {
            var declaration = new OptionDeclaration(_declarationGate, name, description, typeof(T));
            _options.Add(declaration);
            return new(declaration);
        }
    }

    /// <summary>Declares a named body or dependency-only group.</summary>
    public Target Target(string name, string description)
    {
        lock (_declarationGate)
        {
            var target = new Target(_declarationGate, name, description);
            _targets.Add(target);
            return target;
        }
    }

    /// <summary>Adds a rule evaluated after all individual input rules succeed.</summary>
    public Command Validate(string description, Func<InputValidationContext, bool> predicate)
    {
        lock (_declarationGate)
        {
            _rules.Add(new(description, predicate));
        }
        return this;
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

    /// <summary>Runs selected targets and their prerequisites sequentially, executing each body at most once.</summary>
    public Task<int> RunAsync(string[] args, Target target, params Target[] additionalTargets)
        => InvokeAsync(args, null, target, additionalTargets, targetInvocation: true);

    private async Task<int> InvokeAsync(string[] args, Func<RafterContext, Task>? body,
        Target? entry = null, Target[]? additionalTargets = null, bool targetInvocation = false)
    {
        InvocationHost? host = null;
        InvocationOutput? output = null;
        InvocationCancellation? cancellation = null;
        IDisposable? subscription = null;
        TargetExecution? targetExecution = null;
        ProcessInvocation? processes = null;
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
            OptionSnapshot[] options;
            CombinedRule[] rules;
            TargetSnapshot[] targets;
            Target[]? entries = null;
            lock (_declarationGate)
            {
                options = _options.Select(option => option.Snapshot()).ToArray();
                rules = _rules.ToArray();
                targets = _targets.Select(target => target.Snapshot()).ToArray();
                if (targetInvocation)
                {
                    entries = [entry!, .. additionalTargets ?? []];
                }
            }
            var declarations = new List<string>();
            if (string.IsNullOrWhiteSpace(_description))
            {
                declarations.Add("Command description must contain non-whitespace text.");
            }
            if (!targetInvocation && body is null)
            {
                declarations.Add("Command body must not be null.");
            }
            if (targetInvocation && additionalTargets is null)
            {
                declarations.Add("Additional target array must not be null.");
            }
            var plan = TargetPlan.Prepare(targets, entries, declarations);
            if (plan is not null)
            {
                targetExecution = new(plan);
            }
            var spellings = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < options.Length; index++)
            {
                options[index].ValidateDeclaration(index, declarations, spellings);
            }
            foreach (var rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Description) || rule.Predicate is null)
                {
                    declarations.Add("Combined rules must have a description and a predicate.");
                }
            }

            // Honor an explicit presentation switch even when declaration validation prevents parsing.
            var arguments = declarations.Count == 0 ? InvocationArguments.Parse(args, options)
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
                output.Help(_description, options, rules, plan);
                code = 0;
            }
            else
            {
                cancellation = new InvocationCancellation(output);
                try
                {
                    subscription = host.SubscribeCancellation(cancellation.Request);
                    var validation = InputValidation.Evaluate(options, rules, arguments.Values, cancellation.Token);
                    if (validation.Diagnostics.Length > 0)
                    {
                        output.Invalid("Invalid input", validation.Diagnostics);
                    }
                    processes = new(cancellation, output);
                    var context = new RafterContext(host.RootDirectory, output, cancellation.Token, arguments.Values, processes);
                    try
                    {
                        if (validation.Exception is { } validationException)
                        {
                            if (validationException is OperationCanceledException cancelled
                                && cancellation.Requested && cancelled.CancellationToken == cancellation.Token)
                            {
                                code = 130;
                            }
                            else
                            {
                                Report(validationException, validation.Scope!);
                            }
                        }
                        else if (validation.Diagnostics.Length > 0)
                        {
                            code = 2;
                        }
                        else
                        {
                            context.CancellationToken.ThrowIfCancellationRequested();
                            executed = true;
                            output.Start(_description, plan);
                            if (targetExecution is not null)
                            {
                                await targetExecution.RunAsync(context, output).ConfigureAwait(false);
                            }
                            else
                            {
                                await context.RunBodyAsync(body!, "The command body").ConfigureAwait(false);
                            }
                            code = processes.Failed ? 1 : 0;
                        }
                    }
                    catch (OperationCanceledException exception) when (
                        cancellation.Requested && exception.CancellationToken == cancellation.Token)
                    {
                        code = 130;
                    }
                    catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                    {
                        Report(exception, targetExecution?.Current is { } current
                            ? $"Target '{current.Name}'" : "Command body");
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
            if (processes is { Failed: true })
            {
                code = 1;
                foreach (var diagnostic in processes.Diagnostics)
                {
                    Report(diagnostic, "Process execution");
                }
            }
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
                    output.Summary(code, elapsed.Elapsed, targetExecution);
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
