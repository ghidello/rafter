using System.Collections.Immutable;
using static Sotsera.Rafter.BindingEngine;

namespace Sotsera.Rafter.Tests;

public sealed class PhaseThreeGrammarTests
{
    [Fact]
    public void ParsesSupportedLongShortBooleanAndRepeatedForms()
    {
        Command command = NewCommand();
        command.RequiredOption<string>("name").Description("Name.").Alias('n');
        command.Option<bool>("force").Description("Force.").Default(false).Alias('f');
        command.RepeatedOption<string>("item").Description("Item.").Alias('i');
        command.Target("entry").Description("Entry.");
        CommandModel.CommandDefinition model = command.FreezeResult.Model!;

        ParsedInput parsed = CommandLineParser.Parse(
            model,
            ["--name=value=with=equals", "-f", "--item", "one", "-i", "two", "--plain"]);

        parsed.Diagnostics.Should().BeEmpty();
        parsed.Plain.Should().BeTrue();
        Values(model, parsed, "name").Should().Equal("value=with=equals");
        Values(model, parsed, "force").Should().Equal("true");
        Values(model, parsed, "item").Should().Equal("one", "two");
    }

    [Fact]
    public void RecoversFromMalformedTokensWithoutEchoingPayloadsOrPositionals()
    {
        Command command = NewCommand();
        command.RequiredOption<string>("name").Description("Name.").Alias('n');
        command.Target("entry").Description("Entry.");
        CommandModel.CommandDefinition model = command.FreezeResult.Model!;
        ImmutableArray<string> arguments = [
            "--unknown=hidden-payload",
            "positional-secret",
            "-n=value",
            "--name",
            "--other",
            "--",
            "--plain",
            "--plain",
        ];

        ParsedInput parsed = CommandLineParser.Parse(model, arguments);

        parsed.Diagnostics.Should().HaveCount(7);
        string messages = string.Join("\n", parsed.Diagnostics.Select(static diagnostic => diagnostic.Message));
        messages.Should().NotContain("hidden-payload");
        messages.Should().NotContain("positional-secret");
        messages.Should().Contain("Unknown option \"--unknown\".");
        messages.Should().Contain("Unsupported short-option syntax.");
        messages.Should().Contain("requires a value");
        parsed.Plain.Should().BeTrue();
    }

    [Fact]
    public void AValueBearingDuplicateRemainsEligibleAfterAMissingOccurrence()
    {
        Command command = NewCommand();
        command.RequiredOption<string>("name").Description("Name.");
        command.Target("entry").Description("Entry.");
        CommandModel.CommandDefinition model = command.FreezeResult.Model!;

        ParsedInput parsed = CommandLineParser.Parse(model, ["--name", "--name=selected"]);

        Values(model, parsed, "name").Should().Equal(null, "selected");
        parsed.Diagnostics.Select(static diagnostic => diagnostic.Message).Should().Contain(message => message.Contains("requires a value", StringComparison.Ordinal));
        parsed.Diagnostics.Select(static diagnostic => diagnostic.Message).Should().Contain(message => message.Contains("only once", StringComparison.Ordinal));
    }

    [Fact]
    public void EnumAndBooleanConversionUseTheDeliberatelySmallGrammar()
    {
        OptionBindingStrategy enumStrategy = OptionBindingStrategy.Create<BuildMode>();
        OptionBindingStrategy nullableStrategy = OptionBindingStrategy.Create<int?>();
        OptionBindingStrategy booleanStrategy = OptionBindingStrategy.Create<bool>();

        enumStrategy.Convert("release").Value.Should().Be(BuildMode.Release);
        enumStrategy.Convert("1").IsSuccess.Should().BeFalse();
        enumStrategy.Convert("Debug,Release").IsSuccess.Should().BeFalse();
        nullableStrategy.Convert("42").Value.Should().Be(42);
        nullableStrategy.Convert(string.Empty).IsSuccess.Should().BeFalse();
        booleanStrategy.Convert("TRUE").Value.Should().Be(true);
        booleanStrategy.Convert(" true ").IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void FixedSeedMalformedInputsAlwaysTerminateAndKeepPositionsBounded()
    {
        Command command = NewCommand();
        command.Option<bool>("force").Description("Force.").Default(false).Alias('f');
        command.RequiredOption<string>("name").Description("Name.").Alias('n');
        command.RepeatedOption<int>("item").Description("Item.");
        command.Target("entry").Description("Entry.");
        CommandModel.CommandDefinition model = command.FreezeResult.Model!;
        Random random = new(0x5EED);
        const string alphabet = "-=_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789秘密";

        for (int iteration = 0; iteration < 1_000; iteration++)
        {
            int count = random.Next(0, 21);
            ImmutableArray<string>.Builder arguments = ImmutableArray.CreateBuilder<string>(count);
            for (int index = 0; index < count; index++)
            {
                int length = random.Next(0, 31);
                char[] characters = new char[length];
                for (int character = 0; character < length; character++)
                {
                    characters[character] = alphabet[random.Next(alphabet.Length)];
                }

                arguments.Add(new string(characters));
            }

            ParsedInput parsed = CommandLineParser.Parse(model, arguments.MoveToImmutable());

            parsed.Diagnostics
                .Select(static diagnostic => diagnostic.ArgumentPosition)
                .Where(static position => position.HasValue)
                .Select(static position => position!.Value)
                .Should().OnlyContain(position => position >= 0 && position < count);
        }
    }

    private static IEnumerable<string?> Values(
        CommandModel.CommandDefinition model,
        ParsedInput parsed,
        string optionName)
    {
        Guid id = model.Options.Single(option => string.Equals(option.Name, optionName, StringComparison.Ordinal)).Id;
        return parsed.Occurrences[id].Select(static occurrence => occurrence.Value);
    }

    private static Command NewCommand()
        => global::Sotsera.Rafter.Rafter.Command(Root.Invocation).Description("Test command.");

    private enum BuildMode
    {
        Debug,
        Release,
    }
}
