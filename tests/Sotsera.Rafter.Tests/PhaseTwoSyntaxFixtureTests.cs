namespace Sotsera.Rafter.Tests;

public sealed class PhaseTwoSyntaxFixtureTests
{
    [Fact]
    public void EveryPhaseTwoCallbackAndTargetShapeCompilesAgainstThePublicApi()
    {
        Command command = NewCommand(Root.Source).Concurrency(2);
        RequiredOption<string> requiredDirectory = command.RequiredOption<string>("required-directory").Description("Required directory.");
        DefaultedOption<string> defaultedDirectory = command.Option<string>("defaulted-directory").Description("Defaulted directory.").Default("work");

        Target synchronous = command.Target("synchronous").Description("Synchronous.").WorkingDirectory("work").Run(static () => { });
        Target asynchronous = command.Target("asynchronous").Description("Asynchronous.").WorkingDirectory(requiredDirectory).Run(static async () => await Task.Yield());
        Target contextual = command.Target("contextual").Description("Contextual.").WorkingDirectory(defaultedDirectory).Run(static _ => { });
        Target contextualAsync = command.Target("contextual-async").Description("Contextual async.").Run(static async _ => await Task.Yield());
        Target taskExpression = command.Target("task-expression").Description("Task expression.").Run(static _ => Task.CompletedTask);
        Target contextFreeTaskExpression = command.Target("context-free-task-expression").Description("Context-free task expression.").Run(static () => Task.CompletedTask);
        Target aggregate = command.Target("aggregate").Description("Aggregate.").DependsOn(synchronous, asynchronous, contextual, contextualAsync, taskExpression, contextFreeTaskExpression);
        Target noWork = command.Target("no-work").Description("No work.");

        command.FreezeResult.IsSuccess.Should().BeTrue();
        command.FreezeResult.Model!.Targets.Should().HaveCount(8);
        aggregate.Authored.Dependencies.Value.Should().HaveCount(6);
        noWork.Authored.Execution.IsSet.Should().BeFalse();
    }

    [Fact]
    public void EveryCleanupCallbackFormCompilesAgainstThePublicApi()
    {
        Command synchronous = NewCommand(Root.Invocation).Finally(static () => { });
        Command asynchronous = NewCommand(Root.Invocation).Finally(static async () => await Task.Yield());
        Command contextual = NewCommand(Root.Invocation).Finally(static _ => { });
        Command contextualAsync = NewCommand(Root.At("work")).Finally(static async _ => await Task.Yield());
        Command taskExpression = NewCommand(Root.Invocation).Finally(static _ => Task.CompletedTask);

        AddEntry(synchronous).Finally(static () => { });
        AddEntry(asynchronous).Finally(static async () => await Task.Yield());
        AddEntry(contextual).Finally(static _ => { });
        AddEntry(contextualAsync).Finally(static async _ => await Task.Yield());
        AddEntry(taskExpression).Finally(static () => Task.CompletedTask);

        new[] { synchronous, asynchronous, contextual, contextualAsync, taskExpression }
            .Select(static command => command.FreezeResult.IsSuccess)
            .Should().OnlyContain(static success => success);
    }

    private static Target AddEntry(Command command)
        => command.Target("entry").Description("Entry.").Run(static () => { });

    private static Command NewCommand(Root root)
        => global::Sotsera.Rafter.Rafter.Command(root).Description("Syntax fixture.");
}
