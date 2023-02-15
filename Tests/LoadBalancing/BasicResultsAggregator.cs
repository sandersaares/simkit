using Prometheus;

namespace Tests.LoadBalancing;

internal sealed class BasicResultsAggregator : IResultsAggregator
{
    public int RequestsCompletedByClient;
    public int RequestsCompletedByTarget;
    public int RequestsCreated;
    public int RequestsFailed;

    public void OnRequestCompletedByClient()
    {
        Interlocked.Increment(ref RequestsCompletedByClient);
        _metrics.RequestsCompletedByClient.Inc();
    }

    public void OnRequestCompletedByTarget()
    {
        Interlocked.Increment(ref RequestsCompletedByTarget);
        _metrics.RequestsCompletedByTarget.Inc();
    }

    public void OnRequestCreated()
    {
        Interlocked.Increment(ref RequestsCreated);
        _metrics.RequestsCreated.Inc();
    }

    public void OnRequestFailed(string reason)
    {
        Interlocked.Increment(ref RequestsFailed);
        _metrics.RequestsFailed(reason).Inc();
    }

    public BasicResultsAggregator(IMetricFactory metricFactory)
    {
        _metrics = new BasicResultsAggregatorMetrics(metricFactory);
    }

    private readonly BasicResultsAggregatorMetrics _metrics;
}
