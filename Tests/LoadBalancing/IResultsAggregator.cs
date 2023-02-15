namespace Tests.LoadBalancing;

/// <summary>
/// Observes what happens to requests and aggregates the results into a summary that we can use to evaluate scenario success/failure.
/// </summary>
internal interface IResultsAggregator
{
    void OnRequestCreated();
    void OnRequestCompletedByClient();
    void OnRequestCompletedByTarget();
    void OnRequestFailed();
}
