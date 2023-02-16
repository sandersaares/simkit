using System.Collections.Concurrent;
using Prometheus;

namespace Tests.LoadBalancing;

internal sealed class RandomLoadBalancerMetrics
{
    public RandomLoadBalancerMetrics(IMetricFactory metricFactory)
    {
        _routedRequestsBase = metricFactory.CreateCounter("demo_load_balancer_requests_routed_total", "Number of requests routed, by target.", new[] { "target" });

        _createRoutedRequestByTargetDelegate = CreateRoutedRequestByTarget;
    }

    private readonly Counter _routedRequestsBase;

    public Counter.Child RoutedRequests(string target) => _routedRequestsByTarget.GetOrAdd(target, _createRoutedRequestByTargetDelegate);

    // target ID -> counter
    private readonly ConcurrentDictionary<string, Counter.Child> _routedRequestsByTarget = new();
    private Counter.Child CreateRoutedRequestByTarget(string target) => _routedRequestsBase.WithLabels(target);
    private Func<string, Counter.Child> _createRoutedRequestByTargetDelegate;
}
