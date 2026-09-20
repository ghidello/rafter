using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace Sotsera.Rafter;

internal static class ProcessRuntime
{
    private const int BufferSize = 16 * 1024;
    private const long DefaultCaptureLimitBytes = 1024 * 1024;
    private static IProcessAdapterFactory _adapterFactory = SystemProcessAdapter.Factory;
    private static ProcessRuntimePolicy _policy = ProcessRuntimePolicy.Default;

    internal static IProcessAdapterFactory AdapterFactory
    {
        get => Volatile.Read(ref _adapterFactory);
        set => Volatile.Write(ref _adapterFactory, value ?? throw new ArgumentNullException(nameof(value)));
    }

    internal static ProcessRuntimePolicy Policy
    {
        get => Volatile.Read(ref _policy);
        set => Volatile.Write(ref _policy, value ?? throw new ArgumentNullException(nameof(value)));
    }

    internal static Task<ProcessExit> Run(
        RafterContext context,
        ProcessSpecification specification,
        ProcessOperationScope.Operation operation)
        => ExecuteRunAsync(context, specification, operation);

    internal static Task<ProcessCapture> Capture(
        RafterContext context,
        ProcessSpecification specification,
        ProcessOperationScope.Operation operation)
        => ExecuteCaptureAsync(context, specification, operation);

    private static async Task<ProcessExit> ExecuteRunAsync(
        RafterContext context,
        ProcessSpecification specification,
        ProcessOperationScope.Operation operation)
    {
        try
        {
            ProcessExecutionResult result = await ExecuteAsync(
                context,
                specification,
                operation,
                capture: false).ConfigureAwait(false);
            return new ProcessExit(result.ExitCode);
        }
        finally
        {
            operation.Complete();
        }
    }

    private static async Task<ProcessCapture> ExecuteCaptureAsync(
        RafterContext context,
        ProcessSpecification specification,
        ProcessOperationScope.Operation operation)
    {
        try
        {
            ProcessExecutionResult result = await ExecuteAsync(
                context,
                specification,
                operation,
                capture: true).ConfigureAwait(false);
            return new ProcessCapture(result.ExitCode, result.StandardOutput!, result.StandardError!);
        }
        finally
        {
            operation.Complete();
        }
    }

    private static async Task<ProcessExecutionResult> ExecuteAsync(
        RafterContext context,
        ProcessSpecification specification,
        ProcessOperationScope.Operation operation,
        bool capture)
    {
        PreparedProcess prepared = Prepare(context, specification, capture);
        context.CancellationToken.ThrowIfCancellationRequested();

        ProcessRuntimePolicy policy = Policy;
        using LifecycleArbiter arbiter = new(
            specification.Timeout,
            policy.TimeProvider,
            context.CancellationToken,
            operation.OwnershipToken);
        using ProcessOwnership ownership = new(CreateAdapter(prepared.StartInfo));
        ProcessExecutionResult result;
        try
        {
            Start(ownership.Process);
            StartedExecution execution = new(
                context,
                specification,
                operation,
                prepared,
                arbiter,
                policy,
                capture);
            result = await ExecuteStartedAsync(ownership, execution).ConfigureAwait(false);
            if (!specification.ValidExitCodes.Contains(result.ExitCode))
            {
                ProcessCapture? captured = capture
                    ? new ProcessCapture(result.ExitCode, result.StandardOutput!, result.StandardError!)
                    : null;
                throw new ProcessExitException(result.ExitCode, captured);
            }
        }
        catch (Exception exception)
        {
            ownership.Dispose();
            if (ownership.DisposalFailure is not null)
            {
                throw AppendDisposalFailure(exception, ownership.DisposalFailure);
            }
            if (exception is ProcessException or OperationCanceledException)
            {
                throw;
            }
            throw new ProcessException("The process runtime failed.", exception);
        }

        ownership.Dispose();
        if (ownership.DisposalFailure is not null)
        {
            throw new ProcessException("Process resource disposal failed.", ownership.DisposalFailure);
        }
        return result;
    }

