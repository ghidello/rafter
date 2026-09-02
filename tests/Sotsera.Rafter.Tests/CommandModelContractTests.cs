using System.Collections.Immutable;
using System.Reflection;

namespace Sotsera.Rafter.Tests;

public sealed class CommandModelContractTests
{
    [Fact]
    public void PublicSurfaceEnforcesTheFrozenTypeState()
    {
        MethodInfo[] repeatedMethods = typeof(RepeatedOption<string>).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        MethodInfo[] targetMethods = typeof(Target).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        MethodInfo[] conditionMethods = targetMethods.Where(static method => string.Equals(method.Name, nameof(Target.When), StringComparison.Ordinal)).ToArray();

        repeatedMethods.Should().NotContain(method => string.Equals(method.Name, "Default", StringComparison.Ordinal)
            || string.Equals(method.Name, "FromEnvironment", StringComparison.Ordinal));
        targetMethods.Where(static method => string.Equals(method.Name, nameof(Target.WorkingDirectory), StringComparison.Ordinal)).Should().HaveCount(3);
        targetMethods.Should().NotContain(method => string.Equals(method.Name, nameof(Target.WorkingDirectory), StringComparison.Ordinal)
            && method.GetParameters().Single().ParameterType == typeof(Option<string>));
        conditionMethods.Should().HaveCount(4);
        conditionMethods.Should().NotContain(method => method.GetParameters().Single().ParameterType == typeof(Func<Task<bool>>));
        typeof(Option<int>).GetMethod("Default")!.ReturnType.Should().Be<DefaultedOption<int>>();
    }

    [Fact]
    public async Task ConditionsPreserveAuthoredOrderAndDeferredEvaluation()
    {
        int calls = 0;
        bool authored = Increment(ref calls);
        Command command = NewCommand();
        command.Target("entry")
            .Description("Entry.")
            .When(authored)
            .When(() => Increment(ref calls))
            .When(_ => Increment(ref calls))
            .When(_ => Task.FromResult(Increment(ref calls)));

        ImmutableArray<CommandModel.NormalizedCondition> conditions = command.FreezeResult.Model!.Targets.Single().Conditions;

        calls.Should().Be(1);
        foreach (CommandModel.NormalizedCondition condition in conditions)
        {
            (await condition.Evaluate(new RafterContext())).Should().BeTrue();
        }

        calls.Should().Be(4);
    }

    [Fact]
    public void RepeatedValidationHasTheImmutableListShapeAndRetainsItsMessage()
    {
        IReadOnlyList<string>? observed = null;
        Command command = NewCommand();
        command.RepeatedOption<string>("item")
            .Description("Item.")
            .Validate(values =>
            {
                observed = values;
                return values.Count > 0;
            }, "Supply at least one item.");
        command.Target("entry").Description("Entry.");
        CommandModel.ValidatorDefinition validator = command.FreezeResult.Model!.Options.Single().Validators.Single();
        ImmutableArray<string> empty = [];

        bool valid = ((Func<IReadOnlyList<string>, bool>)validator.Predicate)(empty);

        valid.Should().BeFalse();
        observed.Should().BeOfType<ImmutableArray<string>>();
        observed.Should().BeEmpty();
        validator.Message.Should().Be("Supply at least one item.");
    }

    [Fact]
    public void EveryFrozenDefinitionHasAStableIdentity()
    {
        Command command = NewCommand();
        command.Option<string>("value").Description("Value.").Validate(static _ => true, "Valid.");
        command.Target("entry").Description("Entry.").WorkingDirectory("work").When(true).Run(static () => { }).Finally(static () => { });

        CommandModel.CommandDefinition model = command.FreezeResult.Model!;

        model.Id.Should().NotBeEmpty();
        model.Root.Id.Should().NotBeEmpty();
        model.Options.Single().Id.Should().NotBeEmpty();
        model.Options.Single().Validators.Single().Id.Should().NotBeEmpty();
        model.Targets.Single().Id.Should().NotBeEmpty();
        model.Targets.Single().Conditions.Single().Id.Should().NotBeEmpty();
        model.Targets.Single().Execution!.Id.Should().NotBeEmpty();
        model.Targets.Single().Cleanup!.Id.Should().NotBeEmpty();
        model.Targets.Single().WorkingDirectory!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void ReportsEveryCaseInsensitiveEnumMemberCollision()
    {
        Command command = NewCommand();
        command.RequiredOption<AmbiguousValue>("value").Description("Value.");
        command.Target("entry").Description("Entry.");

        CommandModel.ModelDiagnostic diagnostic = command.FreezeResult.Diagnostics.Single(diagnostic => string.Equals(diagnostic.Code, "RAFTER1112", StringComparison.Ordinal));

        diagnostic.Message.Should().ContainAll("'One'", "'one'", "'Two'", "'two'");
    }

    [Fact]
    public void MultipleValidatorsAndConditionsKeepTheirAuthoredOrder()
    {
        Func<string, bool> firstValidator = static _ => true;
        Func<string, bool> secondValidator = static _ => false;
        Command command = NewCommand();
        command.Option<string>("value")
            .Description("Value.")
            .Validate(firstValidator, "First.")
            .Validate(secondValidator, "Second.");
        Target target = command.Target("entry").Description("Entry.").When(true).When(false);

        CommandModel.CommandDefinition model = command.FreezeResult.Model!;

        model.Options.Single().Validators.Select(static validator => validator.Predicate).Should().Equal(firstValidator, secondValidator);
        model.Targets.Single().Conditions.Select(static condition => condition.Id).Should().Equal(target.Authored.Conditions.Select(static condition => condition.Id));
    }

    [Fact]
    public void RejectsDefaultsThatWouldRetainCallerOwnedMutableState()
    {
        MutableParsable authoredDefault = new("first");
        Command defaultedCommand = NewCommand();
        defaultedCommand.Option<MutableParsable>("value").Description("Value.").Default(authoredDefault);
        defaultedCommand.Target("entry").Description("Entry.");
        Command requiredCommand = NewCommand();
        requiredCommand.RequiredOption<MutableParsable>("value").Description("Value.");
        requiredCommand.Target("entry").Description("Entry.");

        authoredDefault.Value = "changed";

        defaultedCommand.FreezeResult.Model.Should().BeNull();
        defaultedCommand.FreezeResult.Diagnostics.Select(static diagnostic => diagnostic.Code).Should().Equal("RAFTER1119");
        requiredCommand.FreezeResult.IsSuccess.Should().BeTrue();
    }

    private static bool Increment(ref int value)
    {
        value++;
        return true;
    }

    private static Command NewCommand()
        => global::Sotsera.Rafter.Rafter.Command(Root.Invocation).Description("Test command.");

    private enum AmbiguousValue
    {
        One,
        one,
        Two,
        two,
    }

    private sealed class MutableParsable(string value) : IParsable<MutableParsable>
    {
        internal string Value { get; set; } = value;

        public static MutableParsable Parse(string value, IFormatProvider? provider) => new(value);

        public static bool TryParse(string? value, IFormatProvider? provider, out MutableParsable result)
        {
            result = new MutableParsable(value ?? string.Empty);
            return value is not null;
        }
    }
}
