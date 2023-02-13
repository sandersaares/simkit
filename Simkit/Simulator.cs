using Prometheus;

namespace Simkit;

/// <summary>
/// Executes simulations and processes their results for easy human understanding.
/// </summary>
public sealed class Simulator
{
    public SimulationParameters Parameters { get; }

    internal string SimulationId { get; }

    public Simulator(SimulationParameters parameters)
    {
        Parameters = parameters;

        SimulationId = $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-{Guid.NewGuid()}";
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
        var artifactsPath = SimulationArtifacts.GetArtifactsPath(SimulationId);
        Directory.CreateDirectory(artifactsPath);

        var metricsExportPath = Path.Combine(artifactsPath, SimulationArtifacts.MetricsExportFilename);
        await using var metricsHistorySerializer = new MetricHistorySerializer(File.Create(metricsExportPath));

        for (var runIndex = 0; runIndex < Parameters.ExecutionCount; runIndex++)
        {
            var runIdentifier = new SimulationRunIdentifier(SimulationId, runIndex);

            var simulation = new Simulation(runIdentifier, Parameters, metricsHistorySerializer);
            await _onExecute(simulation, cancel);
        }
    }

    private sealed class Simulation : ISimulation
    {
        public ITime Time => _time;
        public IMetricFactory MetricFactory => _metricFactory;

        public async Task ExecuteAsync(CancellationToken cancel)
        {
            var tickCount = _parameters.TicksPerSimulation;

            for (var tickIndex = 0; tickIndex < tickCount; tickIndex++)
            {
                await _time.ProcessCurrentTickAsync(cancel);

                await _onTick(cancel);

                await _metricHistory.SampleMetricsIfAppropriateAsync(_time.UtcNow, cancel);

                _time.MoveToNextTick();
            }
        }

        public void OnTick(Func<CancellationToken, Task> onTick)
        {
            _onTick = onTick;
        }

        // Technically one could have a valid simulation with no per-tick callback, that is fine.
        private Func<CancellationToken, Task> _onTick = _ => Task.CompletedTask;

        internal Simulation(
            SimulationRunIdentifier identifier,
            SimulationParameters parameters,
            MetricHistorySerializer metricHistorySerializer)
        {
            _identifier = identifier;
            _parameters = parameters;

            _metricsRegistry = Metrics.NewCustomRegistry();
            _metricFactory = Metrics.WithCustomRegistry(_metricsRegistry);

            _metricHistory = new MetricHistory(parameters, _identifier, _metricsRegistry, metricHistorySerializer);

            _time = new SimulatedTime(_parameters, _metricFactory);
        }

        private readonly SimulationRunIdentifier _identifier;
        private readonly SimulationParameters _parameters;

        private readonly CollectorRegistry _metricsRegistry;
        private readonly MetricFactory _metricFactory;

        private readonly MetricHistory _metricHistory;

        private readonly SimulatedTime _time;
    }
}
