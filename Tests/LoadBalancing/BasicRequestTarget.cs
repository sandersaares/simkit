using Microsoft.Extensions.Logging;
using Simkit;

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
    public Guid Id { get; } = Guid.NewGuid();

    public BasicRequestTarget(
        BasicRequestScenarioConfiguration scenarioConfiguration,
        ITime time,
        ILogger<BasicRequestTarget> logger)
    {
        _scenarioConfiguration = scenarioConfiguration;
        _time = time;
        _logger = logger;
    }

    private readonly BasicRequestScenarioConfiguration _scenarioConfiguration;
    private readonly ITime _time;
    private readonly ILogger _logger;

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

            request.RegisterForCompletionNotification(() => OnRequestCompleted(request));
        }
    }

    private readonly List<BasicRequest> _activeRequests = new();

    private readonly object _lock = new();

    private void OnRequestCompleted(BasicRequest request)
    {
        lock (_lock)
        {
            _activeRequests.Remove(request);
        }
    }

    public ITargetSnapshot GetSnapshot()
    {
        return new StaticTargetSnapshot(Id);
    }
}
