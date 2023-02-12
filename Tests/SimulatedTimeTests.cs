using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prometheus;
using Simkit;

namespace Tests;

[TestClass]
public sealed class SimulatedTimeTests
{
    [TestMethod]
    public async Task Tick_IncrementsTime()
    {
        var time = CreateInstance();

        const int tickCount = 10;

        for (var tickIndex = 0; tickIndex < tickCount; tickIndex++)
        {
            await time.ProcessCurrentTickAsync(CancellationToken.None);
            time.MoveToNextTick();
        }

        var expectedEndTimestamp = Parameters.StartTime + TimeSpan.FromSeconds(tickCount * Parameters.TickDuration.TotalSeconds);
        Assert.AreEqual(expectedEndTimestamp, time.UtcNow);
    }

    [TestMethod]
    public async Task Delay_GetsReleasedOnTime()
    {
        var time = CreateInstance();

        const int delaysPerTick = 10_000;
        const int tickCount = 10;

        int delaysReleased = 0;

        for (var tickIndex = 0; tickIndex < tickCount; tickIndex++)
            for (var delayIndex = 0; delayIndex < delaysPerTick; delayIndex++)
            {
                var delayTask = time.Delay(TimeSpan.FromSeconds(Parameters.TickDuration.TotalSeconds * tickIndex), CancellationToken.None);

                // We could be more realistic here by using Task.Run() but that would be racy (continuations might be late due to task startup delay.
                // By just sticking a continuation directly on the end, we can guarantee synchronous execution, which helps us with test stability.
                // This also validates that it is possible (if everything is well behaved) for delays to complete synchronously, which may be useful for repeatable simulations.
                _ = delayTask.ContinueWith(_ =>
                {
                    Interlocked.Increment(ref delaysReleased);
                }, TaskContinuationOptions.ExecuteSynchronously);
            }

        for (var tickIndex = 0; tickIndex < tickCount; tickIndex++)
        {
            await time.ProcessCurrentTickAsync(CancellationToken.None);

            var expectedDelaysReleased = (tickIndex + 1) * delaysPerTick;
            Assert.AreEqual(expectedDelaysReleased, delaysReleased);

            time.MoveToNextTick();
        }
    }

    [TestMethod]
    public async Task Timer_GetsCalledOnTime()
    {
        var time = CreateInstance();

        const int tickCount = 10;
        const int triggerEveryNthTick = 2;
        var timerInterval = TimeSpan.FromSeconds(Parameters.TickDuration.TotalSeconds * triggerEveryNthTick);

        int timerTicksOccurred = 0;

        time.StartTimer(timerInterval, ct =>
        {
            Interlocked.Increment(ref timerTicksOccurred);
            return true;
        }, CancellationToken.None);

        for (var tickIndex = 0; tickIndex < tickCount; tickIndex++)
        {
            await time.ProcessCurrentTickAsync(CancellationToken.None);

            var expectedTimerTicksOccurred = tickIndex / triggerEveryNthTick;
            Assert.AreEqual(expectedTimerTicksOccurred, timerTicksOccurred);

            time.MoveToNextTick();
        }
    }

    [TestMethod]
    public async Task Timer_IsStoppedByReturnFalse()
    {
        var time = CreateInstance();

        const int tickCount = 10;
        const int triggerEveryNthTick = 2;
        const int maxTicksBeforeStop = 2;
        var timerInterval = TimeSpan.FromSeconds(Parameters.TickDuration.TotalSeconds * triggerEveryNthTick);

        int timerTicksOccurred = 0;

        time.StartTimer(timerInterval, ct =>
        {
            Interlocked.Increment(ref timerTicksOccurred);

            if (timerTicksOccurred == maxTicksBeforeStop)
                return false;

            return true;
        }, CancellationToken.None);

        for (var tickIndex = 0; tickIndex < tickCount; tickIndex++)
        {
            await time.ProcessCurrentTickAsync(CancellationToken.None);

            var expectedTimerTicksOccurred = Math.Min(maxTicksBeforeStop, tickIndex / triggerEveryNthTick);
            Assert.AreEqual(expectedTimerTicksOccurred, timerTicksOccurred);

            time.MoveToNextTick();
        }
    }

    [TestMethod]
    public async Task Timer_IsStoppedByCancel()
    {
        var time = CreateInstance();

        const int tickCount = 10;
        const int triggerEveryNthTick = 2;
        const int maxTicksBeforeStop = 2;
        var timerInterval = TimeSpan.FromSeconds(Parameters.TickDuration.TotalSeconds * triggerEveryNthTick);

        int timerTicksOccurred = 0;

        using var cts = new CancellationTokenSource();

        time.StartTimer(timerInterval, ct =>
        {
            Interlocked.Increment(ref timerTicksOccurred);

            if (timerTicksOccurred == maxTicksBeforeStop)
                cts.Cancel();

            return true;
        }, cts.Token);

        for (var tickIndex = 0; tickIndex < tickCount; tickIndex++)
        {
            await time.ProcessCurrentTickAsync(CancellationToken.None);

            var expectedTimerTicksOccurred = Math.Min(maxTicksBeforeStop, tickIndex / triggerEveryNthTick);
            Assert.AreEqual(expectedTimerTicksOccurred, timerTicksOccurred);

            time.MoveToNextTick();
        }
    }

    [TestMethod]
    public async Task Timer_SkipsOverlappingInvocations()
    {
        var time = CreateInstance();

        const int tickCount = 10;

        var timerInterval = TimeSpan.FromSeconds(Parameters.TickDuration.TotalSeconds * 0.1);

        int timerTicksOccurred = 0;

        time.StartTimer(timerInterval, ct =>
        {
            Interlocked.Increment(ref timerTicksOccurred);
            return true;
        }, CancellationToken.None);

        for (var tickIndex = 0; tickIndex < tickCount; tickIndex++)
        {
            await time.ProcessCurrentTickAsync(CancellationToken.None);

            var expectedTimerTicksOccurred = tickIndex;
            Assert.AreEqual(expectedTimerTicksOccurred, timerTicksOccurred);

            time.MoveToNextTick();
        }
    }

    private static SimulationParameters Parameters => new();

    private static SimulatedTime CreateInstance() => new SimulatedTime(Parameters, Metrics.DefaultFactory);
}
