using System.CommandLine;
using System.CommandLine.Parsing;
using AwesomeAssertions;
using Rafter.Evaluation;
using Xunit;

namespace Rafter.Tests.Evaluation;

// These tests establish package behavior that motivates the adapter; they do not endorse it as Rafter behavior.
public sealed class ParserBaselineTests
{
    [Fact]
    public void Raw_parser_loses_empty_attached_values_and_can_consume_help_as_the_value()
    {
        var candidate = new ParserCandidate();
        candidate.Parse("--name=").Errors.Should().NotBeEmpty();
        candidate.Parse("--name", "").GetValue(candidate.Name).Should().BeEmpty();

        var flag = candidate.Parse("--flag=");
        flag.Errors.Should().BeEmpty();
        flag.GetValue(candidate.Flag).Should().BeTrue();

        var name = candidate.Parse("--name=", "--help");
        name.Errors.Should().BeEmpty();
        name.GetValue(candidate.Name).Should().Be("--help");
        name.GetResult(candidate.Help)!.Implicit.Should().BeTrue();
    }

    [Fact]
    public void Raw_parser_loses_the_option_association_for_invalid_Boolean_values()
    {
        var candidate = new ParserCandidate();
        var result = candidate.Parse("--flag=private-rejected-value-9a28");

        result.Errors.Should().ContainSingle().Which.SymbolResult.Should().BeOfType<CommandResult>();
        result.GetResult(candidate.Flag)!.Tokens.Should().BeEmpty();
        result.UnmatchedTokens.Should().Equal("private-rejected-value-9a28");
    }

    [Fact]
    public void Raw_parser_accepts_colon_and_discards_unknown_leading_directives()
    {
        var candidate = new ParserCandidate();
        var colon = candidate.Parse("--name:Ada");
        colon.Errors.Should().BeEmpty();
        colon.GetValue(candidate.Name).Should().Be("Ada");

        var directive = candidate.Parse("[not-registered]");
        directive.Errors.Should().BeEmpty();
        directive.Tokens.Should().Contain(token => token.Type == TokenType.Directive);
    }

    [Fact]
    public void Builtin_help_clears_errors_during_parsing_even_without_invocation()
    {
        var root = new RootCommand();
        root.Options.Add(new Option<int>("--count"));

        root.Parse(["--help", "--count=bad"]).Errors.Should().BeEmpty();
        new ParserCandidate().Parse("--help", "--count=bad").Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Raw_presence_and_repeat_counts_are_available_independently_of_values()
    {
        var candidate = new ParserCandidate();
        var absent = candidate.Parse();
        absent.GetValue(candidate.Flag).Should().BeFalse();
        absent.GetResult(candidate.Flag)!.Implicit.Should().BeTrue();

        var repeated = candidate.Parse("--flag", "-f=false");
        repeated.GetResult(candidate.Flag)!.Implicit.Should().BeFalse();
        repeated.GetResult(candidate.Flag)!.IdentifierTokenCount.Should().Be(2);
    }

    [Fact]
    public void Raw_parser_strips_a_first_token_matching_the_executable_name()
    {
        var candidate = new ParserCandidate();
        candidate.Parse(candidate.Command.Name).Errors.Should().BeEmpty();

        var adapted = new CandidateAdapter().Evaluate(candidate.Command.Name);
        adapted.ExitCode.Should().Be(2);
        adapted.Diagnostics.Should().Contain(new CandidateDiagnostic("UnexpectedInput", null));
    }
}
