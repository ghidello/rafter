using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;

namespace Sotsera.Rafter;

internal static class CommandModel
{
    internal enum RootKind
    {
        Invocation,
        Source,
        Explicit,
    }

    internal enum DiagnosticStage
    {
        InvalidValue = 0,
        ReservedOrCollision = 1,
        OwnershipOrReference = 2,
        Duplicate = 3,
        MissingMetadata = 4,
        UnsupportedType = 5,
        InvalidStructure = 6,
    }

    internal sealed record ModelDiagnostic(
        string Code,
        string Message,
        long Sequence,
        int? ArgumentPosition,
        DiagnosticStage Stage);

    internal sealed record ModelFreezeResult(
        CommandDefinition? Model,
        ImmutableArray<ModelDiagnostic> Diagnostics)
    {
        internal bool IsSuccess => Model is not null;
    }

    internal sealed record CommandDefinition(
        Guid Id,
        RootDefinition Root,
        string Description,
        int Concurrency,
        ImmutableArray<OptionDefinition> Options,
        ImmutableArray<TargetDefinition> Targets,
        NormalizedCallback? Cleanup);

    internal sealed record RootDefinition(Guid Id, RootKind Kind, string? Path);

    internal sealed record OptionDefinition(
        Guid Id,
        string Name,
        Type ValueType,
        bool IsRequired,
        bool IsRepeated,
        string Description,
        char? Alias,
        string? EnvironmentName,
        bool HasDefault,
        object? DefaultValue,
        bool IsSensitive,
        ImmutableArray<ValidatorDefinition> Validators);

    internal sealed record ValidatorDefinition(Guid Id, Delegate Predicate, string Message);

    internal sealed record TargetDefinition(
        Guid Id,
        string Name,
        string Description,
        ImmutableArray<Guid> Dependencies,
        ImmutableArray<NormalizedCondition> Conditions,
        NormalizedCallback? Execution,
        NormalizedCallback? Cleanup,
        WorkingDirectoryDefinition? WorkingDirectory);

    internal abstract record WorkingDirectoryDefinition(Guid Id);

    internal sealed record LiteralWorkingDirectoryDefinition(Guid Id, string Path) : WorkingDirectoryDefinition(Id);

    internal sealed record OptionWorkingDirectoryDefinition(Guid Id, Guid OptionId) : WorkingDirectoryDefinition(Id);

    internal sealed record NormalizedCallback(Func<RafterContext, ValueTask> Invoke)
    {
        internal Guid Id { get; } = Guid.NewGuid();
    }

    internal sealed record NormalizedCondition(Func<RafterContext, ValueTask<bool>> Evaluate)
    {
        internal Guid Id { get; } = Guid.NewGuid();
    }

    internal sealed class AuthoredCommand(Root root)
    {
        private long _sequence = 1;

        internal Guid Id { get; } = Guid.NewGuid();

        internal long DeclarationSequence { get; } = 1;

        internal Guid RootId { get; } = Guid.NewGuid();

        internal Root Root { get; } = root;

        internal SingleSetting<string> Description { get; } = new();

        internal SingleSetting<int> Concurrency { get; } = new();

        internal SingleSetting<NormalizedCallback> Cleanup { get; } = new();

        internal List<AuthoredOption> Options { get; } = [];

        internal List<AuthoredTarget> Targets { get; } = [];

        internal List<ModelDiagnostic> Diagnostics { get; } = [];

        internal bool IsFrozen { get; set; }

        internal ModelFreezeResult? FreezeResult { get; set; }

        internal long NextSequence() => ++_sequence;

        internal void EnsureMutable()
        {
            if (IsFrozen)
            {
                throw new InvalidOperationException("The command has already been frozen and can no longer be changed.");
            }
        }

        internal void Set<T>(SingleSetting<T> setting, T value, long sequence, string code, string settingName)
        {
            if (!setting.IsSet)
            {
                setting.Set(value, sequence);
                return;
            }

            Diagnostics.Add(new ModelDiagnostic(
                code,
                $"{settingName} may be specified only once.",
                sequence,
                null,
                DiagnosticStage.Duplicate));
        }
    }

