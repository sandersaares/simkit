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
        var timer = RegisteredSynchronousTimer.GetInstance().Update(onElapsed, cancel);
        timer.NextTriggerOnOrAfter = _now + duration;

        lock (_synchronousTimersLock)
        {
            _synchronousTimers.Enqueue(timer, timer.NextTriggerOnOrAfter);

            _metrics.SynchronousTimersCurrent.Set(_synchronousTimers.Count);
            _metrics.SynchronousTimersTotal.Inc();
        }
    }

    public void Delay(TimeSpan duration, Action onElapsed, CancellationToken cancel)
    {
        // A delay with a callback is just a timer that only runs once.
        var timer = RegisteredSynchronousTimer.GetInstance().Update(onElapsed, cancel);
        timer.NextTriggerOnOrAfter = _now + duration;

        lock (_synchronousTimersLock)
        {
            _synchronousTimers.Enqueue(timer, timer.NextTriggerOnOrAfter);

            _metrics.SynchronousTimersCurrent.Set(_synchronousTimers.Count);
            _metrics.SynchronousTimersTotal.Inc();
        }
    }

    public void Delay(TimeSpan duration, Func<object, CancellationToken, ValueTask> onElapsed, object state, CancellationToken cancel)
    {
        // A delay with a callback is just a timer that only runs once.
        var timer = RegisteredSynchronousTimer.GetInstance().Update(onElapsed, state, cancel);
        timer.NextTriggerOnOrAfter = _now + duration;

        lock (_synchronousTimersLock)
        {
            _synchronousTimers.Enqueue(timer, timer.NextTriggerOnOrAfter);

            _metrics.SynchronousTimersCurrent.Set(_synchronousTimers.Count);
            _metrics.SynchronousTimersTotal.Inc();
        }
    }

    public void Delay(TimeSpan duration, Action<object> onElapsed, object state, CancellationToken cancel)
    {
        // A delay with a callback is just a timer that only runs once.
        var timer = RegisteredSynchronousTimer.GetInstance().Update(onElapsed, state, cancel);
        timer.NextTriggerOnOrAfter = _now + duration;

        lock (_synchronousTimersLock)
        {
            _synchronousTimers.Enqueue(timer, timer.NextTriggerOnOrAfter);

            _metrics.SynchronousTimersCurrent.Set(_synchronousTimers.Count);
            _metrics.SynchronousTimersTotal.Inc();
        }
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

    public void StartTimer(TimeSpan interval, Func<bool> onTick, CancellationToken cancel)
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

    public void StartTimer(TimeSpan interval, Func<object, CancellationToken, ValueTask<bool>> onTick, object state, CancellationToken cancel)
    {
        var timer = RegisteredSynchronousTimer.GetInstance().Update(interval, onTick, state, cancel);
        timer.NextTriggerOnOrAfter = _now + interval;

        lock (_synchronousTimersLock)
        {
            _synchronousTimers.Enqueue(timer, timer.NextTriggerOnOrAfter);

            _metrics.SynchronousTimersCurrent.Set(_synchronousTimers.Count);
            _metrics.SynchronousTimersTotal.Inc();
        }
    }

    public void StartTimer(TimeSpan interval, Func<object, bool> onTick, object state, CancellationToken cancel)
    {
        var timer = RegisteredSynchronousTimer.GetInstance().Update(interval, onTick, state, cancel);
        timer.NextTriggerOnOrAfter = _now + interval;

        lock (_synchronousTimersLock)
        {
            _synchronousTimers.Enqueue(timer, timer.NextTriggerOnOrAfter);

            _metrics.SynchronousTimersCurrent.Set(_synchronousTimers.Count);
            _metrics.SynchronousTimersTotal.Inc();
        }
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
        public CancellationToken Cancel { get; private set; }

        // One of these must be set, depending on which mode the user wishes to use.
        public Func<CancellationToken, ValueTask<bool>>? OnValueTaskTick { get; set; }
        public Func<bool>? OnRawTick { get; set; }
        public Func<CancellationToken, ValueTask>? OnSingleInvocationValueTaskTick { get; set; }
        public Action? OnSingleInvocationRawTick { get; set; }
        public Func<object, CancellationToken, ValueTask<bool>>? OnValueTaskTickWithState { get; set; }
        public Func<object, bool>? OnRawTickWithState { get; set; }
        public Func<object, CancellationToken, ValueTask>? OnSingleInvocationValueTaskTickWithState { get; set; }
        public Action<object>? OnSingleInvocationRawTickWithState { get; set; }

        // Provided to any tick handlers that accept a state argument.
        public object State { get; set; } = null!;

        public RegisteredSynchronousTimer Update(TimeSpan interval, Func<CancellationToken, ValueTask<bool>> onTick, CancellationToken cancel)
        {
            Update(interval, cancel);

            OnValueTaskTick = onTick;

            return this;
        }

        public RegisteredSynchronousTimer Update(TimeSpan interval, Func<bool> onTick, CancellationToken cancel)
        {
            Update(interval, cancel);

            OnRawTick = onTick;

            return this;
        }

        public RegisteredSynchronousTimer Update(TimeSpan interval, Func<object, CancellationToken, ValueTask<bool>> onTick, object state, CancellationToken cancel)
        {
            Update(interval, cancel);

            OnValueTaskTickWithState = onTick;
            State = state;

            return this;
        }

        public RegisteredSynchronousTimer Update(TimeSpan interval, Func<object, bool> onTick, object state, CancellationToken cancel)
        {
            Update(interval, cancel);

            OnRawTickWithState = onTick;
            State = state;

            return this;
        }

        // If we trigger a delay (a single invocation timer), we perform the next evaluation and cleanup after this much time elapses.
        // This must be nonzero so we do not get stuck into an infinite loop of trying to increment the timer but failing to do so.
        private static readonly TimeSpan DelayInterval = TimeSpan.FromSeconds(1);

        public RegisteredSynchronousTimer Update(Func<CancellationToken, ValueTask> onTick, CancellationToken cancel)
        {
            Update(DelayInterval, cancel);

            OnSingleInvocationValueTaskTick = onTick;

            return this;
        }

        public RegisteredSynchronousTimer Update(Action onTick, CancellationToken cancel)
        {
            Update(DelayInterval, cancel);

            OnSingleInvocationRawTick = onTick;

            return this;
        }

        public RegisteredSynchronousTimer Update(Func<object, CancellationToken, ValueTask> onTick, object state, CancellationToken cancel)
        {
            Update(DelayInterval, cancel);

            OnSingleInvocationValueTaskTickWithState = onTick;
            State = state;

            return this;
        }

        public RegisteredSynchronousTimer Update(Action<object> onTick, object state, CancellationToken cancel)
        {
            Update(DelayInterval, cancel);

            OnSingleInvocationRawTickWithState = onTick;
            State = state;

            return this;
        }

        private void Update(TimeSpan interval, CancellationToken cancel)
        {
            if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(interval), "Timer interval must be positive.");

            Interval = interval;
            Cancel = cancel;

            ResetReferences();

            // We expect the caller of Update() to set this appropriately.
            NextTriggerOnOrAfter = DateTimeOffset.MinValue;
            Remove = false;
        }

        public DateTimeOffset NextTriggerOnOrAfter { get; set; }

        // If the last callback returned "false", this is set and the logic will remove the timer the next time it is evaluated.
        // If the single invocation mode is used, this will automatically be set to true after the first callback.
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
            // Do not leave dangling references to objects that may be GCed.
            ResetReferences();

            Pool.Return(this);
        }

        private void ResetReferences()
        {
            OnValueTaskTick = null;
            OnRawTick = null;
            OnSingleInvocationValueTaskTick = null;
            OnSingleInvocationRawTick = null;
            OnValueTaskTickWithState = null;
            OnRawTickWithState = null;
            OnSingleInvocationValueTaskTickWithState = null;
            OnSingleInvocationRawTickWithState = null;
            State = null!;
        }
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

    // We reuse these buffers for performance.
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
                try
                {
                    await _asynchronousTimerCallbackTasks[i].WaitAsync(cancel);
                }
                catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
                {
                    // It is normal for timers to be cancelled via timer-specific OCE - we will just ignore it and clean up on next iteration.
                    // Note that we do not ignore the SimulatedTime CT here!
                }

                // Do not leave dangling references in buffer.
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

                bool keepTimer;

                try
                {
                    if (timer.OnValueTaskTick != null)
                    {
                        keepTimer = await timer.OnValueTaskTick(timer.Cancel);
                    }
                    else if (timer.OnRawTick != null)
                    {
                        keepTimer = timer.OnRawTick();
                    }
                    else if (timer.OnSingleInvocationValueTaskTick != null)
                    {
                        await timer.OnSingleInvocationValueTaskTick(timer.Cancel);
                        keepTimer = false;
                    }
                    else if (timer.OnSingleInvocationRawTick != null)
                    {
                        timer.OnSingleInvocationRawTick();
                        keepTimer = false;
                    }
                    else if (timer.OnValueTaskTickWithState != null)
                    {
                        keepTimer = await timer.OnValueTaskTickWithState(timer.State, timer.Cancel);
                    }
                    else if (timer.OnRawTickWithState != null)
                    {
                        keepTimer = timer.OnRawTickWithState(timer.State);
                    }
                    else if (timer.OnSingleInvocationValueTaskTickWithState != null)
                    {
                        await timer.OnSingleInvocationValueTaskTickWithState(timer.State, timer.Cancel);
                        keepTimer = false;
                    }
                    else if (timer.OnSingleInvocationRawTickWithState != null)
                    {
                        timer.OnSingleInvocationRawTickWithState(timer.State);
                        keepTimer = false;
                    }
                    else
                    {
                        throw new InvalidOperationException("Registered timer had no callback associated with it.");
                    }
                }
                catch (OperationCanceledException) when (timer.Cancel.IsCancellationRequested)
                {
                    // We do not propagate the exception because a timer getting caneled is a normal way to unsubscribe it.
                    keepTimer = false;
                }

                if (keepTimer)
                    continue;

                // Next evaluation will remove it.
                timer.Remove = true;
            }
            finally
            {
                // Do not leave dangling references in buffer.
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
