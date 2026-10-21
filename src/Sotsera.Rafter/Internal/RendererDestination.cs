using Spectre.Console;
using Spectre.Console.Rendering;

namespace Sotsera.Rafter.Internal;

internal sealed record DestinationPolicy(bool Rich, bool NoColor)
{
    public static DestinationPolicy Detect(TextWriter originalWriter, bool redirected, bool plain, bool noColor)
    {
        var capabilities = AnsiCapabilities.Create(originalWriter);
        return new(!redirected && !plain && capabilities.Ansi, noColor);
    }
}

// One gate per destination coordinates layout, literal text, prefixes, and final flushing.
// The caller owns the writer and must await its producers before CompleteAsync.
internal sealed class RendererDestination
{
    private readonly TextWriter _writer;
    private readonly DestinationPolicy _policy;
    private readonly IAnsiConsole _console;
    private readonly AnsiWriter _ansi;
    private readonly SemaphoreSlim _gate;
    private bool _lineStart = true;
    private bool _completed;
    private Exception? _failure;

    public Exception? Failure => _failure;

    public RendererDestination(TextWriter writer, DestinationPolicy policy, int width = 80,
        Dictionary<string, string>? environment = null, SemaphoreSlim? gate = null)
    {
        _gate = gate ?? new SemaphoreSlim(1, 1);
        _writer = writer;
        _policy = policy;
        _console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            Ansi = policy.Rich ? AnsiSupport.Yes : AnsiSupport.No,
            ColorSystem = policy.NoColor ? ColorSystemSupport.NoColors : ColorSystemSupport.TrueColor,
            Interactive = InteractionSupport.No,
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            EnvironmentVariables = environment
        });
        _console.Profile.Width = width;
        _ansi = new AnsiWriter(writer, new AnsiCapabilities
        {
            Ansi = policy.Rich,
            ColorSystem = policy.NoColor ? ColorSystem.NoColors : ColorSystem.TrueColor
        });
    }

    public void Framework(string text, Style style)
    {
        Layout(new Paragraph(text, style) { Overflow = Overflow.Fold });
    }

    public void Layout(IRenderable content, int indentation = 0)
    {
        _gate.Wait();
        try
        {
            ThrowIfCompleted();
            if (!_lineStart)
            {
                // A framework message starts its own line after a child's unterminated stderr chunk.
                _writer.Write(Environment.NewLine);
            }
            var layoutLineStart = true;
            var wroteContent = false;
            var width = Math.Max(1, _console.Profile.Width - indentation);
            foreach (var segment in content.Render(RenderOptions.Create(_console), width))
            {
                if (segment.IsLineBreak)
                {
                    _writer.Write(Environment.NewLine);
                    layoutLineStart = true;
                    wroteContent = true;
                    continue;
                }

                if (segment.Text.Length == 0)
                {
                    continue;
                }

                if (layoutLineStart && indentation > 0)
                {
                    _writer.Write(new string(' ', indentation));
                }

                if (_policy.Rich)
                {
                    _ansi.Write(segment.Text, segment.Style);
                }
                else
                {
                    // Rendering segments directly avoids the legacy backend and its Console color side effects.
                    _writer.Write(segment.Text);
                }

                layoutLineStart = false;
                wroteContent = true;
            }

            if (!wroteContent || !layoutLineStart)
            {
                _writer.Write(Environment.NewLine);
            }
            _lineStart = true;
        }
        catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
        {
            Interlocked.CompareExchange(ref _failure, exception, null);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Literal(string text, string prefix = "  > ")
    {
        _gate.Wait();
        try
        {
            ThrowIfCompleted();
            if (!_policy.Rich)
            {
                _writer.Write(text);
                if (text.Length > 0)
                {
                    _lineStart = text[^1] == '\n';
                }
                return;
            }

            // LF starts a logical line; CR is preserved without adding another prefix or rewriting child content.
            var start = 0;
            while (start < text.Length)
            {
                if (_lineStart)
                {
                    _ansi.Write(prefix, new Style(Color.Grey));
                    _lineStart = false;
                }

                var newline = text.IndexOf('\n', start);
                var end = newline < 0 ? text.Length : newline + 1;
                _writer.Write(text.AsSpan(start, end - start));
                _lineStart = newline >= 0;
                start = end;
            }
        }
        catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
        {
            Interlocked.CompareExchange(ref _failure, exception, null);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CompleteAsync()
    {
        await _gate.WaitAsync();
        try
        {
            ThrowIfCompleted();
            _completed = true;
            await _writer.FlushAsync();
        }
        catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
        {
            Interlocked.CompareExchange(ref _failure, exception, null);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ThrowIfCompleted()
    {
        if (_completed)
        {
            throw new InvalidOperationException("The invocation output has completed.");
        }
    }
}
