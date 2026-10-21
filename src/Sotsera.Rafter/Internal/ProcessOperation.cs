using System.Diagnostics;

namespace Sotsera.Rafter.Internal;

internal static class ProcessOperation
{
    public static async Task<ProcessResult> RunAsync(ProcessSettings settings, BodyProcesses body)
    {
        var invocation = body.Invocation;
        var cancellation = invocation.Cancellation;
        cancellation.Token.ThrowIfCancellationRequested();
        if (body.Shutdown.Started)
        {
            throw new OperationCanceledException("The owning body has finished.");
        }
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Owned process execution is currently implemented on Windows only.");
        }
        invocation.Output.ProcessStarted(settings.Executable, settings.Directory);
        cancellation.Token.ThrowIfCancellationRequested();
        if (body.Shutdown.Started)
        {
            throw new OperationCanceledException("The owning body has finished.");
        }
        WindowsProcess process;
        try
        {
            process = WindowsProcess.Start(settings.Executable, settings.Arguments, settings.Directory, settings.Environment);
        }
        catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
        {
            throw new ProcessLaunchException(settings.Executable, settings.Directory, exception);
        }
        return await CompleteAsync(settings, body, process).ConfigureAwait(false);
    }

    internal static async Task<ProcessResult> CompleteAsync(ProcessSettings settings, BodyProcesses body, IOwnedProcess process)
    {
        var invocation = body.Invocation;
        var cancellation = invocation.Cancellation;
        using (process)
        using (var readerCancellation = new CancellationTokenSource())
        {
            // A caller-supplied TextWriter can block synchronously. Keep the lifecycle supervisor independent
            // of both pumps, then join them even if a drain deadline expires while that writer is still running.
            var outputReader = process.Output;
            var errorReader = process.Error;
            var stdout = Task.Run(() => PumpAsync(outputReader, text => invocation.Output.ProcessOutput(text, false),
                readerCancellation.Token));
            var stderr = Task.Run(() => PumpAsync(errorReader, text => invocation.Output.ProcessOutput(text, true),
                readerCancellation.Token));
            var readers = Task.WhenAll(stdout, stderr);
            var localShutdown = new ShutdownWindow();
            var graceDeadline = long.MaxValue;
            var verificationDeadline = long.MaxValue;
            var signalled = false;
            var forced = false;
            var empty = false;
            int? exitCode = null;
            Exception? nativeFailure = null;
            Exception? outputFailure = null;
            try
            {
                while (true)
                {
                    try
                    {
                        exitCode ??= process.ExitCode;
                    }
                    catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                    {
                        nativeFailure = exception;
                        localShutdown.Request(force: true);
                    }
                    try
                    {
                        empty = process.IsEmpty;
                    }
                    catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                    {
                        nativeFailure = exception;
                        localShutdown.Request(force: true);
                    }
                    if (exitCode.HasValue)
                    {
                        localShutdown.Request();
                    }
                    if (stdout.IsFaulted || stderr.IsFaulted)
                    {
                        localShutdown.Request(force: true);
                    }
                    var windows = new[] { cancellation.Processes, body.Shutdown, localShutdown };
                    var active = windows.Where(window => window.Started).ToArray();
                    if (active.Length > 0)
                    {
                        graceDeadline = Math.Min(graceDeadline, active.Min(window => window.GraceDeadline));
                        if (!signalled)
                        {
                            signalled = true;
                            var graceful = false;
                            try
                            {
                                graceful = empty || process.RequestGracefulStop();
                            }
                            catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                            {
                                nativeFailure = exception;
                            }
                            if (!graceful)
                            {
                                foreach (var window in active)
                                {
                                    window.Request(force: true);
                                }
                                graceDeadline = Stopwatch.GetTimestamp();
                            }
                        }
                        if (empty || Stopwatch.GetTimestamp() >= graceDeadline)
                        {
                            var verificationWindows = empty ? new[] { localShutdown } : active;
                            foreach (var window in verificationWindows)
                            {
                                window.Request(force: !empty);
                                window.BeginVerification();
                            }
                            if (!empty && !forced)
                            {
                                forced = true;
                                try
                                {
                                    process.ForceStop();
                                }
                                catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                                {
                                    nativeFailure = exception;
                                }
                            }
                        }
                        verificationDeadline = Math.Min(verificationDeadline,
                            active.Min(window => window.VerificationDeadline));
                    }
                    if (empty && readers.IsCompleted && exitCode.HasValue)
                    {
                        break;
                    }
                    if (Stopwatch.GetTimestamp() >= verificationDeadline)
                    {
                        break;
                    }
                    await Task.Delay(20).ConfigureAwait(false);
                }
            }
            finally
            {
                // Cancellation interrupts overlapped pipe reads. It does not prove that a pump or its writer
                // has returned, so always await both before releasing readers or allowing final output flush.
                var incomplete = !readers.IsCompleted;
                if (incomplete)
                {
                    await readerCancellation.CancelAsync().ConfigureAwait(false);
                }
                try
                {
                    await readers.ConfigureAwait(false);
                }
                catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                {
                    outputFailure = exception;
                }
                if (!empty || incomplete)
                {
                    var failure = new ProcessCompletionException(settings.Executable, exitCode, !empty,
                        nativeFailure ?? outputFailure);
                    invocation.Fail(failure);
                    throw failure;
                }
            }
            if (outputFailure is not null)
            {
                var failure = new ProcessCompletionException(settings.Executable, exitCode, false, outputFailure);
                invocation.Fail(failure);
                throw failure;
            }
            if (!exitCode.HasValue)
            {
                throw new InvalidOperationException($"Process exit code unavailable | {settings.Executable}", nativeFailure);
            }
            cancellation.Token.ThrowIfCancellationRequested();
            if (body.Shutdown.Started)
            {
                throw new OperationCanceledException("The owning body has finished.");
            }
            var result = new ProcessResult(exitCode.Value, settings.AcceptedExitCodes.Contains(exitCode.Value));
            if (result.Succeeded)
            {
                return result;
            }
            var rejected = new ProcessFailureException(settings.Executable, settings.AcceptedExitCodes, result);
            var action = ProcessFailureAction.Throw;
            if (settings.FailureHandler is not null)
            {
                try
                {
                    action = settings.FailureHandler(result);
                    if (!Enum.IsDefined(action))
                    {
                        throw new InvalidOperationException("The process failure handler returned an unknown action.");
                    }
                }
                catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
                {
                    throw new ProcessHandlerException(rejected, exception);
                }
            }
            if (action == ProcessFailureAction.Throw)
            {
                throw rejected;
            }
            if (action == ProcessFailureAction.Fail)
            {
                invocation.Fail();
            }
            invocation.Output.ProcessRejected(rejected, action);
            cancellation.Token.ThrowIfCancellationRequested();
            if (body.Shutdown.Started)
            {
                throw new OperationCanceledException("The owning body has finished.");
            }
            return result;
        }
    }

    private static async Task PumpAsync(StreamReader reader, Action<string> write, CancellationToken token)
    {
        var buffer = new char[4096];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
            if (count == 0)
            {
                return;
            }
            write(new string(buffer, 0, count));
        }
    }
}
