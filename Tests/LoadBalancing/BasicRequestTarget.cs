namespace Tests.LoadBalancing;

/// <summary>
/// A load balancing target that can handle BasicRequests.
/// 
/// Each request lasts for a specific duration, defined by the load generation logic.
/// There is a limit of N concurrent requests per target - if threshold is crossed, requests will fail.
/// </summary>
/// <remarks>
/// Thread-safe.
/// </remarks>
internal sealed class BasicRequestTarget
{
    public string Id { get; } = Guid.NewGuid().ToString();

    public BasicRequestTarget(
        BasicRequestScenarioConfiguration scenarioConfiguration)
    {
        _scenarioConfiguration = scenarioConfiguration;

        _snapshot = new StaticTargetSnapshot(Id);

        _onRequestCompletedDelegate = OnRequestCompleted;
    }

    private readonly BasicRequestScenarioConfiguration _scenarioConfiguration;

    public void Handle(BasicRequest request)
    {
        if (_activeRequests.Count >= _scenarioConfiguration.MaxConcurrentRequestsPerTarget)
        {
            request.MarkAsFailed("Max capacity reached.");
            return;
        }

        lock (_lock)
        {
            // The request itself signals when it is completed (at which point we remove it from our active requests set).
            _activeRequests.Add(request);

            request.RegisterForCompletionNotification(_onRequestCompletedDelegate);
        }
    }

    private readonly List<BasicRequest> _activeRequests = new();

    private readonly object _lock = new();

    private readonly Action<BasicRequest> _onRequestCompletedDelegate;

    private void OnRequestCompleted(BasicRequest request)
    {
        lock (_lock)
        {
            _activeRequests.Remove(request);
        }
    }

    public ITargetSnapshot GetSnapshot() => _snapshot;
    private readonly ITargetSnapshot _snapshot;
}
