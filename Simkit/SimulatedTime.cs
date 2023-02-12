using Prometheus;

namespace Simkit;

internal sealed class SimulatedTime : ITime
{
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

    public void StartTimer(TimeSpan interval, Func<CancellationToken, Task<bool>> onTick, CancellationToken cancel)
    {
        var timer = new RegisteredTimer(interval, onTick, cancel)
        {
            NextTriggerOnOrAfter = _now + interval
        };

        lock (_timersLock)
        {
            _timers.Add(timer);

            _metrics.TimersCurrent.Set(_timers.Count);
            _metrics.TimersTotal.Inc();
        }
    }

    public void StartTimer(TimeSpan interval, Func<CancellationToken, bool> onTick, CancellationToken cancel)
    {
        Task<bool> OnTickWrapper(CancellationToken cancel)
        {
            var result = onTick(cancel);
            return Task.FromResult(result);
        }

        var timer = new RegisteredTimer(interval, OnTickWrapper, cancel)
        {
            NextTriggerOnOrAfter = _now + interval
        };

        lock (_timersLock)
        {
            _timers.Add(timer);

            _metrics.TimersCurrent.Set(_timers.Count);
            _metrics.TimersTotal.Inc();
        }
    }

    private sealed record RegisteredTimer(
        TimeSpan Interval,
        Func<CancellationToken, Task<bool>> OnTick,
        CancellationToken Cancel)
    {
        internal DateTimeOffset NextTriggerOnOrAfter { get; set; }
    }

    private readonly List<RegisteredTimer> _timers = new();
    private readonly object _timersLock = new();

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
            // We cannot guarantee that it does but the closer we get, the less anomalies we produce if something needs to use delays.
            delay.OnCompleted.TrySetResult();
        }

        var timerCallbackTasks = new List<Task>();

        lock (_timersLock)
        {
            // If some timers have been cancelled, just remove them from the working set without further logic.
            _timers.RemoveAll(x => x.Cancel.IsCancellationRequested);

            _metrics.TimersCurrent.Set(_timers.Count);

            foreach (var timer in _timers)
            {
                if (timer.NextTriggerOnOrAfter > _now)
                    continue; // Not yet.

                // Schedule the next execution. We will skip some executions if too much time has passed.
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
                    lock (_timersLock)
                    {
                        _timers.Remove(timer);

                        _metrics.TimersCurrent.Set(_timers.Count);
                    }
                }, TaskContinuationOptions.RunContinuationsAsynchronously | TaskContinuationOptions.OnlyOnRanToCompletion));

                timerCallbackTasks.Add(timerCallbackTask);
            }
        }

        _metrics.TimerCallbacksTotal.Inc(timerCallbackTasks.Count);

        // If there was an unhandled exception in one of the timer tasks, we will re-throw here and the simulation will fail.
        // Pretty drastic but there is no very useful error handling strategy to apply here otherwise.
        await Task.WhenAll(timerCallbackTasks).WaitAsync(cancel);

        _metrics.ElapsedTicks.Inc();
        _metrics.ElapsedTime.IncTo((_now - _parameters.StartTime).TotalSeconds);
        _metrics.SimulatedTimestamp.SetToTimeUtc(_now);
    }

    /// <summary>
    /// Moves the timeline to the next tick.
    /// </summary>
    internal void MoveToNextTick()
    {
        _now += _parameters.TickDuration;
    }
}
