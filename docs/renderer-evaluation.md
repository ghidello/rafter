# Executable renderer evaluation

Issue [#3](https://github.com/sotsera/rafter/issues/3), evaluated 2026-10-20.
**Recommend Spectre.Console 0.57.2 for internal help and framework layout**, with Rafter-owned destination policy,
synchronization, and literal output. The evaluation does not implement the Rafter runtime or publish a package.

## Integration established by the probe

[RendererDestination.cs](../fixtures/renderer/shared/RendererDestination.cs) uses Spectre's public Paragraph/IRenderable
layout and AnsiWriter APIs. Each destination has its own writer, policy, layout profile, and gate. Capabilities are
detected against the actual Console.Out or Console.Error before any recording wrapper is added. Redirection and plain
mode override styling; default profile enrichers are disabled even when CI/GITHUB_ACTIONS is present.

Formatted help is rendered into segments, with explicit folding for long identifiers. Rafter applies the existing
indentation to continuation lines, maps layout line breaks to platform newlines, and writes segments to the destination.
Plain output writes segment text directly, avoiding legacy-console color changes and the non-ANSI WriteAnsi callback
that otherwise drops text. Styled output uses AnsiWriter with explicit capabilities. No global AnsiConsole.Console
replacement, markup parsing, exception formatter, live renderer, or Spectre.Console.Cli is involved.

Script/child text and exception details bypass layout. Styled prefixes are written and reset under the same gate as the
literal chunk. Original indentation, blank lines, CR/LF characters, long lines, and partial final lines are preserved;
no final newline is invented for a literal chunk. LF starts a new logical line for prefixes; CR is passed through and
does not add another prefix. Raw child ANSI is also passed through. This is evidence of preservation, not a newly
adopted sanitization or child-terminal-control policy.

The caller must await its producers, then await both destinations' flushes. Write/flush failures propagate for the
future invocation boundary to classify; the probe does not invent a successful completion after an output error.
Completion rejects later writes through that destination and never disposes or replaces the host's TextWriter.
The fixture has no detached producers, animation, or background output queue.

The nonempty **NO_COLOR** policy was confirmed during this evaluation: suppress Rafter colors while allowing emphasis
and terminal prefixes. Plain mode remains stronger. See the [accepted output design](output-design.md).

## Executed checks

The existing project now reports **Passed: 109, Failed: 0, Skipped: 0** locally: 83 prior cases and 26 renderer cases.
[RendererTests.cs](../tests/Rafter.Evaluation.Tests/RendererTests.cs) covers:

- All three accepted help examples at 100 and 48 columns, in plain and styled form, including long aliases, literal
  brackets, combining marks, wide characters, and an emoji sequence. Content, punctuation, alias order, blank lines,
  and continuation indentation are checked. A separate cell-boundary assertion checks combining and wide characters.
- Literal script/child chunks and full real nested exceptions, with no layout-driven truncation or reflow.
- CI variables versus explicit destination policy, NO_COLOR emphasis, and the legacy WriteAnsi no-op counterexample.
- Concurrent framework and literal writes to both streams, including prefix/reset boundaries. Each write is
  serialized per destination; no total order across stdout/stderr or across separate partial chunks is promised.
- Delayed flush, independent destinations, late writes, styled/plain write failures, flush failures, and host ownership.
- Actual isolated file-app and ordinary-project consumers, redirected stream contents, exit codes, and help on stdout.

Separately, [Verify-RendererTerminal.ps1](../tools/Verify-RendererTerminal.ps1) exercises **18 real Windows pseudoterminal
cases**: all four stdout/stderr redirection combinations in normal/plain/NO_COLOR modes, plus all three help examples
at both widths. CI and GITHUB_ACTIONS are set in every case. It checks actual child handle states, preserves captured
pre-terminal text, and verifies that help leaves stderr empty. Rendering width is explicitly 100 or 48 columns; the
enclosing terminal may wrap wider content again. The [captured help transcripts](renderer-samples.md) show the final text.

Local environment: Windows 10.0.26220 x64, SDK 10.0.402, consumer runtime .NET 10.0.13, Codex-provided Windows
pseudoterminal, TERM=xterm-256color, UTF-8 stdout/stderr. ANSI policy uses real destination detection; unit tests also
exercise deliberate capability combinations. CI uses redirected handles on Windows Server 2022 and records its SDK,
OS, image, build log, and TRX. PR checks provide the current CI result; this does not claim Linux/macOS terminal support.

The csharp-scripts skill guided actual file-app consumers and explicit managed execution. The run-tests skill guided
native-MTP execution and TRX verification. Existing tests were reused; the three added ordinary projects are comparison
consumers, not new test projects. Fixtures remain checked in as reproducible evidence.

## Cost comparison

[Measure-Renderer.ps1](../tools/Measure-Renderer.ps1) compares three separately built consumers:

| Variant | Work performed | Modern dependency assemblies |
| --- | --- | --- |
| Framework | Direct unstyled TextWriter.WriteLine | None |
| Ansi only | AnsiWriter styled line with explicit capabilities | Spectre.Console.Ansi: 242,176 bytes |
| Full | Destination creation plus styled Paragraph layout and segment writing | Spectre.Console: 847,872 bytes, plus Ansi: 242,176 bytes |

The common workload is one short cancellation message to TextWriter.Null. It isolates renderer initialization and
layout/writing cost; it is not a terminal throughput benchmark or an apples-to-apples help-layout implementation in
all three variants. Only full Spectre supplies help wrapping in this comparison. The framework baseline deliberately
does less work, and the Ansi-only alternative would still need Unicode-aware help layout from another implementation.

Final warmed-cache launch medians were **572.7 / 582.4 / 602.3 ms** for framework / Ansi / full file apps, and
**96.2 / 100.1 / 115.4 ms** for direct project consumers. Full Spectre added about 19–30 ms relative to the framework
baseline in this run. Its first measured render allocated about 130 KB; reused writes allocated 3,464 bytes each.
The first full-consumer launches were much slower (1,277.7 ms file app and 692.8 ms project), reinforcing that a single
first-launch measurement is not a reliable steady startup estimate.

The [measurement record](renderer-measurements.md) includes every sample, restore/build time, first-render allocation,
and reused-writer measurement. Each host/variant has one first launch after build and nine subsequent fresh-process
launches, interleaved across variants. File-app timings include dotnet run --no-build and its SDK overhead; ordinary
consumers run through dotnet DLL directly. Do not compare the two host paths as renderer overhead.

The initial fresh CLR render and a warm reused writer are measured separately inside each process. Allocation counts
are managed bytes from GC.GetTotalAllocatedBytes, not native memory, working set, or deployment size. The warm operation
measurement follows 100 warm-up writes and covers 1,000 writes. Restore/build timings use the existing package/build
caches and are excluded from runtime timings. First-launch results are noisy; OS caches were not flushed, so these
are not reproducible OS-cold-cache measurements or startup guarantees. No performance threshold is invented here.

To reproduce, use the [harness test commands](evaluation-harness.md#run-locally), then from the repository root:

```powershell
./tools/Measure-Renderer.ps1
./tools/Verify-RendererTerminal.ps1
```

The measurement script builds all six consumers. Run the terminal script in a terminal with both parent streams
attached; it deliberately fails if this condition is not met. If running it alone, first build
`fixtures/renderer/full/project/Consumer.csproj` in Release. Raw JSON is retained under `artifacts/renderer/`.

The measured extra cost buys the help layout already exercised by the tests. Cache renderer state per invocation and
destination, keep literal writes direct, and avoid capability detection per line. Revisit eager setup in the first
runtime slice if representative minimal scripts show a material startup regression. Do not add a custom Unicode
wrapper merely to remove this dependency without equivalent correctness and cost evidence.

## Dependencies and limits

The central versions are Spectre.Console 0.57.2 and Spectre.Console.Ansi 0.57.2. Restored net10.0 assets resolve the full
package to Ansi 0.57.2, with no further transitive packages. Both are MIT-licensed and identify source/tag commit
`bbbb5729dde27b58deee44f447a788eea46ee451`. The test reference is private and the test project is non-packable; the
comparison consumers select only their respective dependency. No upstream implementation source was copied.
See the [source review](renderer-review.md) and the generated project.assets.json files for package/API provenance.

Child ANSI remains an open policy: its resets, cursor motion, colors, and control sequences can affect the terminal.
The observed CR example visibly rewrites a terminal line even though captured text is preserved. A partial stdout line
can visually meet a stderr message on the same terminal; independent streams cannot promise a merged display order.
Output failures may also prevent writing a style reset. These are explicit limitations, not reasons to sanitize or
rewrite arbitrary child output silently.

Still deferred: the Rafter runtime, production async output ownership/error precedence, child-ANSI policy, non-ANSI
legacy-console visual behavior, Linux/macOS, Native AOT, a shipping NuGet package, actual child-process capture, and
performance budgets for representative scripts. String/chunk preservation tests are not a process-byte-capture API.
