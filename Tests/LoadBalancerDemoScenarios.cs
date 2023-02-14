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
    [TestMethod]
    public async Task BasicScenario()
    {
        var parameters = new SimulationParameters();
        var simulator = new Simulator(parameters);

        const int targetCount = 10;

        simulator.ConfigureServices(services =>
        {
            services.AddSingleton(new BasicRequestScenarioConfiguration(MaxRequestDuration: TimeSpan.FromSeconds(60), MaxConcurrentRequestsPerTarget: 1000, GlobalRequestsPerSecond: 1000));
            services.AddSingleton<BasicLoadGenerator>();

            services.AddSingleton<StaticTargetRegistry>();
            services.AddSingleton<ITargetRegistry>(sp => sp.GetRequiredService<StaticTargetRegistry>());

            services.AddTransient<BasicRequestTarget>();

            services.AddSingleton<RandomLoadBalancer>();
        });

        await simulator.ExecuteAsync(async (simulation, cancel) =>
        {
            var loadGenerator = simulation.GetRequiredService<BasicLoadGenerator>();

            var targets = new List<BasicRequestTarget>();
            for (var i = 0; i < targetCount;i++)
                targets.Add(simulation.GetRequiredService<BasicRequestTarget>());

            var targetRegistry = simulation.GetRequiredService<StaticTargetRegistry>();
            targetRegistry.Targets = targets.Select(x => x.GetSnapshot()).ToList();

            var loadBalancer = simulation.GetRequiredService<RandomLoadBalancer>();

            var logger = simulation.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(BasicScenario));

            var allRequests = new List<BasicRequest>();

            var simulatedTime = simulation.GetRequiredService<SimulatedTime>();

            simulatedTime.TickElapsed += delegate
            {
                while (loadGenerator.TryGetPendingRequest(out var request))
                {
                    // We add it for tracking here.
                    allRequests.Add(request);

                    var targetId = loadBalancer.RouteRequest(request);

                    var target = targets.FirstOrDefault(x => x.Id == targetId);

                    if (target == null)
                    {
                        logger.LogError($"Request {request.Id} routed to non-existing target {targetId}.");
                        request.MarkAsFailed($"Routed to non-existing target {targetId}.");
                        continue;
                    }

                    target.Handle(request);
                }
            };

            await simulation.ExecuteAsync(cancel);

            var successCount = allRequests.Count(x => x.IsCompleted && x.Succeeded);
            var failureCount = allRequests.Count(x => x.IsCompleted && !x.Succeeded);
            var pendingCount = allRequests.Count(x => !x.IsCompleted);

            logger.LogInformation($"{successCount:N0} requests successfully handled; {failureCount:N0} requests failed; {pendingCount:N0} still pending at end of simulation.");
        }, CancellationToken.None);
    }
}
