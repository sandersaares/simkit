namespace Simkit;

/// <summary>
/// Represents one simulation being executed by the simulator.
/// </summary>
/// <remarks>
/// Built-in services offered by every simulation:
/// * SimulationParameters
/// * ITime
/// * SimulatedTime/// * IMetricFactory
/// * ILoggerFactory
/// * ILogger of T
/// </remarks>
public interface ISimulation : IAsyncDisposable
{
    IServiceProvider Services { get; }

    /// <summary>
    /// Executes the simulation.
    /// </summary>
    Task ExecuteAsync(CancellationToken cancel);
}