    private static Exception AppendDisposalFailure(Exception primary, Exception disposal)
    {
        Exception? earlier = primary is ProcessException or OperationCanceledException ? primary.InnerException : primary;
        Exception detail = earlier is null ? disposal : new AggregateException(earlier, disposal);
        return primary switch
        {
            ProcessStartException => new ProcessStartException(detail),
            ProcessTimeoutException timeout => new ProcessTimeoutException(timeout.Timeout, detail),
            ProcessOutputException output => new ProcessOutputException(output.Reason, output.Stream, output.LimitBytes, detail),
            ProcessExitException exit => new ProcessExitException(exit.ExitCode, exit.Capture, detail),
            ProcessException => new ProcessException(primary.Message, detail),
            OperationCanceledException cancelled => new OperationCanceledException(
                cancelled.Message, detail, cancelled.CancellationToken),
            _ => new ProcessException("The process runtime failed.", detail),
        };
    }

    private static IProcessAdapter CreateAdapter(ProcessStartInfo startInfo)
    {
        try
        {
            return AdapterFactory.Create(startInfo);
        }
        catch (Exception exception)
        {
            throw new ProcessStartException(exception);
        }
    }

    private static void Start(IProcessAdapter process)
    {
        try
        {
            if (!process.Start())
            {
                throw new ProcessStartException(innerException: null);
            }
        }
        catch (ProcessStartException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ProcessStartException(exception);
        }
    }

    private static async Task<ProcessExecutionResult> ExecuteStartedAsync(
        ProcessOwnership ownership,
        StartedExecution execution)
    {
        using ProcessObservers observers = new();
        try
        {
            observers.Initialize(ownership.Process, execution);
            execution.Arbiter.ObserveExit(observers.Exit!);
        }
        catch (Exception initializationFailure)
        {
            throw await FailObservationAsync(ownership, observers, execution.Policy, initializationFailure)
                .ConfigureAwait(false);
        }

        LifecycleOutcome outcome = await execution.Arbiter.Completion.ConfigureAwait(false);
        if (outcome == LifecycleOutcome.NaturalExit)
        {
            try
            {
                await observers.Exit!.ConfigureAwait(false);
            }
            catch (Exception observationFailure)
            {
                throw await FailObservationAsync(ownership, observers, execution.Policy, observationFailure)
                    .ConfigureAwait(false);
            }

            return await CompleteNaturalAsync(
                ownership,
                observers.Exit!,
                observers.StandardOutput!,
                observers.StandardError!,
                observers.DrainCancellation,
                execution.Prepared,
                execution.Policy).ConfigureAwait(false);
        }

        Exception? teardown = await TerminateAsync(
            ownership,
            observers.Exit!,
            observers.StandardOutput!,
            observers.StandardError!,
            observers.DrainCancellation,
            execution.Policy).ConfigureAwait(false);
        ThrowLifecycleOutcome(
            outcome,
            execution.Context,
            execution.Specification,
            execution.Operation,
            teardown);
        throw new UnreachableException();
    }

    private static async Task<ProcessException> FailObservationAsync(
        ProcessOwnership ownership,
        ProcessObservers observers,
        ProcessRuntimePolicy policy,
        Exception observationFailure)
    {
        Task verification = observers.Exit is null || observers.Exit.IsFaulted || observers.Exit.IsCanceled
            ? ObserveDirectExitAsync(ownership.Process, policy.TimeProvider)
            : observers.Exit;
        Exception? teardownFailure = await TerminateAsync(
            ownership,
            verification,
            observers.StandardOutput ?? Task.CompletedTask,
            observers.StandardError ?? Task.CompletedTask,
            observers.DrainCancellation,
            policy).ConfigureAwait(false);
        return new ProcessException("The process exit or stream observers failed.",
            teardownFailure is null
                ? observationFailure
                : new AggregateException(observationFailure, teardownFailure));
    }

    private static Task<DrainResult> StartDrain(
        Stream stream,
        ProcessOutputStream outputStream,
        StartedExecution execution,
        CancellationToken cancellationToken)
        => DrainAsync(new DrainRequest(
            stream,
            outputStream,
            execution.Context,
            execution.Prepared.StreamingRedactor,
            execution.Prepared.CaptureLimitBytes,
            execution.Capture,
            cancellationToken));

