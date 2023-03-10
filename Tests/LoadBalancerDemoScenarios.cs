using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simkit;
using Tests.LoadBalancing;

namespace Tests;

/// <summary>
/// Runs various load balancer scenario simulations to prove that simkit is suitable for this use case.
/// The algorithms and scenarios are quite simplified but should be enough to indicate a level of confidence in the framework.
/// </summary>
[TestClass]
public sealed class LoadBalancerDemoScenarios
{
    [DataRow(100)]
    [DataRow(200)]
    [DataRow(300)]
    [DataRow(400)]
    [DataRow(500)]
    [DataTestMethod]
    public async Task BasicScenario(int globalRequestsPerSecond)
    {
        var parameters = new SimulationParameters();
        var simulator = new Simulator(parameters);

        const int targetCount = 10;

        simulator.ConfigureServices(services =>
        {
            services.AddSingleton(new BasicRequestScenarioConfiguration(MaxRequestDuration: TimeSpan.FromSeconds(60), MaxConcurrentRequestsPerTarget: 1000, GlobalRequestsPerSecond: globalRequestsPerSecond));
            services.AddSingleton<BasicLoadGenerator>();

            services.AddSingleton<StaticTargetRegistry>();
            services.AddSingleton<ITargetRegistry>(sp => sp.GetRequiredService<StaticTargetRegistry>());

            services.AddTransient<BasicRequestTarget>();

            services.AddSingleton<RandomLoadBalancer>();

            services.AddSingleton<BasicResultsAggregator>();
            services.AddSingleton<IResultsAggregator>(sp => sp.GetRequiredService<BasicResultsAggregator>());
        });

        await simulator.ExecuteAsync(async (simulation, cancel) =>
        {
            var loadGenerator = simulation.GetRequiredService<BasicLoadGenerator>();

            var targets = new Dictionary<string, BasicRequestTarget>();
            for (var i = 0; i < targetCount; i++)
            {
                var target = simulation.GetRequiredService<BasicRequestTarget>();
                targets.Add(target.Id, target);
            }

            var targetRegistry = simulation.GetRequiredService<StaticTargetRegistry>();
            targetRegistry.SetTargets(targets.Values.Select(x => x.GetSnapshot()).ToList());

            var loadBalancer = simulation.GetRequiredService<RandomLoadBalancer>();

            var logger = simulation.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(BasicScenario));
            logger.LogInformation("Running scenario {scenario} with {rps} requests per second.", nameof(BasicScenario), globalRequestsPerSecond);

            var simulatedTime = simulation.GetRequiredService<SimulatedTime>();
            var resultsAggregator = simulation.GetRequiredService<BasicResultsAggregator>();

            simulatedTime.TickElapsed += delegate
            {
                while (loadGenerator.TryGetPendingRequests(out var requestsBuffer, out var requestCount))
                {
                    for (var i = 0; i < requestCount; i++)
                    {
                        var request = requestsBuffer[i];

                        var targetId = loadBalancer.RouteRequest(request);

                        if (!targets.TryGetValue(targetId, out var target))
                        {
                            request.MarkAsFailed($"Routed to non-existing target {targetId}.");
                            return;
                        }

                        target.Handle(request);
                    }
                }
            };

            await simulation.ExecuteAsync();

            var successCount = resultsAggregator.RequestsCompletedByClient + resultsAggregator.RequestsCompletedByTarget;
            var failureCount = resultsAggregator.RequestsFailed;
            var pendingCount = resultsAggregator.RequestsCreated - successCount - failureCount;

            LoadBalancingLog.ScenarioCompleted(logger, successCount, failureCount, pendingCount);
        }, CancellationToken.None);
    }
}
