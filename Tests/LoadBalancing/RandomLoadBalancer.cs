using Prometheus;

namespace Tests.LoadBalancing;

/// <summary>
/// Just picks a random target for each request.
/// </summary>
internal sealed class RandomLoadBalancer : ILoadBalancer
{
    public RandomLoadBalancer(
        ITargetRegistry targetRegistry,
        IMetricFactory metricFactory)
    {
        _targetRegistry = targetRegistry;

        _metrics = new RandomLoadBalancerMetrics(metricFactory);
    }

    private readonly ITargetRegistry _targetRegistry;

    private readonly RandomLoadBalancerMetrics _metrics;

    public string RouteRequest(IRequest request)
    {
        var targets = _targetRegistry.GetSnapshot();

        if (targets.Targets.Count == 0)
        {
            // There is nothing we can do if there are no valid targets.
            // Just return the ID of a target that does not exist, so the request gets blackholed and fails.
            _metrics.RoutedRequests("").Inc();
            return "";
        }

        var winnerIndex = Random.Shared.Next(targets.Targets.Count);
        var winnerId = targets.Targets[winnerIndex].Id;

        _metrics.RoutedRequests(winnerId).Inc();

        return winnerId;
    }
}
