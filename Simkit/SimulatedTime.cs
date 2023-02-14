using System.Buffers;
using Prometheus;

namespace Simkit;

public sealed class SimulatedTime : ITime
{
    /// <summary>
    /// An event raised when every single simulation tick has elapsed (and which we wait to complete before the next tick starts).
    /// Raised after processing all callbacks for the same simulation tick.
    /// </summary>
    /// <remarks>
    /// The expectation is that this is fast and completes synchronously.
    /// There should not be any complex asynchronous logic happening for I/O simulation on every tick.
    /// </remarks>
    public event Action TickElapsed = delegate { };

    public DateTimeOffset UtcNow => _now;

    // In a simulation, precision will be low no matter what, so let's keep the numbers easy to debug.
    public long HighPrecisionTicksPerSecond => 1000;

    public long GetHighPrecisionTimestamp()
    {
        return (long)((_now - _parameters.StartTime).TotalSeconds * HighPrecisionTicksPerSecond);
    }

    public Task Delay(TimeSpan duration, CancellationToken cancel)
    {
        var tcs = new TaskCompletionSource();

        var cancelRegistration = cancel.Register(delegate
        {
            tcs.TrySetCanceled();
        });

        var delay = new RegisteredDelay(tcs);
        var triggerOnOrAfter = _now + duration;

        lock (_delaysLock)
        {
            _delays.Enqueue(delay, triggerOnOrAfter);

            _metrics.DelaysCurrent.Set(_delays.Count);
            _metrics.DelaysTotal.Inc();
        }

        return tcs.Task.ContinueWith(task =>
        {
            // Whether due to being canceled or due to the interval elapsing,
            // we have now completed the delay and no longer need the cancel registration.
            cancelRegistration.Dispose();

            return task;
        }, TaskContinuationOptions.ExecuteSynchronously).Unwrap();
    }

    private sealed record RegisteredDelay(TaskCompletionSource OnCompleted);

    private readonly PriorityQueue<RegisteredDelay, DateTimeOffset> _delays = new();
    private readonly object _delaysLock = new();

    public void Delay(TimeSpan duration, Func<CancellationToken, Task> onElapsed, CancellationToken cancel)
    {
        // A delay with a callback is just a timer that only runs once.
        async Task<bool> Wrapper(CancellationToken ct)
        {
            await onElapsed(ct);
            return false; // Only once.
        }

        StartTimer(duration, Wrapper, cancel);
    }

    public void Delay(TimeSpan duration, Func<CancellationToken, ValueTask> onElapsed, CancellationToken cancel)
    {
        // A delay with a callback is just a timer that only runs once.
        async ValueTask<bool> Wrapper(CancellationToken ct)
        {
            await onElapsed(ct);
            return false; // Only once.
        }

        StartTimer(duration, Wrapper, cancel);
    }

    public void Delay(TimeSpan duration, Action<CancellationToken> onElapsed, CancellationToken cancel)
    {
        ValueTask Wrapper(CancellationToken ct)
        {
            onElapsed(ct);
            return default;
        }

        Delay(duration, Wrapper, cancel);
    }

    public void StartTimer(TimeSpan interval, Func<CancellationToken, Task<bool>> onTick, CancellationToken cancel)
    {
        var timer = new RegisteredAsynchronousTimer(interval, onTick, cancel)
        {
            NextTriggerOnOrAfter = _now + interval
        };

        lock (_asynchronousTimersLock)
        {
            _asynchronousTimers.Add(timer);

            _metrics.AsynchronousTimersCurrent.Set(_asynchronousTimers.Count);
            _metrics.AsynchronousTimersTotal.Inc();
        }
    }

    public void StartTimer(TimeSpan interval, Func<CancellationToken, ValueTask<bool>> onTick, CancellationToken cancel)
    {
        var timer = new RegisteredSynchronousTimer(interval, onTick, cancel)
        {
            NextTriggerOnOrAfter = _now + interval
        };

        lock (_synchronousTimersLock)
        {
            _synchronousTimers.Add(timer);

            _metrics.SynchronousTimersCurrent.Set(_synchronousTimers.Count);
            _metrics.SynchronousTimersTotal.Inc();
        }
    }

    public void StartTimer(TimeSpan interval, Func<CancellationToken, bool> onTick, CancellationToken cancel)
    {
        ValueTask<bool> Wrapper(CancellationToken cancel)
        {
            var result = onTick(cancel);
            return new ValueTask<bool>(result);
        }

        StartTimer(interval, Wrapper, cancel);
    }

