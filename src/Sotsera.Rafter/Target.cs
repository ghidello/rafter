using System.Collections.Immutable;
using static Sotsera.Rafter.CommandModel;

namespace Sotsera.Rafter;

/// <summary>Defines a node in a command's target graph.</summary>
public sealed class Target
{
    internal Target(AuthoredTarget authored)
    {
        Authored = authored;
    }

    internal AuthoredTarget Authored { get; }

    /// <summary>Sets the target description.</summary>
    public Target Description(string description)
    {
        ArgumentNullException.ThrowIfNull(description);
        long sequence = BeginMutation();
        Authored.Command.Set(Authored.Description, description, sequence, "RAFTER1210", "The target description");
        return this;
    }

    /// <summary>Declares the target's complete dependency set.</summary>
    public Target DependsOn(params Target[] dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        for (int index = 0; index < dependencies.Length; index++)
        {
            ArgumentNullException.ThrowIfNull(dependencies[index], $"{nameof(dependencies)}[{index}]");
        }

        ImmutableArray<AuthoredTarget> snapshot = dependencies.Select(static dependency => dependency.Authored).ToImmutableArray();
        long sequence = BeginMutation();
        Authored.Command.Set(Authored.Dependencies, snapshot, sequence, "RAFTER1211", "Target dependencies");
        return this;
    }

    /// <summary>Sets a literal target working directory.</summary>
    public Target WorkingDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return SetWorkingDirectory(new LiteralWorkingDirectory(path));
    }

    /// <summary>Sets the working directory from a required option.</summary>
    public Target WorkingDirectory(RequiredOption<string> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return SetWorkingDirectory(new OptionWorkingDirectory(option.Authored));
    }

    /// <summary>Sets the working directory from a defaulted option.</summary>
    public Target WorkingDirectory(DefaultedOption<string> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return SetWorkingDirectory(new OptionWorkingDirectory(option.Authored));
    }

    /// <summary>Registers context-free synchronous target work.</summary>
    public Target Run(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return SetExecution(new NormalizedCallback(_ =>
        {
            callback();
            return ValueTask.CompletedTask;
        }));
    }

    /// <summary>Registers context-free asynchronous target work.</summary>
    public Target Run(Func<Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return SetExecution(new NormalizedCallback(_ => new ValueTask(callback())));
    }

    /// <summary>Registers context-aware synchronous target work.</summary>
    public Target Run(Action<RafterContext> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return SetExecution(new NormalizedCallback(context =>
        {
            callback(context);
            return ValueTask.CompletedTask;
        }));
    }

    /// <summary>Registers context-aware asynchronous target work.</summary>
    public Target Run(Func<RafterContext, Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return SetExecution(new NormalizedCallback(context => new ValueTask(callback(context))));
    }

    /// <summary>Adds an authored Boolean condition.</summary>
    public Target When(bool condition)
        => AddCondition(new NormalizedCondition(_ => ValueTask.FromResult(condition)));

    /// <summary>Adds a deferred context-free condition.</summary>
    public Target When(Func<bool> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return AddCondition(new NormalizedCondition(_ => ValueTask.FromResult(condition())));
    }

    /// <summary>Adds a context-aware synchronous condition.</summary>
    public Target When(Func<RafterContext, bool> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return AddCondition(new NormalizedCondition(context => ValueTask.FromResult(condition(context))));
    }

    /// <summary>Adds a context-aware asynchronous condition.</summary>
    public Target When(Func<RafterContext, Task<bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return AddCondition(new NormalizedCondition(context => new ValueTask<bool>(condition(context))));
    }

    /// <summary>Registers context-free synchronous target cleanup.</summary>
    public Target Finally(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return SetCleanup(new NormalizedCallback(_ =>
        {
            callback();
            return ValueTask.CompletedTask;
        }));
    }

    /// <summary>Registers context-free asynchronous target cleanup.</summary>
    public Target Finally(Func<Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return SetCleanup(new NormalizedCallback(_ => new ValueTask(callback())));
    }

    /// <summary>Registers context-aware synchronous target cleanup.</summary>
    public Target Finally(Action<RafterContext> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return SetCleanup(new NormalizedCallback(context =>
        {
            callback(context);
            return ValueTask.CompletedTask;
        }));
    }

    /// <summary>Registers context-aware asynchronous target cleanup.</summary>
    public Target Finally(Func<RafterContext, Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return SetCleanup(new NormalizedCallback(context => new ValueTask(callback(context))));
    }

    private Target SetWorkingDirectory(WorkingDirectoryValue value)
    {
        long sequence = BeginMutation();
        Authored.Command.Set(Authored.WorkingDirectory, value, sequence, "RAFTER1212", "Target working directory");
        return this;
    }

    private Target SetExecution(NormalizedCallback callback)
    {
        long sequence = BeginMutation();
        Authored.Command.Set(Authored.Execution, callback, sequence, "RAFTER1213", "Target execution");
        return this;
    }

    private Target SetCleanup(NormalizedCallback callback)
    {
        long sequence = BeginMutation();
        Authored.Command.Set(Authored.Cleanup, callback, sequence, "RAFTER1214", "Target cleanup");
        return this;
    }

    private Target AddCondition(NormalizedCondition condition)
    {
        _ = BeginMutation();
        Authored.Conditions.Add(condition);
        return this;
    }

    private long BeginMutation()
    {
        Authored.Command.EnsureMutable();
        return Authored.Command.NextSequence();
    }
}