    private static async Task ObserveDirectExitAsync(
        IProcessAdapter process,
        TimeProvider timeProvider)
    {
        // The normal exit observer may be the failed resource. Teardown still needs independent exit verification;
        // if it outlives the deadline, the reaper owns this task and the process until verification settles.
        while (!process.HasExited)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeProvider, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task VerifyExitAsync(IProcessAdapter process, Task exit, TimeProvider timeProvider)
    {
        try
        {
            await exit.ConfigureAwait(false);
        }
        catch
        {
            // A failed observer does not prove child exit. Keep verification owned even when cancellation won.
            await ObserveDirectExitAsync(process, timeProvider).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<ProcessExecutionResult> CompleteNaturalAsync(
        ProcessOwnership ownership,
        Task exit,
        Task<DrainResult> stdout,
        Task<DrainResult> stderr,
        CancellationTokenSource drainCancellation,
        PreparedProcess prepared,
        ProcessRuntimePolicy policy)
    {
        IProcessAdapter process = ownership.Process;
        await exit.ConfigureAwait(false);
        await CompleteNaturalDrainsAsync(
            ownership,
            stdout,
            stderr,
            drainCancellation,
            policy).ConfigureAwait(false);
        DrainResult stdoutResult = await stdout.ConfigureAwait(false);
        DrainResult stderrResult = await stderr.ConfigureAwait(false);
        ThrowOutputFailure(stdoutResult, stderrResult, prepared.CaptureLimitBytes);
        return new ProcessExecutionResult(
            process.ExitCode,
            stdoutResult.CapturedText,
            stderrResult.CapturedText);
    }

    private static void ThrowLifecycleOutcome(
        LifecycleOutcome outcome,
        RafterContext context,
        ProcessSpecification specification,
        ProcessOperationScope.Operation operation,
        Exception? teardown)
    {
        if (outcome == LifecycleOutcome.ExternalCancellation)
        {
            throw new OperationCanceledException("The process was cancelled.", teardown, context.CancellationToken);
        }

        if (outcome == LifecycleOutcome.OwnershipCancellation)
        {
            throw new OperationCanceledException(
                "The process operation was abandoned by its callback.",
                teardown,
                operation.OwnershipToken);
        }

        throw new ProcessTimeoutException(specification.Timeout!.Value, teardown);
    }

    private static PreparedProcess Prepare(
        RafterContext context,
        ProcessSpecification specification,
        bool capture)
    {
        List<ProcessDiagnostic> diagnostics = [.. specification.Diagnostics];
        ValidateTerminalMode(specification, capture, diagnostics);
        string workingDirectory = specification.WorkingDirectory ?? context.WorkingDirectory;
        string executable = NormalizeExecutable(specification.Executable.Text, workingDirectory, diagnostics);
        TextRedactor? streamingRedactor = CreateStreamingRedactor(context, specification, capture, diagnostics);
        ThrowDiagnostics(diagnostics);
        ProcessStartInfo startInfo = CreateStartInfo(executable, workingDirectory, specification);
        return new PreparedProcess(
            startInfo,
            capture ? specification.CaptureLimitBytes ?? DefaultCaptureLimitBytes : null,
            streamingRedactor);
    }

    private static void ValidateTerminalMode(
        ProcessSpecification specification,
        bool capture,
        List<ProcessDiagnostic> diagnostics)
    {
        if (!capture && specification.HasCaptureLimit)
        {
            diagnostics.Add(new ProcessDiagnostic(
                "RAFTER1512",
                "A capture limit can be used only with Capture().",
                long.MaxValue));
        }
    }

    private static string NormalizeExecutable(
        string executable,
        string workingDirectory,
        List<ProcessDiagnostic> diagnostics)
    {
        if (IsPathLike(executable) && !string.IsNullOrWhiteSpace(executable) && !executable.Contains('\0'))
        {
            try
            {
                executable = PathRuntime.PathPolicy.NormalizeAbsolute(executable, workingDirectory);
            }
            catch (PathRuntime.PathPolicyException)
            {
                diagnostics.Add(new ProcessDiagnostic(
                    "RAFTER1502",
                    "The process executable path is malformed or unsupported.",
                    Sequence: 0,
                    Order: 1));
            }
        }

        return executable;
    }

    private static TextRedactor? CreateStreamingRedactor(
        RafterContext context,
        ProcessSpecification specification,
        bool capture,
        List<ProcessDiagnostic> diagnostics)
    {
        TextRedactor? streamingRedactor = null;
        if (!capture)
        {
            ImmutableArray<string> localPatterns = GetSensitiveValues(specification);
            streamingRedactor = TextRedactor.Create([.. context.InvocationRedactor.Patterns, .. localPatterns]);
            if (!streamingRedactor.IsUsable)
            {
                diagnostics.Add(new ProcessDiagnostic(
                    "RAFTER1513",
                    "A safe process-output redaction marker could not be selected.",
                    long.MaxValue));
            }
        }

        return streamingRedactor;
    }

    private static void ThrowDiagnostics(IEnumerable<ProcessDiagnostic> diagnostics)
    {
        ProcessDiagnostic[] ordered = diagnostics
            .OrderBy(static diagnostic => diagnostic.Sequence)
            .ThenBy(static diagnostic => diagnostic.Order)
            .ToArray();
        if (ordered.Length == 0)
        {
            return;
        }

        string detail = string.Join(
            Environment.NewLine,
            ordered.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
        throw new ProcessException($"Process specification is invalid.{Environment.NewLine}{detail}");
    }

    private static ProcessStartInfo CreateStartInfo(
        string executable,
        string workingDirectory,
        ProcessSpecification specification)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (ProcessValue argument in specification.Arguments)
        {
            startInfo.ArgumentList.Add(argument.Text);
        }

        ApplyEnvironment(startInfo, specification.EnvironmentEdits);
        return startInfo;
    }

    private static async Task CompleteNaturalDrainsAsync(
        ProcessOwnership ownership,
        Task<DrainResult> stdout,
        Task<DrainResult> stderr,
        CancellationTokenSource drainCancellation,
        ProcessRuntimePolicy policy)
    {
        IProcessAdapter process = ownership.Process;
        Task both = Task.WhenAll(stdout, stderr);
        if (await CompletesWithinAsync(both, policy.DirectExitDrainCompletion, policy.TimeProvider)
            .ConfigureAwait(false))
        {
            await both.ConfigureAwait(false);
            return;
        }

        ProcessOutputStream retainedStream = GetIncompleteStreams(stdout, stderr);
        List<Exception> failures = [];
        CloseDrains(process, drainCancellation, failures);
        bool settled = await CompletesWithinAsync(both, policy.ForcedCloseDrainSettlement, policy.TimeProvider)
            .ConfigureAwait(false);
        if (!settled)
        {
            failures.Add(new IOException("Redirected process streams did not settle after closure."));
            ownership.Transfer(both);
        }
        else
        {
            try
            {
                await both.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        throw new ProcessOutputException(
            ProcessOutputReason.RetainedPipe,
            retainedStream,
            innerException: Combine(failures));
    }

    private static ProcessOutputStream GetIncompleteStreams(Task stdout, Task stderr)
        => (stdout.IsCompleted, stderr.IsCompleted) switch
        {
            (false, true) => ProcessOutputStream.StandardOutput,
            (true, false) => ProcessOutputStream.StandardError,
            _ => ProcessOutputStream.Both,
        };

    private static async Task<Exception?> TerminateAsync(
        ProcessOwnership ownership,
        Task exit,
        Task stdout,
        Task stderr,
        CancellationTokenSource drainCancellation,
        ProcessRuntimePolicy policy)
    {
        IProcessAdapter process = ownership.Process;
        List<Exception> failures = [];
        Task verifiedExit = VerifyExitAsync(process, exit, policy.TimeProvider);
        Task kill = Task.Factory.StartNew(
            () => Kill(process),
            CancellationToken.None,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
        await ObserveBoundedAsync(
            kill,
            policy.TreeKillRequest,
            policy.TimeProvider,
            "The process-tree termination request did not settle.",
            failures).ConfigureAwait(false);
        await ObserveBoundedAsync(
            verifiedExit,
            policy.ForcedKillVerification,
            policy.TimeProvider,
            "Direct-child termination could not be confirmed.",
            failures).ConfigureAwait(false);

        CloseDrains(process, drainCancellation, failures);
        Task drains = Task.WhenAll(stdout, stderr);
        await ObserveBoundedAsync(
            drains,
            policy.ForcedCloseDrainSettlement,
            policy.TimeProvider,
            "Redirected process streams did not settle after closure.",
            failures).ConfigureAwait(false);

        Task[] lateOperations = new[] { kill, verifiedExit, drains }.Where(static task => !task.IsCompleted).ToArray();
        if (lateOperations.Length != 0)
        {
            ownership.Transfer(Task.WhenAll(lateOperations));
        }

        return failures.Count switch
        {
            0 => null,
            1 => failures[0],
            _ => new AggregateException(failures),
        };
    }

    private static Exception? Combine(List<Exception> failures)
        => failures.Count switch
        {
            0 => null,
            1 => failures[0],
            _ => new AggregateException(failures),
        };

    private static void Kill(IProcessAdapter process)
    {
        try
        {
            process.KillTree();
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
        }
    }

    private static async Task ObserveBoundedAsync(
        Task task,
        TimeSpan timeout,
        TimeProvider timeProvider,
        string timeoutMessage,
        List<Exception> failures)
    {
        if (!await CompletesWithinAsync(task, timeout, timeProvider).ConfigureAwait(false))
        {
            failures.Add(new IOException(timeoutMessage));
            return;
        }

        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static async Task<DrainResult> DrainAsync(DrainRequest request)
    {
        using ProcessOutputLease? output = request.Capture
            ? null
            : new ProcessOutputLease(request.Context, request.OutputStream);
        using CancellationTokenRegistration cancellationRegistration = output is null
            ? default
            : request.CancellationToken.Register(static state => ((ProcessOutputLease)state!).Dispose(), output);
        DrainAccumulator accumulator = new(request, output);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            while (true)
            {
                int read;
                try
                {
                    read = await request.Stream.ReadAsync(
                        buffer.AsMemory(0, BufferSize),
                        request.CancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (request.CancellationToken.IsCancellationRequested
                    && exception is OperationCanceledException or IOException or ObjectDisposedException)
                {
                    break;
                }

                if (read == 0)
                {
                    break;
                }

                accumulator.Append(buffer.AsSpan(0, read));
            }

            return accumulator.Complete();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void ThrowOutputFailure(
        DrainResult stdout,
        DrainResult stderr,
        long? captureLimitBytes)
    {
        if (stdout.InvalidUtf8 || stderr.InvalidUtf8)
        {
            throw new ProcessOutputException(
                ProcessOutputReason.InvalidUtf8,
                SelectStream(stdout.InvalidUtf8, stderr.InvalidUtf8));
        }

        if (stdout.Overflow || stderr.Overflow)
        {
            throw new ProcessOutputException(
                ProcessOutputReason.CaptureLimitExceeded,
                SelectStream(stdout.Overflow, stderr.Overflow),
                captureLimitBytes);
        }
    }

    private static ProcessOutputStream SelectStream(bool stdout, bool stderr)
        => (stdout, stderr) switch
        {
            (true, true) => ProcessOutputStream.Both,
            (true, false) => ProcessOutputStream.StandardOutput,
            _ => ProcessOutputStream.StandardError,
        };

    private static void ApplyEnvironment(
        ProcessStartInfo startInfo,
        ImmutableArray<ProcessEnvironmentEdit> edits)
    {
        foreach (ProcessEnvironmentEdit edit in edits)
        {
            switch (edit.Kind)
            {
                case ProcessEnvironmentEditKind.Clear:
                    startInfo.Environment.Clear();
                    break;
                case ProcessEnvironmentEditKind.Set:
                    startInfo.Environment[edit.Name!] = edit.Value.Text;
                    break;
                case ProcessEnvironmentEditKind.Unset:
                    startInfo.Environment.Remove(edit.Name!);
                    break;
                default:
                    throw new UnreachableException();
            }
        }
    }

    private static ImmutableArray<string> GetSensitiveValues(ProcessSpecification specification)
        => specification.Arguments
            .Append(specification.Executable)
            .Concat(specification.EnvironmentEdits.Select(static edit => edit.Value))
            .Where(static value => value.Sensitive && !string.IsNullOrEmpty(value.Text))
            .Select(static value => value.Text)
            .ToImmutableArray();

    private static bool IsPathLike(string executable)
        => Path.IsPathRooted(executable)
            || executable.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || executable.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || executable.Contains('\\', StringComparison.Ordinal);

    private static async Task<bool> CompletesWithinAsync(Task task, TimeSpan timeout, TimeProvider timeProvider)
    {
        if (task.IsCompleted)
        {
            return true;
        }

        Task delay = Task.Delay(timeout, timeProvider, CancellationToken.None);
        return await Task.WhenAny(task, delay).ConfigureAwait(false) == task;
    }

    private static void CloseDrains(
        IProcessAdapter process,
        CancellationTokenSource cancellation,
        List<Exception> failures)
    {
        try
        {
            cancellation.Cancel();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            process.CloseOutput();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private sealed record PreparedProcess(
        ProcessStartInfo StartInfo,
        long? CaptureLimitBytes,
        TextRedactor? StreamingRedactor);

    private sealed record StartedExecution(
        RafterContext Context,
        ProcessSpecification Specification,
        ProcessOperationScope.Operation Operation,
        PreparedProcess Prepared,
        LifecycleArbiter Arbiter,
        ProcessRuntimePolicy Policy,
        bool Capture);

    private sealed record DrainRequest(
        Stream Stream,
        ProcessOutputStream OutputStream,
        RafterContext Context,
        TextRedactor? Redactor,
        long? CaptureLimitBytes,
        bool Capture,
        CancellationToken CancellationToken);

    private sealed record ProcessExecutionResult(int ExitCode, string? StandardOutput, string? StandardError);

    private sealed record DrainResult(
        ProcessOutputStream Stream,
        bool Overflow,
        bool InvalidUtf8,
        string? CapturedText);

    private sealed class ProcessObservers : IDisposable
    {
        internal CancellationTokenSource DrainCancellation { get; } = new();

        internal Task<DrainResult>? StandardOutput { get; private set; }

        internal Task<DrainResult>? StandardError { get; private set; }

        internal Task? Exit { get; private set; }

        public void Dispose() => DrainCancellation.Dispose();

        internal void Initialize(IProcessAdapter process, StartedExecution execution)
        {
            _ = process.Id;
            // Retain each task before acquiring the next resource, which may fail independently.
            StandardOutput = StartDrain(process.StandardOutput, ProcessOutputStream.StandardOutput,
                execution, DrainCancellation.Token);
            StandardError = StartDrain(process.StandardError, ProcessOutputStream.StandardError,
                execution, DrainCancellation.Token);
            Exit = process.WaitForExitAsync();
        }
    }

    private sealed class ProcessOwnership : IDisposable
    {
        private IProcessAdapter? _process;

        internal ProcessOwnership(IProcessAdapter process)
        {
            _process = process;
        }

        internal IProcessAdapter Process => _process
            ?? throw new InvalidOperationException("Process ownership has been transferred.");

        internal Exception? DisposalFailure { get; private set; }

        public void Dispose()
        {
            try
            {
                Interlocked.Exchange(ref _process, null)?.Dispose();
            }
            catch (Exception exception)
            {
                DisposalFailure = exception;
            }
        }

        internal void Transfer(Task completion)
        {
            IProcessAdapter process = Interlocked.Exchange(ref _process, null)
                ?? throw new InvalidOperationException("Process ownership was transferred more than once.");
            ProcessOperationReaper.Observe(completion, process);
        }

    }

    private sealed class DrainAccumulator
    {
        private readonly bool _capture;
        private readonly ProcessCaptureBuffer? _buffer;
        private readonly ProcessOutputLease? _output;
        private readonly ProcessOutputStream _outputStream;
        private readonly StreamingTextRedactor? _streaming;
        private readonly Utf8Validator _validator = new();
        private bool _invalidUtf8;
        private bool _overflow;

        internal DrainAccumulator(DrainRequest request, ProcessOutputLease? output)
        {
            _outputStream = request.OutputStream;
            _output = output;
            _buffer = request.Capture ? new ProcessCaptureBuffer(request.CaptureLimitBytes!.Value) : null;
            _capture = request.Capture;
            _streaming = request.Redactor is null ? null : new StreamingTextRedactor(request.Redactor);
        }

        internal void Append(ReadOnlySpan<byte> bytes)
        {
            if (_invalidUtf8)
            {
                return;
            }

            Retain(bytes);
            if (!_validator.Append(bytes, _streaming is null ? null : Publish))
            {
                _invalidUtf8 = true;
                _buffer?.Clear();
            }
        }

        internal DrainResult Complete()
        {
            if (!_invalidUtf8)
            {
                _invalidUtf8 = !_validator.Complete();
            }

            _streaming?.Complete((_, safe) => PublishSafe(safe));

            string? captured = _capture && !_overflow && !_invalidUtf8
                ? _buffer!.Materialize()
                : null;
            _buffer?.Clear();
            return new DrainResult(_outputStream, _overflow, _invalidUtf8, captured);
        }

        private void Retain(ReadOnlySpan<byte> bytes)
        {
            if (_buffer is not null && !_overflow && !_buffer.TryAppend(bytes))
            {
                _overflow = true;
            }
        }

        private void Publish(string text)
            => _streaming?.Append("process", text, (_, safe) => PublishSafe(safe));

        private void PublishSafe(string safe)
            => _output?.Publish(safe);
    }

    private sealed class ProcessOutputLease : IDisposable
    {
        private readonly bool _standardError;
        private readonly InvocationOutput _output;
        private readonly OutputScope? _scope;
        private readonly Lock _sync = new();
        private InvocationOutput.Admission? _admission;

        internal ProcessOutputLease(RafterContext context, ProcessOutputStream outputStream)
        {
            _output = context.InvocationOutput;
            _scope = context.OutputScope;
            _standardError = outputStream == ProcessOutputStream.StandardError;
            _admission = _output.Admit();
        }

        internal void Publish(string text)
        {
            lock (_sync)
            {
                if (_admission is not null)
                {
                    _output.PublishConsoleAdmitted(_standardError, _scope, text);
                }
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _admission?.Dispose();
                _admission = null;
            }
        }
    }

    private sealed class Utf8Validator
    {
        private readonly byte[] _pending = new byte[4];
        private int _pendingCount;

        internal bool Append(ReadOnlySpan<byte> bytes, Action<string>? emit)
        {
            StringBuilder? decoded = emit is null ? null : new StringBuilder(Math.Min(bytes.Length, 4096));
            Span<char> characters = stackalloc char[2];
            foreach (byte value in bytes)
            {
                _pending[_pendingCount++] = value;
                OperationStatus status = Rune.DecodeFromUtf8(
                    _pending.AsSpan(0, _pendingCount),
                    out Rune rune,
                    out _);
                if (status == OperationStatus.Done)
                {
                    int written = rune.EncodeToUtf16(characters);
                    decoded?.Append(characters[..written]);
                    _pendingCount = 0;
                    if (decoded?.Length >= 4096)
                    {
                        emit!(decoded.ToString());
                        decoded.Clear();
                    }
                }
                else if (status == OperationStatus.InvalidData || _pendingCount == _pending.Length)
                {
                    if (decoded?.Length > 0)
                    {
                        emit!(decoded.ToString());
                    }

                    return false;
                }
            }

            if (decoded?.Length > 0)
            {
                emit!(decoded.ToString());
            }

            return true;
        }

        internal bool Complete() => _pendingCount == 0;
    }

    private enum LifecycleOutcome
    {
        NaturalExit,
        ExternalCancellation,
        AuthoredTimeout,
        OwnershipCancellation,
    }

    private sealed class LifecycleArbiter : IDisposable
    {
        private readonly TaskCompletionSource<LifecycleOutcome> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenRegistration _externalRegistration;
        private readonly CancellationTokenRegistration _ownershipRegistration;
        private readonly ITimer? _timer;

        internal LifecycleArbiter(
            TimeSpan? timeout,
            TimeProvider timeProvider,
            CancellationToken externalCancellation,
            CancellationToken ownershipCancellation)
        {
            _externalRegistration = externalCancellation.Register(
                static state => ((LifecycleArbiter)state!).TrySet(LifecycleOutcome.ExternalCancellation),
                this);
            _ownershipRegistration = ownershipCancellation.Register(
                static state => ((LifecycleArbiter)state!).TrySet(LifecycleOutcome.OwnershipCancellation),
                this);
            if (timeout is not null)
            {
                _timer = timeProvider.CreateTimer(
                    static state => ((LifecycleArbiter)state!).TrySet(LifecycleOutcome.AuthoredTimeout),
                    this,
                    timeout.Value,
                    Timeout.InfiniteTimeSpan);
            }
        }

        internal Task<LifecycleOutcome> Completion => _completion.Task;

        internal void ObserveExit(Task exit)
        {
            _ = exit.ContinueWith(
                static (_, state) => ((LifecycleArbiter)state!).TrySet(LifecycleOutcome.NaturalExit),
                this,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public void Dispose()
        {
            _externalRegistration.Dispose();
            _ownershipRegistration.Dispose();
            _timer?.Dispose();
        }

        private void TrySet(LifecycleOutcome outcome) => _completion.TrySetResult(outcome);
    }
}
