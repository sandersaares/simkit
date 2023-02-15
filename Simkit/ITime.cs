namespace Simkit;

/// <summary>
/// Time is at the heart of any simulation. All time-related logic in code that can run under a simulation should be using the members of this interface.
/// Any usage of standard library time functionality may cause deviations from the simulated timeline and/or slow down the simulation to real time.
/// </summary>
/// <remarks>
/// Thread-safe.
/// </remarks>
public interface ITime
{
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Periodically calls an asynchronous callback from an unspecified thread.
    /// 
    /// Will not execute multiple times if multiple intervals of time pass.
    /// 
    /// There are two ways to stop the timer - either signal cancellation via the token (which is passed through to callback)
    /// or return false from the callback. Both will result in no further timer executions being performed.
    /// 
    /// If running in a simulation, the simulation will pause until the tick handler returns.
    /// </summary>
    void StartTimer(TimeSpan interval, Func<CancellationToken, Task<bool>> onTick, CancellationToken cancel);

    /// <summary>
    /// Periodically calls an asynchronous callback from an unspecified thread.
    /// 
    /// Will not execute multiple times if multiple intervals of time pass.
    /// 
    /// There are two ways to stop the timer - either signal cancellation via the token (which is passed through to callback)
    /// or return false from the callback. Both will result in no further timer executions being performed.
    /// 
    /// If running in a simulation, the simulation will pause until the tick handler returns.
    /// </summary>
    void StartTimer(TimeSpan interval, Func<CancellationToken, ValueTask<bool>> onTick, CancellationToken cancel);

    /// <summary>
    /// Periodically calls a synchronous callback from an unspecified thread.
    /// 
    /// Will not execute multiple times if multiple intervals of time pass.
    /// 
    /// There are two ways to stop the timer - either signal cancellation via the token (which is passed through to callback)
    /// or return false from the callback. Both will result in no further timer executions being performed.
    /// 
    /// If running in a simulation, the simulation will pause until the tick handler returns.
    /// </summary>
    void StartTimer(TimeSpan interval, Func<bool> onTick, CancellationToken cancel);

    /// <summary>
    /// Avoid using this as a scheduling mechanism because when running in a simulation,
    /// the simulation does not know when the code triggered by the delay finishes executing.
    /// 
    /// That may cause the simulation to continue to the next tick too early (before all triggered code has finished).
    /// Prefer using Delay(callback) or StartTimer() instead, in which case the simulation will wait until the callback has completed.
    /// </summary>
    Task Delay(TimeSpan duration, CancellationToken cancel);

    /// <summary>
    /// Calls a callback (once) when a delay elapses.
    /// 
    /// If running in a simulation, the simulation will pause until the callback returns.
    /// </summary>
    void Delay(TimeSpan duration, Func<CancellationToken, Task> onElapsed, CancellationToken cancel);

    /// <summary>
    /// Calls a callback (once) when a delay elapses.
    /// 
    /// If running in a simulation, the simulation will pause until the callback returns.
    /// </summary>
    void Delay(TimeSpan duration, Func<CancellationToken, ValueTask> onElapsed, CancellationToken cancel);

    /// <summary>
    /// Calls a callback (once) when a delay elapses.
    /// 
    /// If running in a simulation, the simulation will pause until the callback returns.
    /// </summary>
    void Delay(TimeSpan duration, Action onElapsed, CancellationToken cancel);

    long GetHighPrecisionTimestamp();

    long HighPrecisionTicksPerSecond { get; }
}
