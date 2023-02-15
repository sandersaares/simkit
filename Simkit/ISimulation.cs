namespace Simkit;

/// <summary>
/// Represents one simulation being executed by the simulator.
/// </summary>
/// <remarks>
/// Built-in services offered by every simulation:
/// * CancellationToken
/// * SimulationParameters
/// * ITime
/// * SimulatedTime
/// * IMetricFactory
/// * ILoggerFactory
/// * ILogger of T
/// </remarks>
public interface ISimulation : IAsyncDisposable
{
    IServiceProvider Services { get; }

    /// <summary>
    /// Executes the simulation.
    /// </summary>
    /// <remarks>
    /// For cancellation use the cancellation token passed to the root Simulator object.
    /// </remarks>
    Task ExecuteAsync();
}