    internal sealed class SingleSetting<T>
    {
        internal bool IsSet { get; private set; }

        internal T? Value { get; private set; }

        internal long Sequence { get; private set; }

        internal void Set(T value, long sequence)
        {
            IsSet = true;
            Value = value;
            Sequence = sequence;
        }
    }

    internal sealed class AuthoredOption(
        AuthoredCommand command,
        string name,
        Type valueType,
        bool isRequired,
        bool isRepeated,
        bool isSnapshotSafeDefaultType,
        long declarationSequence)
    {
        internal Guid Id { get; } = Guid.NewGuid();

        internal AuthoredCommand Command { get; } = command;

        internal string Name { get; } = name;

        internal Type ValueType { get; } = valueType;

        internal bool IsRequired { get; } = isRequired;

        internal bool IsRepeated { get; } = isRepeated;

        internal bool IsSnapshotSafeDefaultType { get; } = isSnapshotSafeDefaultType;

        internal long DeclarationSequence { get; } = declarationSequence;

        internal SingleSetting<string> Description { get; } = new();

        internal SingleSetting<char> Alias { get; } = new();

        internal SingleSetting<string> EnvironmentName { get; } = new();

        internal SingleSetting<object?> Default { get; } = new();

        internal SingleSetting<bool> Sensitive { get; } = new();

        internal List<AuthoredValidator> Validators { get; } = [];
    }

    internal sealed record AuthoredValidator(Delegate Predicate, string Message, long Sequence)
    {
        internal Guid Id { get; } = Guid.NewGuid();
    }

    internal sealed class AuthoredTarget(AuthoredCommand command, string name, long declarationSequence)
    {
        internal Guid Id { get; } = Guid.NewGuid();

        internal AuthoredCommand Command { get; } = command;

        internal string Name { get; } = name;

        internal long DeclarationSequence { get; } = declarationSequence;

        internal SingleSetting<string> Description { get; } = new();

        internal SingleSetting<ImmutableArray<AuthoredTarget>> Dependencies { get; } = new();

        internal SingleSetting<NormalizedCallback> Execution { get; } = new();

        internal SingleSetting<NormalizedCallback> Cleanup { get; } = new();

        internal SingleSetting<WorkingDirectoryValue> WorkingDirectory { get; } = new();

        internal List<NormalizedCondition> Conditions { get; } = [];
    }

    internal abstract record WorkingDirectoryValue
    {
        internal Guid Id { get; } = Guid.NewGuid();
    }

    internal sealed record LiteralWorkingDirectory(string Path) : WorkingDirectoryValue;

    internal sealed record OptionWorkingDirectory(AuthoredOption Option) : WorkingDirectoryValue;

    internal static class ModelValidation
    {
        private static readonly StringComparer Ordinal = StringComparer.Ordinal;

        internal static ModelFreezeResult Freeze(AuthoredCommand command)
        {
            if (command.FreezeResult is not null)
            {
                return command.FreezeResult;
            }

            command.IsFrozen = true;
            List<ModelDiagnostic> diagnostics = [.. command.Diagnostics];

            ValidateDescription(command.Description, command.DeclarationSequence, "RAFTER1001", "The command", diagnostics);
            if (command.Concurrency.IsSet && command.Concurrency.Value <= 0)
            {
                Add(diagnostics, "RAFTER1002", "Concurrency must be greater than zero.", command.Concurrency.Sequence, null, DiagnosticStage.InvalidValue);
            }

            ValidateOptions(command, diagnostics);
            ValidateTargets(command, diagnostics);

            ImmutableArray<ModelDiagnostic> ordered = diagnostics
                .OrderBy(static diagnostic => diagnostic.Sequence)
                .ThenBy(static diagnostic => diagnostic.ArgumentPosition ?? int.MaxValue)
                .ThenBy(static diagnostic => diagnostic.Stage)
                .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ToImmutableArray();

            CommandDefinition? definition = ordered.IsEmpty ? CreateDefinition(command) : null;
            command.FreezeResult = new ModelFreezeResult(definition, ordered);
            return command.FreezeResult;
        }

