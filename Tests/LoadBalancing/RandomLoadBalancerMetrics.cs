using Prometheus;

namespace Tests.LoadBalancing;

internal sealed class RandomLoadBalancerMetrics
{
    public RandomLoadBalancerMetrics(IMetricFactory metricFactory)
    {
        _routedRequestsBase = metricFactory.CreateCounter("demo_load_balancer_requests_routed_total", "Number of requests routed, by target.", new[] { "target" });
    }

    private readonly Counter _routedRequestsBase;

    public Counter.Child RoutedRequests(string target)
    {
        return _routedRequestsBase.WithLabels(target);
    }
}
