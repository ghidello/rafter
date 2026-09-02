namespace Sotsera.Rafter.Tests;

public sealed class PhaseThreeSyntaxFixtureTests
{
    [Fact]
    public void EveryValueLookupShapeCompilesAgainstThePublicApi()
    {
        Command command = global::Sotsera.Rafter.Rafter.Command(Root.Invocation)
            .Description("Syntax fixture.");

        Option<string> optionalReference = command.Option<string>("optional-reference").Description("Optional reference.");
        Option<int?> optionalValue = command.Option<int?>("optional-value").Description("Optional value.");
        RequiredOption<Guid> required = command.RequiredOption<Guid>("required").Description("Required.");
        DefaultedOption<int> defaulted = command.Option<int>("defaulted").Description("Defaulted.").Default(1);
        RepeatedOption<string> repeated = command.RepeatedOption<string>("repeated").Description("Repeated.");

        Target entry = command.Target("entry")
            .Description("Entry.")
            .Run(context =>
            {
                string? reference = context.Value(optionalReference);
                int? value = context.Value(optionalValue);
                Guid requiredValue = context.Value(required);
                int defaultedValue = context.Value(defaulted);
                IReadOnlyList<string> repeatedValues = context.Value(repeated);
                _ = (reference, value, requiredValue, defaultedValue, repeatedValues);
            });

        command.FreezeResult.IsSuccess.Should().BeTrue();
        entry.Authored.Execution.IsSet.Should().BeTrue();
    }
}
