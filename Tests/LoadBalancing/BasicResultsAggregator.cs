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
    }

    public void OnRequestCompletedByTarget()
    {
        Interlocked.Increment(ref RequestsCompletedByTarget);
    }

    public void OnRequestCreated()
    {
        Interlocked.Increment(ref RequestsCreated);
    }

    public void OnRequestFailed()
    {
        Interlocked.Increment(ref RequestsFailed);
    }
}
