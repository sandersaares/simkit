using Prometheus;

namespace Simkit;

/// <summary>
/// Executes simulations and processes their results for easy human understanding.
/// </summary>
public sealed class Simulator
{
    public SimulationParameters Parameters { get; }

    public Simulator(SimulationParameters parameters)
    {
        Parameters = parameters;
    }

    /// <summary>
    /// Configures the callback called when the simulator wants to execute the code under test.
    /// This will be called one or more times by the simulator, depending on its parameters.
    /// </summary>
    /// <remarks>
    /// When the callback is called, you need to:
    /// 1) Initialize and wire up any code under test.
    /// 2) Call ISimulation.Execute().
    /// 3) Log/measure/evaluate any results.
    /// 
    /// If an exception is thrown from the callback, the simulation is aborted.
    /// </remarks>
    public void OnExecute(Func<ISimulation, CancellationToken, Task> onExecute)
    {
        _onExecute = onExecute;
    }

    private Func<ISimulation, CancellationToken, Task> _onExecute = (_, _) => throw new InvalidOperationException($"You must call {nameof(OnExecute)} before starting the simulation.");

    /// <summary>
    /// Executes the configured number of iterations of the simulation.
    /// Telemetry from each iteration is written to persistent storage for later manual analysis.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancel)
    {
        for (var runIndex = 0; runIndex < Parameters.ExecutionCount; runIndex++)
        {
            var runMetricsRegistry = Metrics.NewCustomRegistry();
            var runMetricsFactory = Metrics.WithCustomRegistry(runMetricsRegistry);

            var simulation = new Simulation(Parameters, runMetricsFactory);
            await _onExecute(simulation, cancel);
        }
    }

    private sealed class Simulation : ISimulation
    {
        public ITime Time => _time;
        public IMetricFactory MetricFactory { get; }

        public async Task ExecuteAsync(CancellationToken cancel)
        {
            var tickCount = _parameters.TicksPerSimulation;

            for (var tickIndex = 0; tickIndex < tickCount; tickIndex++)
            {
                await _time.ProcessCurrentTickAsync(cancel);

                await _onTick(cancel);

                _time.MoveToNextTick();
            }
        }

        public void OnTick(Func<CancellationToken, Task> onTick)
        {
            _onTick = onTick;
        }

        // Technically one could have a simulation with no per-tick callback, that is fine.
        private Func<CancellationToken, Task> _onTick = _ => Task.CompletedTask;

        internal Simulation(
            SimulationParameters parameters,
            IMetricFactory metricFactory)
        {
            _parameters = parameters;

            _time = new SimulatedTime(_parameters, metricFactory);
            MetricFactory = metricFactory;
        }

        private readonly SimulationParameters _parameters;

        private readonly SimulatedTime _time;
    }
}
