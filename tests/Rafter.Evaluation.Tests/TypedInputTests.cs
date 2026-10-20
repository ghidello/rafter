using System.Globalization;
using AwesomeAssertions;
using Sotsera.Rafter;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.Evaluation.Tests;

public sealed class TypedInputTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Repeated_or_conflicting_policies_are_invalid_before_help(int configuration)
    {
        using var host = new InputHost();
        var option = host.Command.Option<int>("count", "Count.");
        if (configuration is 0 or 2)
        {
            option.Required();
        }
        else
        {
            option.Default(1);
        }
        if (configuration is 0 or 3)
        {
            option.Required();
        }
        else
        {
            option.Default(1);
        }
        (await host.Run("--help")).Should().Be(2);
        host.Error.ToString().Should().Contain("configured only once");
        host.Output.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-")]
    [InlineData("--")]
    [InlineData("---name")]
    [InlineData("Name")]
    [InlineData("1name")]
    [InlineData("na_me")]
    [InlineData("name-")]
    [InlineData("na--me")]
    [InlineData("na mé")]
    [InlineData("help")]
    [InlineData("-quiet")]
    [InlineData("--verbose")]
    [InlineData("plain")]
    public async Task Invalid_names_and_aliases_are_declaration_errors(string? name)
    {
        foreach (var alias in new[] { false, true })
        {
            using var host = new InputHost();
            var option = host.Command.Option<string>(alias ? "name" : name!, "Description.");
            if (alias)
            {
                option.Aliases(name!);
            }
            (await host.Run("--help")).Should().Be(2);
            host.Error.ToString().Should().Contain("Invalid command");
            host.Output.ToString().Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Declaration_errors_are_aggregated_before_help()
    {
        using var host = new InputHost();
        host.Command.Option<string>("name", " ").Default(null!);
        host.Command.Option<int>("count", "Count.").Aliases((string[])null!);
        host.Command.Option<int?>("nullable", "Nullable.");
        host.Command.Option<double>("double", "Double.");
        host.Command.Option<string>("rule", "Rule.").Validate(" ", null!);
        host.Command.Validate(" ", null!);
        (await host.Run("--help")).Should().Be(2);
        host.Error.ToString().Should().Contain("Description").And.Contain("Default must not be null")
            .And.Contain("Alias arrays").And.Contain("Supported supplied types").And.Contain("Rules require")
            .And.Contain("Combined rules");
    }

    [Fact]
    public async Task Alias_expansion_preserves_explicit_prefixes_and_deduplicates_in_declaration_order()
    {
        using var host = new InputHost();
        var name = host.Command.Option<string>("name", "Name.")
            .Aliases("n", "--name", "n", "-name", "--n")
            .Required()
            .Aliases("label");
        foreach (var spelling in new[] { "--name", "-n", "-name", "--n", "--label" })
        {
            (await host.Command.RunAsync([spelling, "Ada"], context =>
            {
                context.GetValue(name).Should().Be("Ada");
            })).Should().Be(0);
        }
        (await host.Run("--help")).Should().Be(0);
        host.Output.ToString().Should().Contain("--name, -n, -name, --n, --label <string>");
    }

    [Theory]
    [InlineData("name", "--name")]
    [InlineData("n", "-n")]
    public async Task Expanded_collisions_between_options_are_invalid(string first, string second)
    {
        using var host = new InputHost();
        host.Command.Option<string>(first, "First.");
        host.Command.Option<string>("other", "Second.").Aliases(second);
        (await host.Run("--help")).Should().Be(2);
        host.Error.ToString().Should().Contain("belongs to more than one option");
    }

    [Fact]
    public async Task Optional_values_are_null_and_defaults_are_used_only_when_absent()
    {
        using var host = new InputHost();
        var name = host.Command.Option<string>("name", "Name.");
        var count = host.Command.Option<int>("count", "Count.");
        var flag = host.Command.Option<bool>("flag", "Flag.");
        var fallback = host.Command.Option<int>("fallback", "Fallback.").Default(7);
        (await host.Command.RunAsync([], context =>
        {
            context.GetValue(name).Should().BeNull();
            context.GetValue(count).Should().BeNull();
            context.GetValue(flag).Should().BeNull();
            context.GetValue(fallback).Should().Be(7);
        })).Should().Be(0);
        (await host.Command.RunAsync(["--name=", "--count", "0", "--flag=false", "--fallback=0"], context =>
        {
            context.GetValue(name).Should().BeEmpty();
            context.GetValue(count).Should().Be(0);
            context.GetValue(flag).Should().BeFalse();
            context.GetValue(fallback).Should().Be(0);
        })).Should().Be(0);
    }

    [Fact]
    public async Task Required_means_presence_including_empty_zero_and_false()
    {
        using var host = new InputHost();
        var name = host.Command.Option<string>("name", "Name.").Required();
        var count = host.Command.Option<int>("count", "Count.").Required();
        var flag = host.Command.Option<bool>("flag", "Flag.").Required();
        (await host.Run()).Should().Be(2);
        host.Error.ToString().Should().Contain("--name is required").And.Contain("--count is required")
            .And.Contain("--flag is required");
        (await host.Command.RunAsync(["--name=", "--count=0", "--flag=false"], context =>
        {
            context.GetValue(name).Should().BeEmpty();
            context.GetValue(count).Should().Be(0);
            context.GetValue(flag).Should().BeFalse();
        })).Should().Be(0);
    }

    [Theory]
    [InlineData("--flag", null, true)]
    [InlineData("--flag", "TRUE", true)]
    [InlineData("--flag", "False", false)]
    [InlineData("--flag=false", null, false)]
    public async Task Booleans_accept_bare_space_and_equals_forms(string first, string? second, bool expected)
    {
        using var host = new InputHost();
        var flag = host.Command.Option<bool>("flag", "Flag.").Required();
        (await host.Command.RunAsync(second is null ? [first] : [first, second], context =>
        {
            context.GetValue(flag).Should().Be(expected);
        })).Should().Be(0);
    }

    [Theory]
    [InlineData("--name", "--help")]
    [InlineData("--count=")]
    [InlineData("--flag=")]
    [InlineData("--flag=private-invalid")]
    [InlineData("--count=private-invalid")]
    [InlineData("--count=2147483648")]
    [InlineData("--name:private-invalid")]
    [InlineData("--name=one", "-n=one")]
    [InlineData("--flag", "--flag")]
    [InlineData("@private-invalid")]
    [InlineData("[private-invalid]")]
    [InlineData("private-invalid")]
    [InlineData("--unknown=private-invalid")]
    [InlineData("--")]
    [InlineData("-nf")]
    public async Task Invalid_supplied_inputs_fail_even_with_help_without_echoing_values(params string[] args)
    {
        using var host = new InputHost();
        host.Command.Option<string>("name", "Name.").Aliases("n");
        host.Command.Option<int>("count", "Count.");
        host.Command.Option<bool>("flag", "Flag.").Aliases("f");
        var culture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
            (await host.Run([.. args, "--help"])).Should().Be(2);
        }
        finally
        {
            CultureInfo.CurrentUICulture = culture;
        }
        host.Output.ToString().Should().BeEmpty();
        host.Error.ToString().Should().Contain("Invalid input").And.NotContain("private-invalid");
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("a:b=c")]
    [InlineData("[red]literal[/]")]
    [InlineData("@file")]
    [InlineData("界面 café")]
    public async Task Attached_strings_preserve_literal_content(string text)
    {
        using var host = new InputHost();
        var name = host.Command.Option<string>("name", "Name.").Required();
        (await host.Command.RunAsync(["--name=" + text], context =>
        {
            context.GetValue(name).Should().Be(text);
        })).Should().Be(0);
    }

    [Fact]
    public async Task Rules_aggregate_in_declaration_order_skip_absent_optionals_and_block_combined_rules()
    {
        using var host = new InputHost();
        var order = new List<string>();
        host.Command.Option<string>("optional", "Optional.").Validate("Skipped.", _ => throw new Exception());
        host.Command.Option<int>("first", "First.")
            .Validate("First failure.", _ =>
            {
                order.Add("first");
                return false;
            })
            .Default(1)
            .Validate("Second failure.", _ =>
            {
                order.Add("second");
                return false;
            });
        host.Command.Option<int>("second", "Second.")
            .Default(2)
            .Validate("Third failure.", _ =>
            {
                order.Add("third");
                return false;
            });
        host.Command.Validate("Skipped combined.", _ => throw new Exception());
        (await host.Command.RunAsync([], _ => { order.Add("body"); })).Should().Be(2);
        order.Should().Equal("first", "second", "third");
        host.Error.ToString().Should().Contain("--first: First failure.")
            .And.Contain("--first: Second failure.").And.Contain("--second: Third failure.")
            .And.NotContain("[Failed]");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Thrown_rules_stop_validation_and_preserve_earlier_diagnostics(bool combined)
    {
        using var host = new InputHost();
        var later = false;
        var option = host.Command.Option<int>("value", "Value.").Default(1);
        if (combined)
        {
            host.Command.Validate("Earlier failure.", _ => false);
            host.Command.Validate("Throw.", _ => throw new InvalidOperationException("rule fault"));
            host.Command.Validate("Later.", _ =>
            {
                later = true;
                return true;
            });
        }
        else
        {
            option.Validate("Earlier failure.", _ => false)
                .Validate("Throw.", _ => throw new InvalidOperationException("rule fault"))
                .Validate("Later.", _ =>
                {
                    later = true;
                    return true;
                });
        }
        (await host.Run()).Should().Be(1);
        later.Should().BeFalse();
        host.Error.ToString().Should().Contain("Earlier failure.").And.Contain("InvalidOperationException")
            .And.Contain("rule fault").And.NotContain("[OK]");
    }

    [Fact]
    public async Task Combined_rules_aggregate_and_read_the_same_prepared_values_as_the_body()
    {
        using var host = new InputHost();
        var count = host.Command.Option<int>("count", "Count.").Default(4);
        var calls = 0;
        count.Validate("Positive.", value =>
        {
            calls++;
            return value > 0;
        });
        host.Command.Validate("Four.", inputs => inputs.GetValue(count) == 4);
        (await host.Command.RunAsync([], context =>
        {
            context.GetValue(count).Should().Be(4);
            context.GetValue(count).Should().Be(4);
        })).Should().Be(0);
        calls.Should().Be(1);
        host.Command.Validate("Failure one.", _ => false);
        host.Command.Validate("Failure two.", _ => false);
        (await host.Run()).Should().Be(2);
        host.Error.ToString().Should().Contain("Failure one.").And.Contain("Failure two.");
    }

    [Fact]
    public async Task Snapshot_is_fixed_before_callbacks_and_values_are_isolated_between_runs()
    {
        using var host = new InputHost();
        var option = host.Command.Option<int>("count", "Count.");
        var calls = 0;
        option.Validate("Original rule.", _ =>
        {
            calls++;
            option.Default(9);
            option.Validate("Added later.", _ => false);
            return true;
        });
        (await host.Command.RunAsync(["--count=3"], context =>
        {
            context.GetValue(option).Should().Be(3);
            context.GetValue(option).Should().Be(3);
        })).Should().Be(0);
        calls.Should().Be(1);
        (await host.Run()).Should().Be(2);
        host.Error.ToString().Should().Contain("Added later.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Foreign_handles_are_execution_failures(bool combined)
    {
        using var host = new InputHost();
        var foreign = Sotsera.Rafter.Rafter.Command("Other.").Option<string>("name", "Name.").Default("other");
        if (combined)
        {
            host.Command.Validate("Foreign.", inputs => inputs.GetValue(foreign) == "other");
        }
        (await host.Command.RunAsync([], context =>
        {
            context.GetValue(foreign);
        })).Should().Be(1);
        host.Error.ToString().Should().Contain("does not belong to this invocation");
    }

    [Fact]
    public async Task A_handle_added_by_a_callback_is_not_part_of_the_current_snapshot()
    {
        using var host = new InputHost();
        ValueOption<int>? added = null;
        host.Command.Validate("Add a declaration.", _ =>
        {
            added ??= host.Command.Option<int>("later", "Later.").Default(9);
            return true;
        });
        (await host.Command.RunAsync([], context =>
        {
            context.GetValue(added!);
        })).Should().Be(1);
        (await host.Command.RunAsync([], context =>
        {
            context.GetValue(added!).Should().Be(9);
        })).Should().Be(0);
    }

    [Fact]
    public async Task Overlapping_invocations_keep_independent_prepared_values()
    {
        using var host = new InputHost();
        var count = host.Command.Option<int>("count", "Count.").Required();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = host.Command.RunAsync(["--count=1", "--quiet"], async context =>
        {
            context.GetValue(count).Should().Be(1);
            await release.Task;
            context.GetValue(count).Should().Be(1);
        });
        try
        {
            (await host.Command.RunAsync(["--count=2", "--quiet"], context =>
            {
                context.GetValue(count).Should().Be(2);
            })).Should().Be(0);
        }
        finally
        {
            release.TrySetResult();
            (await first).Should().Be(0);
        }
    }

    [Theory]
    [InlineData(48)]
    [InlineData(100)]
    public async Task Help_lists_dynamic_metadata_literally_without_running_rules(int width)
    {
        using var host = new InputHost(width);
        host.Command.Option<string>("name", "Literal [red]text[/] 界面.").Aliases("n", "label")
            .Required().Validate("Required rule.", _ => throw new Exception());
        host.Command.Option<bool>("flag", "Flag.").Default(false);
        host.Command.Option<int>("optional", "Optional.").Validate("Optional rule.", _ => throw new Exception());
        host.Command.Validate("Combined rule.", _ => throw new Exception());
        (await host.Run("--help")).Should().Be(0);
        host.Error.ToString().Should().BeEmpty();
        var text = System.Text.RegularExpressions.Regex.Replace(host.Output.ToString(), "\u001b\\[[0-9;]*m", "");
        text.Should().Contain("--name, -n, --label <string>").And.Contain("[red]text[/] 界面.")
            .And.Contain("Required.").And.Contain("Default: false.").And.Contain("[bool]")
            .And.Contain("Rules when supplied:").And.Contain("Combined rule.");
        host.Output.ToString().Should().NotContain("\x1b[");
    }

    [Fact]
    public async Task Cancellation_between_rules_prevents_later_callbacks_and_body()
    {
        using var host = new InputHost();
        var called = false;
        host.Command.Option<int>("count", "Count.").Default(1)
            .Validate("Cancel.", _ =>
            {
                host.Signal();
                return true;
            })
            .Validate("Later.", _ =>
            {
                called = true;
                return true;
            });
        (await host.Command.RunAsync([], _ => { called = true; })).Should().Be(130);
        called.Should().BeFalse();
        host.Error.ToString().Should().NotContain("OperationCanceledException");
    }

    private sealed class InputHost : IDisposable
    {
        private Action? _signal;
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public Command Command { get; }

        public InputHost(int width = 100)
        {
            Command = new Command("Typed inputs.", () => new InvocationHost("root", Output, Error,
                true, true, false, width, signal =>
                {
                    _signal = signal;
                    return new Subscription(() => _signal = null);
                }));
        }

        public Task<int> Run(params string[] args) => Command.RunAsync(args, _ => { });
        public void Signal() => _signal?.Invoke();

        public void Dispose()
        {
            Output.Dispose();
            Error.Dispose();
        }
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        public void Dispose() => unsubscribe();
    }
}
