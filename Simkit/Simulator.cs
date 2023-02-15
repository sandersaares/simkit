using System.Text;
using Karambolo.Extensions.Logging.File;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Simkit;

/// <summary>
/// Executes simulations and exports their results for easy human analysis.
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
    /// Sets the callback to use when the service collection for a simulation needs to be configured.
    /// This is called for every simulation run that is executed by the simulator, before invoking that run of the simulation.
    /// </summary>
    public void ConfigureServices(Action<IServiceCollection> configureServices)
    {
        _configureServices = configureServices;
    }

    private Action<IServiceCollection> _configureServices = _ => { };

    /// <summary>
    /// Executes the configured number of iterations of the simulation.
    /// Telemetry from each iteration is written to persistent storage for later manual analysis.
    /// </summary>
    /// <param name="executeSimulationRun">
    /// Callback called for every simulation run that is to be executed, potentially concurrently.
    /// The callback is expected to do any necessary setup and then call ISimulation.ExecuteAsync().
    /// </param>
    /// <param name="cancel">Signal to cancel the simulation.</param>
    public async Task ExecuteAsync(Func<ISimulation, CancellationToken, Task> executeSimulationRun, CancellationToken cancel)
    {
        using var timeoutCts = new CancellationTokenSource(Parameters.Timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancel, timeoutCts.Token);

        var artifactsPath = SimulationArtifacts.GetArtifactsPath(SimulationId);
        Directory.CreateDirectory(artifactsPath);

        var metricsExportPath = Path.Combine(artifactsPath, SimulationArtifacts.MetricsExportFilename);
        await using var metricsHistorySerializer = new MetricHistorySerializer(File.Create(metricsExportPath));

        using var parallelismLimiter = new SemaphoreSlim(Environment.ProcessorCount);

        var tasks = new List<Task>();

        for (var runIndex = 0; runIndex < Parameters.RunCount; runIndex++)
        {
            var thisRunIndex = runIndex; // Copy for the closure.
            tasks.Add(Task.Run(() => ExecuteOneRunAsync(thisRunIndex, executeSimulationRun, metricsHistorySerializer, parallelismLimiter, combinedCts.Token), combinedCts.Token));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ExecuteOneRunAsync(int runIndex, Func<ISimulation, CancellationToken, Task> executeSimulationRun, MetricHistorySerializer metricsHistorySerializer, SemaphoreSlim parallelismLimiter, CancellationToken cancel)
    {
        var runIdentifier = new SimulationRunIdentifier(SimulationId, runIndex);

        await parallelismLimiter.WaitAsync(cancel);

        try
        {
            await using var simulation = new Simulation(runIdentifier, Parameters, metricsHistorySerializer, _configureServices, cancel);
            await executeSimulationRun(simulation, cancel);
        }
        finally
        {
            parallelismLimiter.Release();
        }
    }

    private sealed class Simulation : ISimulation
    {
        public IServiceProvider Services => _host.Services;

        public async Task ExecuteAsync()
        {
            var tickCount = _parameters.TicksPerSimulation;

            for (var tickIndex = 0; tickIndex < tickCount; tickIndex++)
            {
                await _time.ProcessCurrentTickAsync(_cancel);

                await _metricHistory.SampleMetricsIfAppropriateAsync(_time.UtcNow, _cancel);

                _time.MoveToNextTick();
            }
        }

        internal Simulation(
            SimulationRunIdentifier identifier,
            SimulationParameters parameters,
            MetricHistorySerializer metricHistorySerializer,
            Action<IServiceCollection> configureServices,
            CancellationToken cancel)
        {
            _identifier = identifier;
            _parameters = parameters;
            _cancel = cancel;

            _metricsRegistry = Metrics.NewCustomRegistry();
            _metricFactory = Metrics.WithCustomRegistry(_metricsRegistry);

            _metricHistory = new MetricHistory(parameters, _identifier, _metricsRegistry, metricHistorySerializer);

            _time = new SimulatedTime(_parameters, _metricFactory);

            _host = CreateSimulationHost(configureServices);
        }

        private readonly SimulationRunIdentifier _identifier;
        private readonly SimulationParameters _parameters;
        private readonly CancellationToken _cancel;

        private readonly CollectorRegistry _metricsRegistry;
        // Registered as a service (via IMetricFactory).
        private readonly MetricFactory _metricFactory;

        private readonly MetricHistory _metricHistory;

        // Registered as a service.
        private readonly SimulatedTime _time;

        private readonly IHost _host;

        private IHost CreateSimulationHost(Action<IServiceCollection> configureUserServices)
            => Host.CreateDefaultBuilder()
                .ConfigureLogging(ConfigureLogging)
                .ConfigureServices(services =>
                {
                    ConfigureBuiltinServices(services);
                    configureUserServices(services);
                })
                .Build();

        private void ConfigureBuiltinServices(IServiceCollection services)
        {
            services.AddSingleton(_parameters);
            services.AddSingleton<IMetricFactory>(_metricFactory);
            services.AddSingleton<ITime>(_time);
            services.AddSingleton<SimulatedTime>(_time);
            services.AddTransient(typeof(CancellationToken), _ => _cancel);
        }

        #region Logging
        private sealed class SimulationFileLoggerContext : FileLoggerContext
        {
            public SimulationFileLoggerContext(ITime time) : base(default)
            {
                _time = time;
            }

            private readonly ITime _time;

            public override DateTimeOffset GetTimestamp()
            {
                return _time.UtcNow;
            }
        }

        private sealed class SimulationFileLogEntryTextBuilder : FileLogEntryTextBuilder
        {
            internal static new readonly SimulationFileLogEntryTextBuilder Instance = new();

            protected override void AppendTimestamp(StringBuilder sb, DateTimeOffset timestamp)
            {
                // By default, the base class emits local time. We override this here to avoid timezone conversion and use UTC (which we use for everything).
                sb.Append(" @ ").AppendLine($"{timestamp:u}");
            }
        }

        private void ConfigureLogging(ILoggingBuilder logging)
        {
            logging.ClearProviders();

            void ConfigureOptions(FileLoggerOptions options)
            {
                options.Files = new[]
                {
                    new LogFileOptions
                    {
                        Path = SimulationArtifacts.GetLogFileName(_identifier)
                    }
                };

                options.FileAccessMode = LogFileAccessMode.KeepOpen;
                options.RootPath = Path.GetFullPath(SimulationArtifacts.GetArtifactsPath(_identifier.SimulationId));
                options.TextBuilder = SimulationFileLogEntryTextBuilder.Instance;
            }

            logging.AddFile(new SimulationFileLoggerContext(_time), ConfigureOptions);
        }
        #endregion

        public ValueTask DisposeAsync()
        {
            _host.Dispose();
            return default;
        }
    }
}
