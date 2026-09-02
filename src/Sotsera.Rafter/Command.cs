using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static Sotsera.Rafter.BindingEngine;
using static Sotsera.Rafter.CommandModel;
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

    internal PathRuntime.InvocationPaths? LastInvocationPaths { get; private set; }

    internal InvocationStatus? LastInvocationStatus { get; private set; }

    internal Func<InvocationServices> InvocationServicesFactory { get; set; } = InvocationServices.Capture;

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
    public Task<int> RunAsync(Target entryTarget, string[] args)
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
            LastInvocationPaths = null;
            LastInvocationStatus = null;
            _lastInvocationDiagnostics = entryTarget.Authored.Command.Id == _authored.Id
                ? []
                : [new ModelDiagnostic("RAFTER1301", "The entry target must belong to the invoked command.", 0, null, DiagnosticStage.OwnershipOrReference)];
            ModelFreezeResult freezeResult = ModelValidation.Freeze(_authored);
            return CompleteInvocationAsync(entryTarget.Authored.Id, freezeResult);
        }
        catch
        {
            Volatile.Write(ref _invocationActive, 0);
            throw;
        }
    }

    private async Task<int> CompleteInvocationAsync(Guid entryTargetId, ModelFreezeResult freezeResult)
    {
        try
        {
            if (InvocationBarrier is not null)
            {
                await InvocationBarrier.ConfigureAwait(false);
            }

            return await ExecuteInvocationAsync(entryTargetId, freezeResult).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _invocationActive, 0);
        }
    }

    private async Task<int> ExecuteInvocationAsync(Guid entryTargetId, ModelFreezeResult freezeResult)
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

        ImmutableArray<ModelDiagnostic> modelDiagnostics = [.. freezeResult.Diagnostics, .. _lastInvocationDiagnostics];
        TextRedactor defaultRedactor = ModelValidation.CreateSensitiveDefaultRedactor(_authored);
        if (!modelDiagnostics.IsEmpty)
        {
            CommandPresentation.Report report = CommandPresentation.CreateModelFailure(modelDiagnostics);
            bool written = await TryWriteAsync(
                report,
                services.StandardError,
                services.StandardErrorSupportsAnsi,
                defaultRedactor).ConfigureAwait(false);
            LastInvocationStatus = written ? InvocationStatus.InvalidModel : InvocationStatus.InfrastructureFailure;
            return written ? 2 : 1;
        }

        CommandDefinition model = freezeResult.Model!;
        bool help = _lastArguments.Any(static argument => argument is "--help" or "-h");
        bool plain = _lastArguments.Any(static argument => string.Equals(argument, "--plain", StringComparison.Ordinal));
        if (help)
        {
            CommandPresentation.Report report = CommandPresentation.CreateHelp(model, entryTargetId, services.InvocationName);
            bool written = await TryWriteAsync(
                report,
                services.StandardOutput,
                !plain && services.StandardOutputSupportsAnsi,
                defaultRedactor).ConfigureAwait(false);
            LastInvocationStatus = written ? InvocationStatus.Help : InvocationStatus.InfrastructureFailure;
            return written ? 0 : 1;
        }

        LastBindingResult = BindingEngine.Bind(model, _lastArguments, services);
        return await CompleteBindingAsync(model, entryTargetId, services, LastBindingResult).ConfigureAwait(false);
    }

    private async Task<int> CompleteBindingAsync(
        CommandDefinition model,
        Guid entryTargetId,
        InvocationServices services,
        BindingResult result)
    {
        switch (result.Status)
        {
            case BindingStatus.InputFailure:
                {
                    CommandPresentation.Report report = CommandPresentation.CreateInputFailure(
                        model,
                        entryTargetId,
                        services.InvocationName,
                        result.Diagnostics);
                    bool written = await TryWriteAsync(
                        report,
                        services.StandardError,
                        !result.Plain && services.StandardErrorSupportsAnsi,
                        result.Redactor).ConfigureAwait(false);
                    LastInvocationStatus = written ? InvocationStatus.InputFailure : InvocationStatus.InfrastructureFailure;
                    return written ? 2 : 1;
                }
            case BindingStatus.AuthorFailure:
            case BindingStatus.InfrastructureFailure:
                {
                    CommandPresentation.Report report = CommandPresentation.CreateBindingFailure(result);
                    bool written = await TryWriteAsync(
                        report,
                        services.StandardError,
                        !result.Plain && services.StandardErrorSupportsAnsi,
                        result.Redactor).ConfigureAwait(false);
                    LastInvocationStatus = !written || result.Status == BindingStatus.InfrastructureFailure
                        ? InvocationStatus.InfrastructureFailure
                        : InvocationStatus.AuthorFailure;
                    return 1;
                }
            case BindingStatus.Success:
                return await CompletePathInitializationAsync(model, services, result).ConfigureAwait(false);
            default:
                throw new UnreachableException();
        }
    }

    private async Task<int> CompletePathInitializationAsync(
        CommandDefinition model,
        InvocationServices services,
        BindingResult result)
    {
        try
        {
            LastInvocationPaths = PathRuntime.Resolve(model, result.Snapshot!, services);
        }
        catch (PathPolicyException exception)
        {
            CommandPresentation.Report report = CommandPresentation.CreatePathFailure(exception.Message);
            bool written = await TryWriteAsync(
                report,
                services.StandardError,
                !result.Plain && services.StandardErrorSupportsAnsi,
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
                services.StandardError,
                !result.Plain && services.StandardErrorSupportsAnsi,
                result.Redactor).ConfigureAwait(false);
            LastInvocationStatus = InvocationStatus.InfrastructureFailure;
            return 1;
        }

        LastInvocationStatus = InvocationStatus.SuccessfulPathStub;
        await Task.Yield();
        throw new NotSupportedException("Command execution is implemented in a later Rafter phase.");
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
        bool isSnapshotSafeDefaultType = typeof(T) == typeof(string) || !RuntimeHelpers.IsReferenceOrContainsReferences<T>();
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

    internal enum InvocationStatus
    {
        InvalidModel,
        Help,
        InputFailure,
        AuthorFailure,
        InfrastructureFailure,
        PathFailure,
        SuccessfulPathStub,
    }
}
