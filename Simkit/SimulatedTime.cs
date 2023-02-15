using Microsoft.Extensions.ObjectPool;
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
            _asynchronousTimers.Enqueue(timer, timer.NextTriggerOnOrAfter);

            _metrics.AsynchronousTimersCurrent.Set(_asynchronousTimers.Count);
            _metrics.AsynchronousTimersTotal.Inc();
        }
    }

    public void StartTimer(TimeSpan interval, Func<CancellationToken, ValueTask<bool>> onTick, CancellationToken cancel)
    {
        var timer = RegisteredSynchronousTimer.GetInstance().Update(interval, onTick, cancel);
        timer.NextTriggerOnOrAfter = _now + interval;

        lock (_synchronousTimersLock)
        {
            _synchronousTimers.Enqueue(timer, timer.NextTriggerOnOrAfter);

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

        // If the last callback returned "false", this is set and the logic will remove the timer the next time it is evaluated.
        internal bool Remove { get; set; }
    }

    private readonly PriorityQueue<RegisteredAsynchronousTimer, DateTimeOffset> _asynchronousTimers = new();
    private readonly object _asynchronousTimersLock = new();

    // A timer that is expected to complete synchronously (the ValueTask will probably be in a completed state immediately).
    // We still permit async execution but we use the expectation as an optimization opportunity.
    private sealed class RegisteredSynchronousTimer
    {
        public TimeSpan Interval { get; private set; }
        public Func<CancellationToken, ValueTask<bool>> OnTick { get; private set; } = DefaultOnTick;
        public CancellationToken Cancel { get; private set; }

        public RegisteredSynchronousTimer Update(TimeSpan interval, Func<CancellationToken, ValueTask<bool>> onTick, CancellationToken cancel)
        {
            Interval = interval;
            OnTick = onTick;
            Cancel = cancel;

            NextTriggerOnOrAfter = DateTimeOffset.MaxValue;
            Remove = false;

            return this;
        }

        public DateTimeOffset NextTriggerOnOrAfter { get; set; }

        // If the last callback returned "false", this is set and the logic will remove the timer the next time it is evaluated.
        public bool Remove { get; set; }

        private static readonly DefaultObjectPoolProvider PoolProvider = new()
        {
            // This is per-process, across all simulation runs. Could theoretically be a sizable bulk.
            MaximumRetained = 4096
        };

        private static readonly ObjectPool<RegisteredSynchronousTimer> Pool = PoolProvider.Create<RegisteredSynchronousTimer>();

        public static RegisteredSynchronousTimer GetInstance() => Pool.Get();
        public void ReturnToPool()
        {
            OnTick = DefaultOnTick;
            Pool.Return(this);
        }

        private static ValueTask<bool> DefaultOnTick(CancellationToken _) => throw new InvalidOperationException("Somehow the default tick handler got called. This code should be unreachable.");
    }

    private readonly PriorityQueue<RegisteredSynchronousTimer, DateTimeOffset> _synchronousTimers = new();
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

    // We reuse these buffers for performance. We do not use ArrayPool because it has size limits.
    private readonly Task[] _asynchronousTimerCallbackTasks = new Task[128];
    private readonly RegisteredSynchronousTimer[] _synchronousTimersToTrigger = new RegisteredSynchronousTimer[128];

    /// <summary>
    /// Performs any time processing for the current tick (e.g. releasing delays and triggering timers).
    /// </summary>
    /// <param name="cancel">May be signaled to give up on the simulation and consider it a failure.</param>
    internal async Task ProcessCurrentTickAsync(CancellationToken cancel)
    {
        TriggerDelays(cancel);

        while (TriggerAsynchronousTimersAndLoadCallbackTasks(out var loadedCallbackCount))
        {
            for (var i = 0; i < loadedCallbackCount; i++)
            {
                await _asynchronousTimerCallbackTasks[i].WaitAsync(cancel);

                // Do not leave dangling references to dead objects.
                _asynchronousTimerCallbackTasks[i] = Task.CompletedTask;
            }

            _metrics.TimerCallbacksTotal.Inc(loadedCallbackCount);
        }

        while (DetermineSynchronousTimersToTrigger(out var timersToTriggerCount))
            await TriggerSynchronousTimersAsync(timersToTriggerCount);

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

    /// <summary>
    /// Triggers asynchronous timers, storing the pending tasks in _asynchronousTimerCallbackTasks.
    /// We call this (and process the results) in a loop until it returns false.
    /// </summary>
    /// <returns>
    /// True if we loaded any callback tasks that need to be processed process.
    /// </returns>
    private bool TriggerAsynchronousTimersAndLoadCallbackTasks(out int loadedCallbackTaskCount)
    {
        loadedCallbackTaskCount = 0;

        lock (_asynchronousTimersLock)
        {
            while (true)
            {
                if (!_asynchronousTimers.TryPeek(out var timer, out var triggerOnOrAfter))
                    break; // No timers registered.

                // This timer needs to be triggered (or removed, if cancelled).
                if (timer.Cancel.IsCancellationRequested || timer.Remove)
                {
                    // This timer is dead, just remove it.
                    _asynchronousTimers.Dequeue();
                    _metrics.AsynchronousTimersCurrent.Set(_asynchronousTimers.Count);
                    continue;
                }

                if (_now < triggerOnOrAfter)
                    break; // The next timer is in the future.

                // First, schedule the next execution. We will skip some executions if too much time has passed.
                while (timer.NextTriggerOnOrAfter <= _now)
                    timer.NextTriggerOnOrAfter += timer.Interval;

                // Remove the timer from the beginning and enqueue it again with a new timestamp when it is next scheduled.
                // Logic above guarantees we will not trigger it twice, even if it is again the next one in the queue.
                _asynchronousTimers.EnqueueDequeue(timer, timer.NextTriggerOnOrAfter);

                // We must kick off the callback into a new task here to avoid deadlock because the timer callback could itself register a new timer.
                var timerCallbackTask = Task.Run(() => timer.OnTick(timer.Cancel).ContinueWith(t =>
                {
                    // Safe because we only execute this if we get a result.
                    if (t.Result == true)
                        return; // Keep ticking.

                    // Next evaluation will remove it.
                    timer.Remove = true;
                }, TaskContinuationOptions.RunContinuationsAsynchronously | TaskContinuationOptions.OnlyOnRanToCompletion));

                _asynchronousTimerCallbackTasks[loadedCallbackTaskCount++] = timerCallbackTask;

                if (loadedCallbackTaskCount == _asynchronousTimerCallbackTasks.Length)
                    break; // Buffer is full, cannot load any more callbacks. Next iteration will get the rest.
            }
        }

        return loadedCallbackTaskCount != 0;
    }

    private bool DetermineSynchronousTimersToTrigger(out int timersToTriggerCount)
    {
        timersToTriggerCount = 0;

        lock (_synchronousTimersLock)
        {
            while (true)
            {
                if (!_synchronousTimers.TryPeek(out var timer, out var triggerOnOrAfter))
                    break; // No timers registered.

                // This timer needs to be triggered (or removed, if cancelled).
                if (timer.Cancel.IsCancellationRequested || timer.Remove)
                {
                    // This timer is dead, just remove it.
                    _synchronousTimers.Dequeue();
                    timer.ReturnToPool();
                    _metrics.SynchronousTimersCurrent.Set(_synchronousTimers.Count);
                    continue;
                }

                if (timer.NextTriggerOnOrAfter > _now)
                    break; // The next timer is in the future.

                // This timer needs to be triggered.
                // First, schedule the next execution. We will skip some executions if too much time has passed.
                while (timer.NextTriggerOnOrAfter <= _now)
                    timer.NextTriggerOnOrAfter += timer.Interval;

                // Remove the timer from the beginning and enqueue it again with a new timestamp when it is next scheduled.
                // Logic above guarantees we will not trigger it twice, even if it is again the next one in the queue.
                _synchronousTimers.EnqueueDequeue(timer, timer.NextTriggerOnOrAfter);

                _synchronousTimersToTrigger[timersToTriggerCount++] = timer;

                if (timersToTriggerCount == _synchronousTimersToTrigger.Length)
                    break; // Buffer is full, cannot reference any more timers. Next iteration will get the rest.
            }
        }

        return timersToTriggerCount != 0;
    }

    private async ValueTask TriggerSynchronousTimersAsync(int timersToTriggerCount)
    {
        for (var i = 0; i < timersToTriggerCount; i++)
        {
            try
            {
                var timer = _synchronousTimersToTrigger[i];

                bool keepTimer = await timer.OnTick(timer.Cancel);

                if (keepTimer)
                    continue;

                // Next evaluation will remove it.
                timer.Remove = true;
            }
            finally
            {
                // Do not leave dangling references to dead objects.
                _synchronousTimersToTrigger[i] = null!;
            }
        }

        _metrics.TimerCallbacksTotal.Inc(timersToTriggerCount);
    }

    /// <summary>
    /// Moves the timeline to the next tick.
    /// </summary>
    internal void MoveToNextTick()
    {
        _now += _parameters.TickDuration;
    }
}