        private static void ValidateOptions(AuthoredCommand command, List<ModelDiagnostic> diagnostics)
        {
            Dictionary<string, AuthoredOption> names = new(Ordinal);
            Dictionary<char, AuthoredOption> aliases = [];

            foreach (AuthoredOption option in command.Options)
            {
                ValidateOption(option, names, aliases, diagnostics);
            }

            foreach (AuthoredOption option in command.Options)
            {
                if (option.Name.Length == 1
                    && aliases.TryGetValue(option.Name[0], out AuthoredOption? aliasOwner)
                    && aliasOwner.Id != option.Id)
                {
                    long sequence = Math.Max(option.DeclarationSequence, aliasOwner.Alias.Sequence);
                    Add(diagnostics, "RAFTER1110", "A one-character option name collides with an option alias.", sequence, null, DiagnosticStage.ReservedOrCollision);
                }
            }
        }

        private static void ValidateOption(
            AuthoredOption option,
            Dictionary<string, AuthoredOption> names,
            Dictionary<char, AuthoredOption> aliases,
            List<ModelDiagnostic> diagnostics)
        {
            if (!IsKebabCase(option.Name))
            {
                Add(diagnostics, "RAFTER1101", "Option names must use lowercase ASCII kebab-case.", option.DeclarationSequence, null, DiagnosticStage.InvalidValue);
            }

            if (option.Name is "plain" or "help")
            {
                Add(diagnostics, "RAFTER1102", "The option name is reserved by Rafter.", option.DeclarationSequence, null, DiagnosticStage.ReservedOrCollision);
            }

            if (!names.TryAdd(option.Name, option))
            {
                Add(diagnostics, "RAFTER1103", "An option with this name has already been declared.", option.DeclarationSequence, null, DiagnosticStage.ReservedOrCollision);
            }

            ValidateDescription(option.Description, option.DeclarationSequence, "RAFTER1104", "The option", diagnostics);
            ValidateOptionType(option, diagnostics);

            if (!option.IsRequired && !option.IsRepeated && !option.Default.IsSet && IsNonNullableValueType(option.ValueType))
            {
                Add(diagnostics, "RAFTER1105", "An optional non-nullable value type must be nullable, required, or defaulted.", option.DeclarationSequence, null, DiagnosticStage.InvalidStructure);
            }

            if (option.Default.IsSet && !option.IsSnapshotSafeDefaultType)
            {
                Add(diagnostics, "RAFTER1119", "The authored default type cannot be copied safely into the frozen command model.", option.Default.Sequence, null, DiagnosticStage.InvalidStructure);
            }

            ValidateAlias(option, aliases, diagnostics);
            if (option.EnvironmentName.IsSet && !IsValidEnvironmentName(option.EnvironmentName.Value!))
            {
                Add(diagnostics, "RAFTER1109", "Environment names must contain non-whitespace text and cannot contain NUL or '='.", option.EnvironmentName.Sequence, null, DiagnosticStage.InvalidValue);
            }
        }

        private static void ValidateAlias(AuthoredOption option, Dictionary<char, AuthoredOption> aliases, List<ModelDiagnostic> diagnostics)
        {
            if (!option.Alias.IsSet)
            {
                return;
            }

            char alias = option.Alias.Value;
            if (alias is < 'a' or > 'z')
            {
                Add(diagnostics, "RAFTER1106", "Option aliases must be lowercase ASCII letters.", option.Alias.Sequence, null, DiagnosticStage.InvalidValue);
            }

            if (alias == 'h')
            {
                Add(diagnostics, "RAFTER1107", "The option alias is reserved by Rafter.", option.Alias.Sequence, null, DiagnosticStage.ReservedOrCollision);
            }

            if (!aliases.TryAdd(alias, option))
            {
                Add(diagnostics, "RAFTER1108", "An option with this alias has already been declared.", option.Alias.Sequence, null, DiagnosticStage.ReservedOrCollision);
            }
        }

