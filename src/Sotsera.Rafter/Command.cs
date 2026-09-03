using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static Sotsera.Rafter.BindingEngine;
using static Sotsera.Rafter.CommandModel;
using static Sotsera.Rafter.ExecutionRuntime;
using static Sotsera.Rafter.GraphPlanner;
using static Sotsera.Rafter.PathRuntime;

namespace Sotsera.Rafter;

/// <summary>Defines a Rafter command.</summary>
public sealed class Command
{
    private readonly AuthoredCommand _authored;
    private int _invocationActive;
    private ImmutableArray<string> _lastArguments = [];
    private ImmutableArray<ModelDiagnostic> _lastInvocationDiagnostics = [];

    internal Command(Root root)
    {
        _authored = new AuthoredCommand(root);
    }

    internal ModelFreezeResult FreezeResult => ModelValidation.Freeze(_authored);

    internal ImmutableArray<string> LastArguments => _lastArguments;

    internal ImmutableArray<ModelDiagnostic> LastInvocationDiagnostics => _lastInvocationDiagnostics;

    internal BindingResult? LastBindingResult { get; private set; }

    internal ExecutionOutcome? LastExecutionOutcome { get; private set; }

    internal GraphPlanningResult? LastGraphPlanningResult { get; private set; }

    internal PathRuntime.InvocationPaths? LastInvocationPaths { get; private set; }

    internal InvocationStatus? LastInvocationStatus { get; private set; }

    internal Func<InvocationServices> InvocationServicesFactory { get; set; } = InvocationServices.Capture;

    internal ConsoleCancellationCoordinator CancellationCoordinator { get; set; }
        = ConsoleCancellationCoordinator.Shared;

    internal Task? InvocationBarrier { get; set; }

    /// <summary>Sets the command description.</summary>
    public Command Description(string description)
    {
        ArgumentNullException.ThrowIfNull(description);
        long sequence = BeginMutation();
        _authored.Set(_authored.Description, description, sequence, "RAFTER1003", "The command description");
        return this;
    }

    /// <summary>Sets the maximum number of concurrently executing targets.</summary>
    public Command Concurrency(int concurrency)
    {
        long sequence = BeginMutation();
        _authored.Set(_authored.Concurrency, concurrency, sequence, "RAFTER1004", "Command concurrency");
        return this;
    }

    /// <summary>Declares an optional scalar option.</summary>
    public Option<T> Option<T>(string name)
    {
        AuthoredOption option = AddOption<T>(name, isRequired: false, isRepeated: false);
        return new Option<T>(option);
    }

    /// <summary>Declares a required scalar option.</summary>
    public RequiredOption<T> RequiredOption<T>(string name)
    {
        AuthoredOption option = AddOption<T>(name, isRequired: true, isRepeated: false);
        return new RequiredOption<T>(option);
    }

    /// <summary>Declares a repeated option.</summary>
    public RepeatedOption<T> RepeatedOption<T>(string name)
    {
        AuthoredOption option = AddOption<T>(name, isRequired: false, isRepeated: true);
        return new RepeatedOption<T>(option);
    }

    /// <summary>Declares a target.</summary>
    public Target Target(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        long sequence = BeginMutation();
        AuthoredTarget target = new(_authored, name, sequence);
        _authored.Targets.Add(target);
        return new Target(target);
    }

    /// <summary>Registers context-free synchronous command cleanup.</summary>
    public Command Finally(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return SetCleanup(new NormalizedCallback(_ =>
        {
            callback();
            return ValueTask.CompletedTask;
        }));
    }

    /// <summary>Registers context-free asynchronous command cleanup.</summary>
    public Command Finally(Func<Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return SetCleanup(new NormalizedCallback(_ => new ValueTask(callback())));
    }

    /// <summary>Registers context-aware synchronous command cleanup.</summary>
    public Command Finally(Action<RafterContext> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return SetCleanup(new NormalizedCallback(context =>
        {
            callback(context);
            return ValueTask.CompletedTask;
        }));
    }

    /// <summary>Registers context-aware asynchronous command cleanup.</summary>
    public Command Finally(Func<RafterContext, Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return SetCleanup(new NormalizedCallback(context => new ValueTask(callback(context))));
    }

    /// <summary>Freezes the command and starts an invocation.</summary>
    /// <param name="entryTarget">The target whose reachable dependency graph is executed.</param>
    /// <param name="args">The command-line arguments to parse.</param>
    /// <param name="cancellationToken">A token that requests cooperative invocation cancellation.</param>
    /// <returns>The command exit code.</returns>
    public Task<int> RunAsync(
        Target entryTarget,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entryTarget);
        ArgumentNullException.ThrowIfNull(args);
        for (int index = 0; index < args.Length; index++)
        {
            ArgumentNullException.ThrowIfNull(args[index], $"{nameof(args)}[{index}]");
        }

