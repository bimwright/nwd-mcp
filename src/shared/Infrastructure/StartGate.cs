using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bimwright.Nwd.Shared.Infrastructure;

/// <summary>
/// Family contract FC-BUSY-01..04 for work queued to another thread (the Navisworks UI thread). The work runs only if
/// that thread starts it by a start deadline; otherwise it is abandoned and never runs later (HOST_BUSY). Once started
/// it is waited for until cancellation, and a cancellation then means the outcome is unknown (TIMEOUT).
/// Host-free, so the start/abandon race is unit-tested.
/// </summary>
public static class StartGate
{
    private const int Queued = 0, Started = 1, Abandoned = 2;

    /// <param name="post">Queues a callback on the target thread.</param>
    /// <returns>False when the work did not start by <paramref name="startByUtc"/> (or before cancellation) and will never run.</returns>
    /// <exception cref="OperationCanceledException">The work started and was still running at cancellation.</exception>
    public static bool Run(Action<Action> post, Action action, DateTime startByUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = Queued;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        post(() =>
        {
            // Exactly one of this callback and the waiter moves the state off Queued: it starts, or it is abandoned.
            if (Interlocked.CompareExchange(ref state, Started, Queued) != Queued) return;
            try
            {
                action();
                completion.TrySetResult(true);
            }
            catch (Exception ex) { completion.TrySetException(ex); }
        });

        try
        {
            var untilStart = startByUtc - DateTime.UtcNow;
            if (!completion.Task.Wait((int)Math.Min(int.MaxValue, Math.Max(0, untilStart.TotalMilliseconds)), cancellationToken))
            {
                if (Interlocked.CompareExchange(ref state, Abandoned, Queued) == Queued) return false;
                completion.Task.Wait(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            if (Interlocked.CompareExchange(ref state, Abandoned, Queued) == Queued) return false;
            throw;
        }
        catch (AggregateException ex) when (ex.InnerExceptions.Count == 1)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
        }
        return true;
    }
}