    // A timer that is expected to complete asynchronously (the Task will probably not be completed synchronously).
    private sealed record RegisteredAsynchronousTimer(
        TimeSpan Interval,
        Func<CancellationToken, Task<bool>> OnTick,
        CancellationToken Cancel)
    {
        internal DateTimeOffset NextTriggerOnOrAfter { get; set; }
    }

    private readonly List<RegisteredAsynchronousTimer> _asynchronousTimers = new();
    private readonly object _asynchronousTimersLock = new();

    // A timer that is expected to complete synchronously (the ValueTask will probably be in a completed state immediately).
    // We still permit async execution but we use the expectation as an optimization opportunity.
    private sealed record RegisteredSynchronousTimer(
        TimeSpan Interval,
        Func<CancellationToken, ValueTask<bool>> OnTick,
        CancellationToken Cancel)
    {
        internal DateTimeOffset NextTriggerOnOrAfter { get; set; }
    }

    private readonly List<RegisteredSynchronousTimer> _synchronousTimers = new();
    private readonly object _synchronousTimersLock = new();

    internal SimulatedTime(
        SimulationParameters parameters,
        IMetricFactory metricFactory)
    {
        _parameters = parameters;

        _metrics = new SimulatedTimeMetrics(metricFactory);

        _now = parameters.StartTime;
    }

    private readonly SimulationParameters _parameters;
    private readonly SimulatedTimeMetrics _metrics;

    private DateTimeOffset _now;

    /// <summary>
    /// Performs any time processing for the current tick (e.g. releasing delays and triggering timers).
    /// </summary>
    /// <param name="cancel">May be signaled to give up on the simulation and consider it a failure.</param>
    internal async Task ProcessCurrentTickAsync(CancellationToken cancel)
    {
        TriggerDelays(cancel);

        Task[] asynchronousTimerCallbackTasks;
        // How many items in the above buffer are actually used.
        int asynchronousTimerCallbackCount = 0;

        asynchronousTimerCallbackTasks = TriggerAsynchronousTimers(ref asynchronousTimerCallbackCount);

        _metrics.TimerCallbacksTotal.Inc(asynchronousTimerCallbackCount);

        // Now process all the synchronous timers. Note that we still need to be careful about reentrancy - the timer callbacks could try register new timers!
        RegisteredSynchronousTimer[] synchronousTimersToTrigger;
        // How many items in the above buffer are actually used.
        int synchronousTimersToTriggerCount = 0;

        synchronousTimersToTrigger = DetermineSynchronousTimersToTrigger(ref synchronousTimersToTriggerCount);

        // Process all the synchronous timer callbacks.
        await TriggerSynchronousTimers(synchronousTimersToTrigger, synchronousTimersToTriggerCount);

        // Gather all the results from the asynchronous timer callbacks.
        // If there was an unhandled exception in one of the timer tasks, we will re-throw here and the simulation will fail.
        // Pretty drastic but there is no very useful error handling strategy to apply here otherwise.
        await Task.WhenAll(asynchronousTimerCallbackTasks.Take(asynchronousTimerCallbackCount)).WaitAsync(cancel);

        ArrayPool<Task>.Shared.Return(asynchronousTimerCallbackTasks);

        // Call any registered per-tick callback.
        // This is often where simulated inputs/outputs perform their updates (the timers and delays are more meant for code under test).
        TickElapsed();

        _metrics.ElapsedTicks.Inc();
        _metrics.ElapsedTime.IncTo((_now - _parameters.StartTime).TotalSeconds);
        _metrics.SimulatedTimestamp.SetToTimeUtc(_now);
    }

    private void TriggerDelays(CancellationToken cancel)
    {
        while (true)
        {
            cancel.ThrowIfCancellationRequested();

            RegisteredDelay delay;

            lock (_delaysLock)
            {
                if (!_delays.TryPeek(out _, out var triggerOnOrAfter))
                    break; // No delays registered.

                if (_now < triggerOnOrAfter)
                    break; // Next delay is in the future.

                delay = _delays.Dequeue();
                _metrics.DelaysCurrent.Set(_delays.Count);
            }

            // We will run the TCS continuations synchronously (the default behavior) to maximize the chances of any delayed logic happening inline.
            // We cannot guarantee that it does but the closer we get, the less anomalies we produce if something needs to use Task-based delays.
            // Ideally, our logic would use callback-based delays instead, which we can guarantee runs inline with the tick processing.
            delay.OnCompleted.TrySetResult();
        }
    }