        if (Interlocked.CompareExchange(ref _invocationActive, 1, 0) != 0)
        {
            throw new InvalidOperationException("Overlapping invocations of the same command are not supported.");
        }

        try
        {
            _lastArguments = [.. args];
            LastBindingResult = null;
            LastExecutionOutcome = null;
            LastGraphPlanningResult = null;
            LastInvocationPaths = null;
            LastInvocationStatus = null;
            _lastInvocationDiagnostics = entryTarget.Authored.Command.Id == _authored.Id
                ? []
                : [new ModelDiagnostic(
                    "RAFTER1301",
                    "The entry target must belong to the invoked command.",
                    0,
                    null,
                    DiagnosticStage.OwnershipOrReference)];
            ModelFreezeResult freezeResult = ModelValidation.Freeze(_authored);
            return CompleteInvocationAsync(entryTarget.Authored.Id, freezeResult, cancellationToken);
        }
        catch
        {
            Volatile.Write(ref _invocationActive, 0);
            throw;
        }
    }

    private async Task<int> CompleteInvocationAsync(
        Guid entryTargetId,
        ModelFreezeResult freezeResult,
        CancellationToken cancellationToken)
    {
        try
        {
            if (InvocationBarrier is not null)
            {
                await InvocationBarrier.ConfigureAwait(false);
            }

            return await ExecuteInvocationAsync(entryTargetId, freezeResult, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _invocationActive, 0);
        }
    }

    private async Task<int> ExecuteInvocationAsync(
        Guid entryTargetId,
        ModelFreezeResult freezeResult,
        CancellationToken cancellationToken)
    {
        InvocationServices services;
        try
        {
            services = InvocationServicesFactory();
        }
        catch
        {
            LastInvocationStatus = InvocationStatus.InfrastructureFailure;
            return 1;
        }

        TextRedactor defaultRedactor = ModelValidation.CreateSensitiveDefaultRedactor(_authored);
        ImmutableArray<ModelDiagnostic> modelDiagnostics = [.. freezeResult.Diagnostics, .. _lastInvocationDiagnostics];
        if (!modelDiagnostics.IsEmpty)
        {
            return await CompleteModelFailureAsync(services, modelDiagnostics, defaultRedactor).ConfigureAwait(false);
        }

        CommandDefinition model = freezeResult.Model!;
        bool help = _lastArguments.Any(static argument => argument is "--help" or "-h");
        bool plain = _lastArguments.Any(static argument =>
            string.Equals(argument, "--plain", StringComparison.Ordinal));
        if (help)
        {
            return await CompleteHelpAsync(
                model,
                entryTargetId,
                services,
                plain,
                defaultRedactor).ConfigureAwait(false);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return await CompleteCancellationAsync(
                services,
                plain,
                defaultRedactor).ConfigureAwait(false);
        }

        InvocationStart start = new(model, entryTargetId, services, plain, defaultRedactor, cancellationToken);
        return await ExecuteNormalInvocationAsync(start).ConfigureAwait(false);
    }

    private async Task<int> ExecuteNormalInvocationAsync(InvocationStart start)
    {
        ConsoleCancellationCoordinator.Lease cancellationLease;
        try
        {
            cancellationLease = CancellationCoordinator.Register(start.CallerToken);
        }
        catch
        {
            return await CompleteInfrastructureFailureAsync(
                start.Services,
                start.Plain,
                start.Redactor,
                "Cancellation coordination could not be initialized.").ConfigureAwait(false);
        }

        using (cancellationLease)
        {
            CancellationToken invocationToken = cancellationLease.Token;
            if (invocationToken.IsCancellationRequested)
            {
                return await CompleteCancellationAsync(
                    start.Services,
                    start.Plain,
                    start.Redactor).ConfigureAwait(false);
            }

            if (!TryPlanGraph(start, out GraphPlanningResult planningResult))
            {
                return await CompleteInfrastructureFailureAsync(
                    start.Services,
                    start.Plain,
                    start.Redactor,
                    "Target graph planning failed because the frozen model was inconsistent.").ConfigureAwait(false);
            }

            if (!planningResult.IsSuccess)
            {
                return await CompleteGraphFailureAsync(start, planningResult.Diagnostics)
                    .ConfigureAwait(false);
            }

            if (invocationToken.IsCancellationRequested)
            {
                return await CompleteCancellationAsync(
                    start.Services,
                    start.Plain,
                    start.Redactor).ConfigureAwait(false);
            }

            InvocationExecution execution = new(
                start.Model,
                planningResult.Plan!,
                start.Services,
                invocationToken);
            LastBindingResult = BindingEngine.Bind(start.Model, _lastArguments, start.Services);
            return await CompleteBindingAsync(execution, LastBindingResult).ConfigureAwait(false);
        }
    }

    private bool TryPlanGraph(
        InvocationStart start,
        out GraphPlanningResult planningResult)
    {
        try
        {
            planningResult = GraphPlanner.Plan(start.Model, start.EntryTargetId);
            LastGraphPlanningResult = planningResult;
            return true;
        }
        catch
        {
            planningResult = null!;
            return false;
        }
    }

    private async Task<int> CompleteBindingAsync(
        InvocationExecution execution,
        BindingResult result)
    {
        switch (result.Status)
        {
            case BindingStatus.InputFailure:
                {
                    CommandPresentation.Report report = CommandPresentation.CreateInputFailure(
                        execution.Model,
                        execution.Plan.Targets[^1].Id,
                        execution.Services.InvocationName,
                        result.Diagnostics);
                    bool written = await TryWriteAsync(
                        report,
                        execution.Services.StandardError,
                        !result.Plain && execution.Services.StandardErrorSupportsAnsi,
                        result.Redactor).ConfigureAwait(false);
                    LastInvocationStatus = written
                        ? InvocationStatus.InputFailure
                        : InvocationStatus.InfrastructureFailure;
                    return written ? 2 : 1;
                }
            case BindingStatus.AuthorFailure:
            case BindingStatus.InfrastructureFailure:
                {
                    CommandPresentation.Report report = CommandPresentation.CreateBindingFailure(result);
                    bool written = await TryWriteAsync(
                        report,
                        execution.Services.StandardError,
                        !result.Plain && execution.Services.StandardErrorSupportsAnsi,
                        result.Redactor).ConfigureAwait(false);
                    LastInvocationStatus = !written || result.Status == BindingStatus.InfrastructureFailure
                        ? InvocationStatus.InfrastructureFailure
                        : InvocationStatus.AuthorFailure;
                    return 1;
                }
            case BindingStatus.Success:
                if (execution.CancellationToken.IsCancellationRequested)
                {
                    return await CompleteCancellationAsync(
                        execution.Services,
                        result.Plain,
                        result.Redactor).ConfigureAwait(false);
                }

                return await CompletePathInitializationAsync(execution, result).ConfigureAwait(false);
            default:
                throw new UnreachableException();
        }
    }

    private async Task<int> CompletePathInitializationAsync(
        InvocationExecution execution,
        BindingResult result)
    {
        try
        {
            LastInvocationPaths = PathRuntime.Resolve(
                execution.Model,
                execution.Plan,
                result.Snapshot!,
                execution.Services);
        }
        catch (PathPolicyException exception)
        {
            CommandPresentation.Report report = CommandPresentation.CreatePathFailure(exception.Message);
            bool written = await TryWriteAsync(
                report,
                execution.Services.StandardError,
                !result.Plain && execution.Services.StandardErrorSupportsAnsi,
                result.Redactor).ConfigureAwait(false);
            LastInvocationStatus = written ? InvocationStatus.PathFailure : InvocationStatus.InfrastructureFailure;
            return written ? 2 : 1;
        }
        catch
        {
            CommandPresentation.Report report = CommandPresentation.CreatePathFailure(
                "Command path initialization failed because filesystem metadata was unavailable.");
            _ = await TryWriteAsync(
                report,
                execution.Services.StandardError,
                !result.Plain && execution.Services.StandardErrorSupportsAnsi,
                result.Redactor).ConfigureAwait(false);
            LastInvocationStatus = InvocationStatus.InfrastructureFailure;
            return 1;
        }

        ExecutionScope scope = new(
            execution.Model,
            execution.Plan,
            result.Snapshot!,
            LastInvocationPaths,
            execution.Services.FileSystem,
            execution.CancellationToken);
        LastExecutionOutcome = await ExecutionRuntime.ExecuteAsync(scope).ConfigureAwait(false);
        return await CompleteExecutionAsync(execution.Services, result, LastExecutionOutcome).ConfigureAwait(false);
    }

    private async Task<int> CompleteExecutionAsync(
        InvocationServices services,
        BindingResult result,
        ExecutionOutcome outcome)
    {
        if (outcome.ExitCode == 0)
        {
            LastInvocationStatus = InvocationStatus.Success;
            return 0;
        }

        CommandPresentation.Report outcomeReport = outcome.ExitCode == 130
            ? CommandPresentation.CreateCancellation(outcome)
            : CommandPresentation.CreateExecutionFailure(outcome);
        bool outcomeWritten = await TryWriteAsync(
            outcomeReport,
            services.StandardError,
            !result.Plain && services.StandardErrorSupportsAnsi,
            result.Redactor).ConfigureAwait(false);
        if (!outcomeWritten)
        {
            LastInvocationStatus = InvocationStatus.InfrastructureFailure;
            return 1;
        }

        LastInvocationStatus = outcome.ExitCode == 130
            ? InvocationStatus.Cancelled
            : InvocationStatus.ExecutionFailure;
        return outcome.ExitCode;
    }

    private async Task<int> CompleteModelFailureAsync(
        InvocationServices services,
        ImmutableArray<ModelDiagnostic> diagnostics,
        TextRedactor redactor)
    {
        CommandPresentation.Report report = CommandPresentation.CreateModelFailure(diagnostics);
        bool written = await TryWriteAsync(
            report,
            services.StandardError,
            services.StandardErrorSupportsAnsi,
            redactor).ConfigureAwait(false);
        LastInvocationStatus = written ? InvocationStatus.InvalidModel : InvocationStatus.InfrastructureFailure;
        return written ? 2 : 1;
    }

    private async Task<int> CompleteHelpAsync(
        CommandDefinition model,
        Guid entryTargetId,
        InvocationServices services,
        bool plain,
        TextRedactor redactor)
    {
        CommandPresentation.Report report = CommandPresentation.CreateHelp(
            model,
            entryTargetId,
            services.InvocationName);
        bool written = await TryWriteAsync(
            report,
            services.StandardOutput,
            !plain && services.StandardOutputSupportsAnsi,
            redactor).ConfigureAwait(false);
        LastInvocationStatus = written ? InvocationStatus.Help : InvocationStatus.InfrastructureFailure;
        return written ? 0 : 1;
    }

    private async Task<int> CompleteGraphFailureAsync(
        InvocationStart start,
        ImmutableArray<GraphDiagnostic> diagnostics)
    {
        CommandPresentation.Report report = CommandPresentation.CreateGraphFailure(diagnostics);
        bool written = await TryWriteAsync(
            report,
            start.Services.StandardError,
            !start.Plain && start.Services.StandardErrorSupportsAnsi,
            start.Redactor).ConfigureAwait(false);
        LastInvocationStatus = written ? InvocationStatus.GraphFailure : InvocationStatus.InfrastructureFailure;
        return written ? 2 : 1;
    }

    private async Task<int> CompleteCancellationAsync(
        InvocationServices services,
        bool plain,
        TextRedactor redactor)
    {
        bool written = await TryWriteAsync(
            CommandPresentation.CreateCancellation(),
            services.StandardError,
            !plain && services.StandardErrorSupportsAnsi,
            redactor).ConfigureAwait(false);
        LastInvocationStatus = written ? InvocationStatus.Cancelled : InvocationStatus.InfrastructureFailure;
        return written ? 130 : 1;
    }

    private async Task<int> CompleteInfrastructureFailureAsync(
        InvocationServices services,
        bool plain,
        TextRedactor redactor,
        string message)
    {
        _ = await TryWriteAsync(
            CommandPresentation.CreateInfrastructureFailure(message),
            services.StandardError,
            !plain && services.StandardErrorSupportsAnsi,
            redactor).ConfigureAwait(false);
        LastInvocationStatus = InvocationStatus.InfrastructureFailure;
        return 1;
    }

    private static async Task<bool> TryWriteAsync(
        CommandPresentation.Report report,
        TextWriter writer,
        bool rich,
        TextRedactor redactor)
    {
        try
        {
            return await CommandPresentation.WriteAsync(report, writer, rich, redactor).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    private AuthoredOption AddOption<T>(string name, bool isRequired, bool isRepeated)
    {
        ArgumentNullException.ThrowIfNull(name);
        long sequence = BeginMutation();
        bool isSnapshotSafeDefaultType = typeof(T) == typeof(string)
            || !RuntimeHelpers.IsReferenceOrContainsReferences<T>();
        AuthoredOption option = new(
            _authored,
            name,
            typeof(T),
            isRequired,
            isRepeated,
            isSnapshotSafeDefaultType,
            OptionBindingStrategy.Create<T>(),
            sequence);
        _authored.Options.Add(option);
        return option;
    }

    private Command SetCleanup(NormalizedCallback callback)
    {
        long sequence = BeginMutation();
        _authored.Set(_authored.Cleanup, callback, sequence, "RAFTER1005", "Command cleanup");
        return this;
    }

    private long BeginMutation()
    {
        _authored.EnsureMutable();
        return _authored.NextSequence();
    }

    private sealed record InvocationStart(
        CommandDefinition Model,
        Guid EntryTargetId,
        InvocationServices Services,
        bool Plain,
        TextRedactor Redactor,
        CancellationToken CallerToken);

    private sealed record InvocationExecution(
        CommandDefinition Model,
        GraphPlan Plan,
        InvocationServices Services,
        CancellationToken CancellationToken);

    internal enum InvocationStatus
    {
        InvalidModel,
        Help,
        GraphFailure,
        InputFailure,
        AuthorFailure,
        InfrastructureFailure,
        PathFailure,
        Success,
        ExecutionFailure,
        Cancelled,
    }
}
