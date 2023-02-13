using Microsoft.Extensions.Logging;
using Prometheus;

namespace Simkit;

/// <summary>
/// Represents one simulation being executed by the simulator.
/// </summary>
public interface ISimulation : IAsyncDisposable
{
    ITime Time { get; }
    IMetricFactory MetricFactory { get; }
    ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Configures a callback to be called for each simulation tick.
    /// 
    /// Simulator timing logic increments to the new tick (potentially releasing parallel tasks/threads)
    /// and waits for all timer callbacks to complete before this callback is called for the tick.
    /// </summary>
    /// <remarks>
    /// As long as all logic under test uses timer callbacks and not its own homebrew timing logic,
    /// you can assume that tick processing has completed by the time this callback is called.
    /// </remarks>
    void OnTick(Func<CancellationToken, Task> onTick);

    /// <summary>
    /// Executes one simulation.
    /// </summary>
    Task ExecuteAsync(CancellationToken cancel);
}