    private Task[] TriggerAsynchronousTimers(ref int asynchronousTimerCallbackCount)
    {
        Task[] asynchronousTimerCallbackTasks;
        lock (_asynchronousTimersLock)
        {
            // If some timers have been cancelled, just remove them from the working set without further logic.
            _asynchronousTimers.RemoveAll(x => x.Cancel.IsCancellationRequested);

            asynchronousTimerCallbackTasks = ArrayPool<Task>.Shared.Rent(_asynchronousTimers.Count);

            _metrics.AsynchronousTimersCurrent.Set(_asynchronousTimers.Count);

            foreach (var timer in _asynchronousTimers)
            {
                if (timer.NextTriggerOnOrAfter > _now)
                    continue; // Not yet.

                // This timer needs to be triggered.
                // First, schedule the next execution. We will skip some executions if too much time has passed.
                while (timer.NextTriggerOnOrAfter <= _now)
                    timer.NextTriggerOnOrAfter += timer.Interval;

                // We must kick off the callback into a new task here to avoid deadlock because the timer callback could itself register a new timer.
                var timerCallbackTask = Task.Run(() => timer.OnTick(timer.Cancel).ContinueWith(t =>
                {
                    // Safe because we only execute this if we get a result.
                    if (t.Result == true)
                        return; // Keep ticking.

                    // Should no longer keep ticking. Remove the timer.
                    // This is deadlock safe because we run asynchronously.
                    lock (_asynchronousTimersLock)
                    {
                        _asynchronousTimers.Remove(timer);

                        _metrics.AsynchronousTimersCurrent.Set(_asynchronousTimers.Count);
                    }
                }, TaskContinuationOptions.RunContinuationsAsynchronously | TaskContinuationOptions.OnlyOnRanToCompletion));

                asynchronousTimerCallbackTasks[asynchronousTimerCallbackCount++] = timerCallbackTask;
            }
        }

        return asynchronousTimerCallbackTasks;
    }

    private RegisteredSynchronousTimer[] DetermineSynchronousTimersToTrigger(ref int synchronousTimersToTriggerCount)
    {
        RegisteredSynchronousTimer[] synchronousTimersToTrigger;
        lock (_synchronousTimersLock)
        {
            // If some timers have been cancelled, just remove them from the working set without further logic.
            _synchronousTimers.RemoveAll(x => x.Cancel.IsCancellationRequested);

            synchronousTimersToTrigger = ArrayPool<RegisteredSynchronousTimer>.Shared.Rent(_synchronousTimers.Count);

            _metrics.SynchronousTimersCurrent.Set(_synchronousTimers.Count);

            foreach (var timer in _synchronousTimers)
            {
                if (timer.NextTriggerOnOrAfter > _now)
                    continue; // Not yet.

                // This timer needs to be triggered.
                // First, schedule the next execution. We will skip some executions if too much time has passed.
                while (timer.NextTriggerOnOrAfter <= _now)
                    timer.NextTriggerOnOrAfter += timer.Interval;

                synchronousTimersToTrigger[synchronousTimersToTriggerCount++] = timer;
            }
        }

        return synchronousTimersToTrigger;
    }

    private async Task TriggerSynchronousTimers(RegisteredSynchronousTimer[] synchronousTimersToTrigger, int synchronousTimersToTriggerCount)
    {
        for (var i = 0; i < synchronousTimersToTriggerCount; i++)
        {
            var timer = synchronousTimersToTrigger[i];

            bool keepTimer = await timer.OnTick(timer.Cancel);

            if (keepTimer)
                continue;

            // Should no longer keep ticking. Remove the timer.
            // This is deadlock safe because we run asynchronously.
            lock (_synchronousTimersLock)
            {
                _synchronousTimers.Remove(timer);

                _metrics.SynchronousTimersCurrent.Set(_synchronousTimers.Count);
            }
        }

        _metrics.TimerCallbacksTotal.Inc(synchronousTimersToTriggerCount);
    }

    /// <summary>
    /// Moves the timeline to the next tick.
    /// </summary>
    internal void MoveToNextTick()
    {
        _now += _parameters.TickDuration;
    }
}