        private static void ValidateTargets(AuthoredCommand command, List<ModelDiagnostic> diagnostics)
        {
            Dictionary<string, AuthoredTarget> names = new(Ordinal);
            foreach (AuthoredTarget target in command.Targets)
            {
                if (!IsKebabCase(target.Name))
                {
                    Add(diagnostics, "RAFTER1201", "Target names must use lowercase ASCII kebab-case.", target.DeclarationSequence, null, DiagnosticStage.InvalidValue);
                }

                if (!names.TryAdd(target.Name, target))
                {
                    Add(diagnostics, "RAFTER1202", "A target with this name has already been declared.", target.DeclarationSequence, null, DiagnosticStage.ReservedOrCollision);
                }

                ValidateDescription(target.Description, target.DeclarationSequence, "RAFTER1203", "The target", diagnostics);

                if (target.Dependencies.IsSet)
                {
                    HashSet<Guid> dependencyIds = [];
                    ImmutableArray<AuthoredTarget> dependencies = target.Dependencies.Value;
                    for (int index = 0; index < dependencies.Length; index++)
                    {
                        AuthoredTarget dependency = dependencies[index];
                        if (dependency.Command.Id != command.Id)
                        {
                            Add(diagnostics, "RAFTER1204", "Dependencies must belong to the same command.", target.Dependencies.Sequence, index, DiagnosticStage.OwnershipOrReference);
                        }

                        if (dependency.Id == target.Id)
                        {
                            Add(diagnostics, "RAFTER1205", "A target cannot depend on itself.", target.Dependencies.Sequence, index, DiagnosticStage.OwnershipOrReference);
                        }

                        if (!dependencyIds.Add(dependency.Id))
                        {
                            Add(diagnostics, "RAFTER1206", "A dependency may be declared only once.", target.Dependencies.Sequence, index, DiagnosticStage.Duplicate);
                        }
                    }
                }

                if (target.WorkingDirectory.IsSet && target.WorkingDirectory.Value is OptionWorkingDirectory optionDirectory && optionDirectory.Option.Command.Id != command.Id)
                {
                    Add(diagnostics, "RAFTER1207", "The working-directory option must belong to the same command.", target.WorkingDirectory.Sequence, null, DiagnosticStage.OwnershipOrReference);
                }

                if (target.Cleanup.IsSet && !target.Execution.IsSet)
                {
                    Add(diagnostics, "RAFTER1208", "Target cleanup requires an execution callback.", target.Cleanup.Sequence, null, DiagnosticStage.InvalidStructure);
                }
            }
        }

        private static void ValidateDescription(SingleSetting<string> setting, long declarationSequence, string code, string owner, List<ModelDiagnostic> diagnostics)
        {
            if (!setting.IsSet)
            {
                Add(diagnostics, code, $"{owner} requires a description.", declarationSequence, null, DiagnosticStage.MissingMetadata);
            }
            else if (string.IsNullOrWhiteSpace(setting.Value))
            {
                Add(diagnostics, code, $"{owner} description cannot be empty or whitespace.", setting.Sequence, null, DiagnosticStage.InvalidValue);
            }
        }

