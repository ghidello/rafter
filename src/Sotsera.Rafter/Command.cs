using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using static Sotsera.Rafter.CommandModel;

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

    internal Task? PhaseTwoInvocationBarrier { get; set; }

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
            _lastInvocationDiagnostics = entryTarget.Authored.Command.Id == _authored.Id
                ? []
                : [new ModelDiagnostic("RAFTER1301", "The entry target must belong to the invoked command.", 0, null, DiagnosticStage.OwnershipOrReference)];
            _ = ModelValidation.Freeze(_authored);
            return CompletePhaseTwoInvocationAsync();
        }
        catch
        {
            Volatile.Write(ref _invocationActive, 0);
            throw;
        }
    }

    private async Task<int> CompletePhaseTwoInvocationAsync()
    {
        try
        {
            if (PhaseTwoInvocationBarrier is not null)
            {
                await PhaseTwoInvocationBarrier.ConfigureAwait(false);
            }

            await Task.Yield();
            throw new NotSupportedException("Command execution is implemented in a later Rafter phase.");
        }
        finally
        {
            Volatile.Write(ref _invocationActive, 0);
        }
    }

    private AuthoredOption AddOption<T>(string name, bool isRequired, bool isRepeated)
    {
        ArgumentNullException.ThrowIfNull(name);
        long sequence = BeginMutation();
        bool isSnapshotSafeDefaultType = typeof(T) == typeof(string) || !RuntimeHelpers.IsReferenceOrContainsReferences<T>();
        AuthoredOption option = new(_authored, name, typeof(T), isRequired, isRepeated, isSnapshotSafeDefaultType, sequence);
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
}
