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

        public TickCounter(ITime time)
        {
            time.StartTimer(TimeSpan.FromSeconds(1), OnTick, _cts.Token);
        }

        private readonly CancellationTokenSource _cts = new();

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        private bool OnTick(CancellationToken cancel)
        {
            Ticks++;
            return true;
        }
    }

    [TestMethod]
    public async Task TickCounter_CountsTicksAtExpectedRateAndEmitsExpectedTelemetry()
    {
        var parameters = new SimulationParameters();

        var simulator = new Simulator(parameters);

        simulator.OnExecute(async (simulation, cancel) =>
        {
            var tickCounter = new TickCounter(simulation.Time);

            await simulation.ExecuteAsync(cancel);

            // It counts one per second, so that's how much we expect to see.
            Assert.AreEqual((int)parameters.SimulationDuration.TotalSeconds, tickCounter.Ticks);
        });

        // We expect this to run the simulation and also write any telemetry to disk.
        await simulator.ExecuteAsync(CancellationToken.None);

        var artifactsPath = SimulationArtifacts.GetArtifactsPath(simulator.SimulationId);
        var metricsExportPath = Path.Combine(artifactsPath, SimulationArtifacts.MetricsExportFilename);

        Assert.IsTrue(File.Exists(metricsExportPath));
    }
}
