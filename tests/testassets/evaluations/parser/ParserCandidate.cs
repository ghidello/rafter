using System.CommandLine;

namespace Rafter.Evaluation;

// Evaluation-only schema. This is not Rafter's public API or a production parser adapter.
internal sealed class ParserCandidate
{
    public RootCommand Command { get; } = new("Parser compatibility probe");
    public Option<string> Name { get; } = new("--name", "-n", "--label", "-name", "--n");
    public Option<int> Count { get; } = new("--count", "-c");
    public Option<bool> Flag { get; } = new("--flag", "-f");
    public Option<bool> Help { get; } = new("--help") { Arity = ArgumentArity.Zero };
    public Option<bool> Quiet { get; } = new("--quiet") { Arity = ArgumentArity.Zero };
    public Option<bool> Verbose { get; } = new("--verbose") { Arity = ArgumentArity.Zero };

    public ParserCandidate()
    {
        Command.Options.Clear();
        Command.Directives.Clear();
        Command.Options.Add(Name);
        Command.Options.Add(Count);
        Command.Options.Add(Flag);
        Command.Options.Add(Help);
        Command.Options.Add(Quiet);
        Command.Options.Add(Verbose);
    }

    public ParseResult Parse(params string[] arguments)
    {
        return Command.Parse(arguments, new ParserConfiguration
        {
            EnablePosixBundling = false,
            ResponseFileTokenReplacer = null
        });
    }
}
