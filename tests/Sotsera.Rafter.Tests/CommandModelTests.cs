using System.Collections.Immutable;

namespace Sotsera.Rafter.Tests;

public sealed class CommandModelTests
{
    [Fact]
    public void FreezesACompleteCommandIntoImmutableDefinitions()
    {
        Command command = NewCommand().Concurrency(2);
        RequiredOption<string> required = command.RequiredOption<string>("required")
            .Description("Required value.")
            .Alias('r')
            .FromEnvironment("RAFTER_REQUIRED")
            .Sensitive()
            .Validate(static value => value.Length > 0, "Value is required.");
        DefaultedOption<int> count = command.Option<int>("count")
            .Description("Number of attempts.")
            .Default(2)
            .Validate(static value => value > 0, "Count must be positive.");
        RepeatedOption<string> items = command.RepeatedOption<string>("item")
            .Description("Items to include.")
            .Validate(static values => values.Count < 10, "Too many items.");
        Target prepare = command.Target("prepare").Description("Prepare inputs.").Run(static () => { });
        Target execute = command.Target("execute")
            .Description("Execute work.")
            .DependsOn(prepare)
            .WorkingDirectory(required)
            .When(true)
            .When(static () => true)
            .When(static _ => true)
            .When(static _ => Task.FromResult(true))
            .Run(static _ => { })
            .Finally(static () => { });
        command.Finally(static _ => { });

        CommandModel.ModelFreezeResult result = command.FreezeResult;

        result.IsSuccess.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
        result.Model!.Concurrency.Should().Be(2);
        result.Model.Options.Should().HaveCount(3);
        result.Model.Options[0].Validators.Should().ContainSingle()
            .Which.Message.Should().Be("Value is required.");
        result.Model.Options[1].Id.Should().Be(count.Authored.Id);
        result.Model.Options[2].Id.Should().Be(items.Authored.Id);
        result.Model.Targets.Should().HaveCount(2);
        result.Model.Targets[1].Dependencies.Should().Equal(prepare.Authored.Id);
        result.Model.Targets[1].Conditions.Should().HaveCount(4);
        result.Model.Targets[1].Execution.Should().NotBeNull();
        result.Model.Targets[1].Cleanup.Should().NotBeNull();
        result.Model.Cleanup.Should().NotBeNull();
        execute.Authored.Id.Should().Be(result.Model.Targets[1].Id);
    }

