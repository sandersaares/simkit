using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simkit;

namespace Tests;

[TestClass]
public sealed class SimulatorTests
{
    /// <summary>
    /// Counts one tick per second.
    /// </summary>
    sealed class TickCounter : IDisposable
    {
        public long Ticks { get; private set; }

        public TickCounter(ITime time, ILogger<TickCounter> logger)
        {
            _logger = logger;

            time.StartTimer(TimeSpan.FromSeconds(1), OnTick, _cts.Token);
        }

        private readonly ILogger _logger;

        private readonly CancellationTokenSource _cts = new();

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        private bool OnTick(CancellationToken cancel)
        {
            Ticks++;

            _logger.LogInformation($"Have seen {Ticks} ticks.");

            return true;
        }
    }

    [TestMethod]
    public async Task TickCounter_CountsTicksAtExpectedRateAndEmitsExpectedTelemetry()
    {
        var parameters = new SimulationParameters();
        var simulator = new Simulator(parameters);

        simulator.ConfigureServices(services =>
        {
            services.AddSingleton<TickCounter>();
        });

        // We expect this to run the simulation and also write any telemetry to disk.
        await simulator.ExecuteAsync(async (simulation, cancel) =>
        {
            var tickCounter = simulation.GetRequiredService<TickCounter>();

            await simulation.ExecuteAsync();

            // It counts one per second, so that's how much we expect to see.
            Assert.AreEqual((int)parameters.SimulationDuration.TotalSeconds, tickCounter.Ticks);
        }, CancellationToken.None);

        var artifactsPath = SimulationArtifacts.GetArtifactsPath(simulator.SimulationId);
        var metricsExportPath = Path.Combine(artifactsPath, SimulationArtifacts.MetricsExportFilename);

        Assert.IsTrue(File.Exists(metricsExportPath));

        for (var runIndex = 0; runIndex < parameters.RunCount; runIndex++)
        {
            var logFilename = SimulationArtifacts.GetLogFileName(new SimulationRunIdentifier(simulator.SimulationId, runIndex));
            var logPath = Path.Combine(artifactsPath, logFilename);

            Assert.IsTrue(File.Exists(logPath));
        }
    }
}