        private static void ValidateOptionType(AuthoredOption option, List<ModelDiagnostic> diagnostics)
        {
            Type type = Nullable.GetUnderlyingType(option.ValueType) ?? option.ValueType;
            bool supported = type == typeof(string)
                || type == typeof(bool)
                || type.IsEnum
                || type.GetInterfaces().Any(interfaceType =>
                    interfaceType.IsGenericType
                    && interfaceType.GetGenericTypeDefinition() == typeof(IParsable<>)
                    && interfaceType.GenericTypeArguments[0] == type);

            if (!supported)
            {
                Add(diagnostics, "RAFTER1111", "The option value type is not supported.", option.DeclarationSequence, null, DiagnosticStage.UnsupportedType);
                return;
            }

            if (type.IsEnum)
            {
                string[] collisions = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Select(static field => field.Name)
                    .GroupBy(static name => name, StringComparer.OrdinalIgnoreCase)
                    .Where(static group => group.Count() > 1)
                    .Select(static group => string.Join(", ", group.Select(static name => $"'{name}'")))
                    .ToArray();
                if (collisions.Length > 0)
                {
                    Add(diagnostics, "RAFTER1112", $"The enum contains member names that differ only by case: {string.Join("; ", collisions)}.", option.DeclarationSequence, null, DiagnosticStage.UnsupportedType);
                }
            }
        }

        private static CommandDefinition CreateDefinition(AuthoredCommand command)
        {
            ImmutableArray<OptionDefinition> options = command.Options.Select(static option => new OptionDefinition(
                option.Id,
                option.Name,
                option.ValueType,
                option.IsRequired,
                option.IsRepeated,
                option.Description.Value!,
                option.Alias.IsSet ? option.Alias.Value : null,
                option.EnvironmentName.IsSet ? option.EnvironmentName.Value : null,
                option.Default.IsSet,
                option.Default.Value,
                option.Sensitive.IsSet && option.Sensitive.Value,
                option.Validators.Select(static validator => new ValidatorDefinition(validator.Id, validator.Predicate, validator.Message)).ToImmutableArray())).ToImmutableArray();

            ImmutableArray<TargetDefinition> targets = command.Targets.Select(static target => new TargetDefinition(
                target.Id,
                target.Name,
                target.Description.Value!,
                target.Dependencies.IsSet ? target.Dependencies.Value.Select(static dependency => dependency.Id).ToImmutableArray() : [],
                [.. target.Conditions],
                target.Execution.IsSet ? target.Execution.Value : null,
                target.Cleanup.IsSet ? target.Cleanup.Value : null,
                CreateWorkingDirectory(target.WorkingDirectory))).ToImmutableArray();

            return new CommandDefinition(
                command.Id,
                new RootDefinition(command.RootId, command.Root.Kind, command.Root.Path),
                command.Description.Value!,
                command.Concurrency.IsSet ? command.Concurrency.Value : 1,
                options,
                targets,
                command.Cleanup.IsSet ? command.Cleanup.Value : null);
        }

        private static WorkingDirectoryDefinition? CreateWorkingDirectory(SingleSetting<WorkingDirectoryValue> setting)
        {
            if (!setting.IsSet)
            {
                return null;
            }

            return setting.Value switch
            {
                LiteralWorkingDirectory literal => new LiteralWorkingDirectoryDefinition(literal.Id, literal.Path),
                OptionWorkingDirectory option => new OptionWorkingDirectoryDefinition(option.Id, option.Option.Id),
                _ => throw new UnreachableException(),
            };
        }

        private static bool IsNonNullableValueType(Type type) => type.IsValueType && Nullable.GetUnderlyingType(type) is null;

        private static bool IsValidEnvironmentName(string name) => !string.IsNullOrWhiteSpace(name) && !name.Contains('\0', StringComparison.Ordinal) && !name.Contains('=', StringComparison.Ordinal);

        private static bool IsKebabCase(string name)
        {
            if (name.Length == 0 || name[0] is < 'a' or > 'z' || name[^1] == '-')
            {
                return false;
            }

            bool previousHyphen = false;
            foreach (char character in name)
            {
                if (character == '-')
                {
                    if (previousHyphen)
                    {
                        return false;
                    }

                    previousHyphen = true;
                    continue;
                }

                if (character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9'))
                {
                    return false;
                }

                previousHyphen = false;
            }

            return true;
        }

        private static void Add(List<ModelDiagnostic> diagnostics, string code, string message, long sequence, int? argumentPosition, DiagnosticStage stage)
            => diagnostics.Add(new ModelDiagnostic(code, message, sequence, argumentPosition, stage));
    }
}