    [Fact]
    public void MetadataMethodsPreserveOptionTypeState()
    {
        Command command = NewCommand();

        Option<string> optional = command.Option<string>("optional").Description("Optional.").Alias('o').FromEnvironment("OPTIONAL").Sensitive().Validate(static _ => true, "Valid.");
        RequiredOption<string> required = command.RequiredOption<string>("required").Description("Required.").Alias('r').FromEnvironment("REQUIRED").Sensitive().Validate(static _ => true, "Valid.");
        DefaultedOption<string> defaulted = command.Option<string>("defaulted").Default("value").Description("Defaulted.").Alias('d').FromEnvironment("DEFAULTED").Sensitive().Validate(static _ => true, "Valid.");
        RepeatedOption<string> repeated = command.RepeatedOption<string>("repeated").Description("Repeated.").Alias('e').Sensitive().Validate(static _ => true, "Valid.");
        command.Target("entry").Description("Entry.");

        optional.Should().BeOfType<Option<string>>();
        required.Should().BeOfType<RequiredOption<string>>();
        defaulted.Should().BeOfType<DefaultedOption<string>>();
        repeated.Should().BeOfType<RepeatedOption<string>>();
        command.FreezeResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void IgnoredDefaultReturnStillUpdatesTheSharedOption()
    {
        Command command = NewCommand();
        Option<int> option = command.Option<int>("count").Description("Count.");
        _ = option.Default(3);
        Target entry = command.Target("entry").Description("Entry.");

        CommandModel.ModelFreezeResult result = command.FreezeResult;

        result.IsSuccess.Should().BeTrue();
        result.Model!.Options.Single().HasDefault.Should().BeTrue();
        result.Model.Options.Single().DefaultValue.Should().Be(3);
        entry.Authored.Command.Id.Should().Be(option.Authored.Command.Id);
    }

    [Fact]
    public async Task DependenciesAndInvocationArgumentsAreSnapshotted()
    {
        Command command = NewCommand();
        Target first = command.Target("first").Description("First.");
        Target replacement = command.Target("replacement").Description("Replacement.");
        Target entry = command.Target("entry").Description("Entry.");
        Target[] dependencies = [first];
        entry.DependsOn(dependencies);
        dependencies[0] = replacement;
        string[] arguments = ["--value", "first"];

        Task<int> invocation = command.RunAsync(entry, arguments);
        arguments[1] = "changed";

        command.FreezeResult.Model!.Targets.Single(target => target.Id == entry.Authored.Id).Dependencies.Should().Equal(first.Authored.Id);
        command.LastArguments.Should().Equal("--value", "first");
        await FluentActions.Awaiting(() => invocation).Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task RejectsOverlappingInvocationsAndAllowsSequentialReuse()
    {
        Command command = NewCommand();
        Target entry = command.Target("entry").Description("Entry.");
        TaskCompletionSource barrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        command.PhaseTwoInvocationBarrier = barrier.Task;

        Task<int> first = command.RunAsync(entry, []);
        Action overlap = () => command.RunAsync(entry, []);

        overlap.Should().Throw<InvalidOperationException>();
        barrier.SetResult();
        await FluentActions.Awaiting(() => first).Should().ThrowAsync<NotSupportedException>();
        command.PhaseTwoInvocationBarrier = null;
        await FluentActions.Awaiting(() => command.RunAsync(entry, ["--second"])).Should().ThrowAsync<NotSupportedException>();
        command.LastArguments.Should().Equal("--second");
    }

    [Fact]
    public async Task ForeignEntryIsInvocationSpecificAndDoesNotPoisonTheFrozenModel()
    {
        Command command = NewCommand();
        Target owned = command.Target("owned").Description("Owned.");
        Command other = NewCommand();
        Target foreign = other.Target("foreign").Description("Foreign.");

        await FluentActions.Awaiting(() => command.RunAsync(foreign, [])).Should().ThrowAsync<NotSupportedException>();

        command.FreezeResult.IsSuccess.Should().BeTrue();
        command.FreezeResult.Diagnostics.Should().BeEmpty();
        command.LastInvocationDiagnostics.Select(static diagnostic => diagnostic.Code).Should().Equal("RAFTER1301");
        await FluentActions.Awaiting(() => command.RunAsync(owned, [])).Should().ThrowAsync<NotSupportedException>();
        command.LastInvocationDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void FreezeIsCachedAndClosesEveryBuilderAfterFailure()
    {
        Command command = global::Sotsera.Rafter.Rafter.Command(Root.Invocation);
        Target target = command.Target("entry");

        CommandModel.ModelFreezeResult first = command.FreezeResult;
        CommandModel.ModelFreezeResult second = command.FreezeResult;
        Action mutateCommand = () => command.Description("Too late.");
        Action mutateTarget = () => target.Description("Too late.");

        first.Should().BeSameAs(second);
        first.IsSuccess.Should().BeFalse();
        mutateCommand.Should().Throw<InvalidOperationException>();
        mutateTarget.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void NullProgrammingErrorsDoNotPartiallyMutateBuilders()
    {
        Command command = NewCommand();
        Target dependency = command.Target("dependency").Description("Dependency.");
        Target entry = command.Target("entry").Description("Entry.");
        Target[] dependencies = [dependency, null!];

        Action invalidDependencies = () => entry.DependsOn(dependencies);
        Action invalidDescription = () => command.Description(null!);

        invalidDependencies.Should().Throw<ArgumentNullException>();
        invalidDescription.Should().Throw<ArgumentNullException>();
        command.FreezeResult.IsSuccess.Should().BeTrue();
        command.FreezeResult.Model!.Targets.Single(target => target.Id == entry.Authored.Id).Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void ReportsStructuralErrorsInDocumentedOrder()
    {
        Command command = global::Sotsera.Rafter.Rafter.Command(Root.Invocation);
        command.Concurrency(0).Concurrency(2);
        command.Option<int>("Bad")
            .Description(" ")
            .Alias('H')
            .FromEnvironment("BAD=NAME");
        command.Option<object>("Bad").Description("Unsupported.");
        Target entry = command.Target("Bad").Description(" ");
        entry.DependsOn(entry, entry).DependsOn();
        entry.Finally(static () => { });

        string[] codes = command.FreezeResult.Diagnostics.Select(static diagnostic => diagnostic.Code).ToArray();

        codes.Should().Equal(
            "RAFTER1001",
            "RAFTER1002",
            "RAFTER1004",
            "RAFTER1101",
            "RAFTER1105",
            "RAFTER1104",
            "RAFTER1106",
            "RAFTER1109",
            "RAFTER1101",
            "RAFTER1103",
            "RAFTER1111",
            "RAFTER1201",
            "RAFTER1203",
            "RAFTER1205",
            "RAFTER1205",
            "RAFTER1206",
            "RAFTER1211",
            "RAFTER1208");
    }

    [Fact]
    public void PreservesFirstSingleValuedSettingsAndExplicitOwnershipDiagnostics()
    {
        Command first = NewCommand();
        Command second = NewCommand();
        DefaultedOption<string> foreignDirectory = second.Option<string>("directory").Description("Directory.").Default("work");
        Target foreignDependency = second.Target("dependency").Description("Dependency.");
        Target entry = first.Target("entry")
            .Description("First.")
            .Description("Second.")
            .WorkingDirectory(foreignDirectory)
            .DependsOn(foreignDependency);

        CommandModel.ModelFreezeResult result = first.FreezeResult;

        result.Diagnostics.Select(static diagnostic => diagnostic.Code).Should().Equal("RAFTER1210", "RAFTER1207", "RAFTER1204");
        entry.Authored.Description.Value.Should().Be("First.");
    }

    [Fact]
    public async Task NormalizedCallbacksPreserveOriginalExceptionIdentity()
    {
        InvalidDataException expected = new("Expected.");
        Command command = NewCommand();
        command.Target("entry").Description("Entry.").Run(() => throw expected);

        CommandModel.NormalizedCallback callback = command.FreezeResult.Model!.Targets.Single().Execution!;
        var assertion = await FluentActions.Awaiting(() => callback.Invoke(new RafterContext()).AsTask()).Should().ThrowAsync<InvalidDataException>();

        assertion.Which.Should().BeSameAs(expected);
    }

    private static Command NewCommand()
        => global::Sotsera.Rafter.Rafter.Command(Root.Invocation).Description("Test command.");
}
