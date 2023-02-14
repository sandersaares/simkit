using System.Diagnostics;

namespace Simkit;

public sealed class RealTime : ITime
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public long GetHighPrecisionTimestamp() => Stopwatch.GetTimestamp();
    public long HighPrecisionTicksPerSecond => Stopwatch.Frequency;

    public Task Delay(TimeSpan duration, CancellationToken cancel) => Task.Delay(duration, cancel);
    public void Delay(TimeSpan duration, Func<CancellationToken, Task> onElapsed, CancellationToken cancel) => _ = Task.Run(async delegate
    {
        await Task.Delay(duration, cancel);
        await onElapsed(cancel);
    });
    public void Delay(TimeSpan duration, Func<CancellationToken, ValueTask> onElapsed, CancellationToken cancel) => _ = Task.Run(async delegate
    {
        await Task.Delay(duration, cancel);
        await onElapsed(cancel);
    });
    public void Delay(TimeSpan duration, Action<CancellationToken> onElapsed, CancellationToken cancel) => _ = Task.Run(async delegate
    {
        await Task.Delay(duration, cancel);
        onElapsed(cancel);
    });

    public void StartTimer(TimeSpan interval, Func<CancellationToken, Task<bool>> onTick, CancellationToken cancel)
    {
        _ = Task.Run(async delegate
        {
            using var timer = new PeriodicTimer(interval);
            
            while (await timer.WaitForNextTickAsync(cancel))
            {
                try
                {
                    if (!await onTick(cancel))
                        break;
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                {
                    // Normal timer cancellation - nothing to get worked up about.
                    // Ideally, the callback would exit without throwing, of course.
                    break;
                }
            }
        });
    }

    public void StartTimer(TimeSpan interval, Func<CancellationToken, ValueTask<bool>> onTick, CancellationToken cancel)
    {
        _ = Task.Run(async delegate
        {
            using var timer = new PeriodicTimer(interval);

            while (await timer.WaitForNextTickAsync(cancel))
            {
                try
                {
                    if (!await onTick(cancel))
                        break;
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                {
                    // Normal timer cancellation - nothing to get worked up about.
                    // Ideally, the callback would exit without throwing, of course.
                    break;
                }
            }
        });
    }

    public void StartTimer(TimeSpan interval, Func<CancellationToken, bool> onTick, CancellationToken cancel)
    {
        _ = Task.Run(async delegate
        {
            using var timer = new PeriodicTimer(interval);

            while (await timer.WaitForNextTickAsync(cancel))
            {
                try
                {
                    if (!onTick(cancel))
                        break;
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                {
                    // Normal timer cancellation - nothing to get worked up about.
                    // Ideally, the callback would exit without throwing, of course.
                    break;
                }
            }
        });
    }
}
