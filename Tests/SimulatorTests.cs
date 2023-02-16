using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simkit;

namespace Tests;

[TestClass]
public sealed class SimulatorTests
{
    /// <summary>
    /// Counts one tick per second. That's all it does, as a minimal example system under test.
    /// </summary>
    sealed class TickCounter : IDisposable
    {
        public long Ticks { get; private set; }

        public TickCounter(
            ITime time,
            ILogger<TickCounter> logger)
        {
            _time = time;
            _logger = logger;
        }

        public void Start()
        {
            _time.StartTimer(TimeSpan.FromSeconds(1), OnTick, _cts.Token);
        }

        private readonly ITime _time;
        private readonly ILogger _logger;

        private readonly CancellationTokenSource _cts = new();

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        private bool OnTick()
        {
            Ticks++;

            // Log messages will be available for later analysis.
            // Note that logging comes with a significant performance cost, so use sparingly.
            _logger.LogInformation($"Have seen {Ticks} ticks.");

            return true;
        }
    }

    [TestMethod]
    public async Task TickCounter_CountsTicksAtExpectedRate()
    {
        var parameters = new SimulationParameters();
        var simulator = new Simulator(parameters);

        simulator.ConfigureServices(services =>
        {
            services.AddSingleton<TickCounter>();
        });

        await simulator.ExecuteAsync(async (simulation, cancel) =>
        {
            var tickCounter = simulation.GetRequiredService<TickCounter>();
            tickCounter.Start();

            await simulation.ExecuteAsync();

            // Validate results - did the simulated scenario actually succeed?
            // The counter counts one tick per second, so that's how much we expect to see after the simulation is completed.
            Assert.AreEqual((int)parameters.SimulationDuration.TotalSeconds, tickCounter.Ticks);
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task TickCounter_EmitsExpectedTelemetry()
    {
        var parameters = new SimulationParameters();
        var simulator = new Simulator(parameters);

        simulator.ConfigureServices(services =>
        {
            services.AddSingleton<TickCounter>();
        });

        await simulator.ExecuteAsync(async (simulation, cancel) =>
        {
            var tickCounter = simulation.GetRequiredService<TickCounter>();
            tickCounter.Start();

            await simulation.ExecuteAsync();
        }, CancellationToken.None);

        var artifactsPath = SimulationArtifacts.GetArtifactsPath(simulator.SimulationId);
        var metricsExportPath = Path.Combine(artifactsPath, SimulationArtifacts.MetricsExportFilename);

        // We expect a metrics file to exist (combined data from all runs).
        Assert.IsTrue(File.Exists(metricsExportPath));

        for (var runIndex = 0; runIndex < parameters.RunCount; runIndex++)
        {
            var logFilename = SimulationArtifacts.GetLogFileName(new SimulationRunIdentifier(simulator.SimulationId, runIndex));
            var logPath = Path.Combine(artifactsPath, logFilename);

            // We expect one log file to exist per run.
            Assert.IsTrue(File.Exists(logPath));
        }
    }
}
