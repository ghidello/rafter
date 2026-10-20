# Renderer review: Spectre.Console

Reviewed 2026-10-20. The [executable evaluation](renderer-evaluation.md) now recommends `Spectre.Console` 0.57.2
as the internal renderer for Rafter-authored help and formatted messages. Preserve script/child text and
full exception details through a literal output path. Keep presentation policy and output ownership in Rafter.
The package is pinned in evaluation tooling, with real Windows terminal/consumer checks and startup/allocation
measurements. No Rafter runtime implementation was added. The existing output design remains the accepted contract.

## Reviewed baseline

The current stable package listing is **0.57.2**, published 2026-07-02, under MIT. It provides net8.0, net9.0, net10.0,
and netstandard2.0 assets. The modern .NET assets depend on `Spectre.Console.Ansi >= 0.57.2`; the netstandard2.0 asset
also lists Microsoft.Bcl.TimeProvider and System.Memory. The package download is about 2.13 MB; that is not a measure
of consumer deployment size, loaded memory, or startup time. [Package metadata](https://www.nuget.org/packages/Spectre.Console/0.57.2)

Findings below use upstream tag `0.57.2`, commit `bbbb5729dde27b58deee44f447a788eea46ee451`. No upstream code was copied.
The evaluated net10.0 graph resolves Spectre.Console.Ansi 0.57.2 with no further dependencies.

## Fit against Rafter's output contract

| Requirement | Source finding | Proposed integration |
| --- | --- | --- |
| Independent stdout/stderr | AnsiConsoleSettings accepts an output; AnsiConsoleOutput and AnsiDetector distinguish standard output from standard error and their redirection flags. | Create separate instances with the actual destination writers. Avoid the global static console. Verify both asymmetric redirection cases. |
| Plain output even in CI | Profile enrichers run after capability detection. The GitHub enricher explicitly enables ANSI; default enrichers are enabled. | Disable default enrichers initially. Enforce plain/redirection policy independently of detected styling support. |
| Literal names and descriptions | Text and Paragraph support programmatic styles without requiring markup strings. | Treat user-authored text as data; do not interpret brackets, including target headings, as markup. |
| Readable narrow help | Text delegates to Paragraph, which measures console cells and wraps to available width. | Use styled text/paragraphs for help and framework prose. Preserve the agreed grouping, alias order, commas, and flat layout. |
| Preserve original output and stacks | Paragraph splits lines and whitespace and can wrap, crop, or omit content at constrained widths. | Bypass layout for script/child text and full exception details. Add only the agreed presentation gutter/prefix, without reflow or truncation. |
| Non-ANSI literal output | The legacy backend's WriteAnsi callback is a no-op. | Do not use IAnsiConsole.WriteAnsi as a universal writer. Plain output must explicitly reach the destination TextWriter. |
| Concurrent output | AnsiConsoleFacade locks writes per instance; this does not cover direct writes outside that instance or a second instance. | Coordinate Rafter-owned formatted and literal writes, including prefix/reset sequences. Preserve stream destinations without promising merged ordering. |

Sources: [output destination](https://github.com/spectreconsole/spectre.console/blob/0.57.2/src/Spectre.Console/AnsiConsoleOutput.cs),
[ANSI detection](https://github.com/spectreconsole/spectre.console/blob/0.57.2/src/Spectre.Console.Ansi/AnsiDetector.cs),
[factory ordering](https://github.com/spectreconsole/spectre.console/blob/0.57.2/src/Spectre.Console/AnsiConsoleFactory.cs),
[enrichment defaults](https://github.com/spectreconsole/spectre.console/blob/0.57.2/src/Spectre.Console/Enrichment/ProfileEnrichment.cs),
[GitHub enrichment](https://github.com/spectreconsole/spectre.console/blob/0.57.2/src/Spectre.Console/Enrichment/CI/GitHubEnricher.cs),
[paragraph layout](https://github.com/spectreconsole/spectre.console/blob/0.57.2/src/Spectre.Console/Widgets/Paragraph.cs),
[legacy backend](https://github.com/spectreconsole/spectre.console/blob/0.57.2/src/Spectre.Console/Internal/Backends/Legacy/LegacyConsoleBackend.cs),
[write locking](https://github.com/spectreconsole/spectre.console/blob/0.57.2/src/Spectre.Console/Internal/Backends/AnsiConsoleFacade.cs).

## Proposed output boundaries

Rafter decides message content, stream, verbosity, indentation, prefix, and status style. Spectre remains an internal
dependency: scripts continue to call `context.WriteLine`, without Spectre types or markup semantics in that API.
No Spectre.Console.Cli, prompts, live displays, global console replacement, or exception formatter is selected by this review.

Use layout only where wrapping is intentional. For literal content, write the original text through a coordinated
destination writer; terminal prefixes can be styled separately and reset before the original text. The terminal may
visually wrap long lines, but Rafter must not insert layout-driven newlines into that content. This is a text-preservation
requirement, not a newly promised byte-for-byte process capture API. The evaluation now checks partial lines and
carriage-return preservation; production child-ANSI policy and actual process-capture integration remain open.

The evaluated adapter bypasses the legacy backend's global Console color changes and the WriteAnsi no-op by rendering
through public IRenderable APIs and writing the resulting segments. Framework help uses measured plain layout without
styling side effects. Do not crop long identifiers or hide full exception details to satisfy a narrow width.

Capability checks recognize standard writers by identity; wrapping a writer before detection can change the result.
Detect against the real destination and pass deliberate capabilities to adapters. Do not force Unicode output by
changing the host's global encoding as an incidental renderer setup step.
[Writer identity checks](https://github.com/spectreconsole/spectre.console/blob/0.57.2/src/Spectre.Console/Extensions/Bcl/TextWriterExtensions.cs)

`NO_COLOR` disables colors in the reviewed detector, but does not itself mean that all ANSI decoration is disabled.
We confirmed respecting a nonempty value for color and keeping `--plain` as the explicit complete presentation override.
The evaluation verifies retained emphasis/prefixes and independent redirection.
[Color detection](https://github.com/spectreconsole/spectre.console/blob/0.57.2/src/Spectre.Console.Ansi/ColorSystemDetector.cs)

## Alternatives and cost

| Option | Benefit | Work retained by Rafter |
| --- | --- | --- |
| Spectre.Console — preferred candidate | Help layout, text measurement, styling, and capability support. | Output policy, literal/plain paths, synchronization, and lifetime/error handling. |
| Spectre.Console.Ansi alone | Capability-aware styling and direct text writing with a smaller package surface. | Help measurement and wrapping as well as output policy; writers need coordinated access. |
| Framework APIs only | No third-party rendering dependency. | Terminal detection, ANSI styling/fallback, Unicode-aware layout, and all output behavior. |

The separate Ansi package is also MIT, lists no package dependencies for its supplied frameworks, and has an approximately
785 KB download at 0.57.2. Its writer's plain Write method forwards text directly. This makes it a credible fallback if
the full renderer adds little value or measurable overhead. It is not evidence that either option starts faster.
[Ansi package metadata](https://www.nuget.org/packages/Spectre.Console.Ansi/0.57.2),
[AnsiWriter source](https://github.com/spectreconsole/spectre.console/blob/0.57.2/src/Spectre.Console.Ansi/AnsiWriter.cs)

Prefer reusing help layout over writing Unicode wrapping ourselves, but make final adoption depend on a small working
comparison. Cache writers per invocation/destination rather than detecting capabilities for every line.
[Official ANSI-writing guidance](https://spectreconsole.net/console/how-to/writing-ansi/)

## Executable adoption checks

The detailed acceptance checklist now lives in
[#3: Verify Spectre.Console output compatibility and startup cost](https://github.com/sotsera/rafter/issues/3),
depending on the harness in #1. Retain the output contract above and check actual captured text, not just screenshots
or stripped-color snapshots. Record terminal environments and separate restore/build overhead from runtime cost.

The [evaluation record](renderer-evaluation.md) supersedes the earlier source-only status. It records executable
checks, captured help, cost comparison, and remaining limits. The candidate remains internal; adopting it does not
also adopt Spectre.Console.Cli or its live UI features, and does not claim cross-platform verification.
