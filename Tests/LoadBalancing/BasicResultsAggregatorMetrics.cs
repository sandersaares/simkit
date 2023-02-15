using Prometheus;

namespace Tests.LoadBalancing;

internal sealed class BasicResultsAggregatorMetrics
{
    public BasicResultsAggregatorMetrics(IMetricFactory metricFactory)
    {
        RequestsCompletedByClient = metricFactory.CreateCounter("demo_requests_completed_by_client_total", "Number of requests that have been successfully completed because the client closed the connection before the target.");
        RequestsCompletedByTarget = metricFactory.CreateCounter("demo_requests_completed_by_target_total", "Number of requests that have been successfully completed because the target closed the connection before the client.");
        RequestsCreated = metricFactory.CreateCounter("demo_requests_created_total", "Number of requests that have been created in the scenario.");

        _requestsFailedBase = metricFactory.CreateCounter("demo_requests_failed_total", "Number of requests that have failed for any reason, per reason.", new[] { "reason" });
    }

    public Counter RequestsCompletedByClient { get; }
    public Counter RequestsCompletedByTarget { get; }
    public Counter RequestsCreated { get; }

    public Counter.Child RequestsFailed(string reason) => _requestsFailedBase.WithLabels(reason);

    private readonly Counter _requestsFailedBase;
}
