using System.Diagnostics.CodeAnalysis;
using Prometheus;
using Simkit;

namespace Tests.LoadBalancing;

/// <remarks>
/// Not thread-safe.
/// </remarks>
internal sealed class BasicLoadGenerator : ILoadGenerator<BasicRequest>
{
    public BasicLoadGenerator(
        SimulationParameters parameters,
        BasicRequestScenarioConfiguration scenarioConfiguration,
        IResultsAggregator resultsAggregator,
        ITime time,
        IMetricFactory metricFactory,
        CancellationToken cancel)
    {
        _parameters = parameters;
        _scenarioConfiguration = scenarioConfiguration;
        _resultsAggregator = resultsAggregator;
        _time = time;
        _cancel = cancel;

        _metrics = new BasicLoadGeneratorMetrics(metricFactory);
    }

    private readonly SimulationParameters _parameters;
    private readonly BasicRequestScenarioConfiguration _scenarioConfiguration;
    private readonly IResultsAggregator _resultsAggregator;
    private readonly ITime _time;
    private readonly CancellationToken _cancel;

    private readonly BasicLoadGeneratorMetrics _metrics;

    private int _requestsCreated;

    private readonly BasicRequest[] _pendingRequestBuffer = new BasicRequest[128];

    public bool TryGetPendingRequests([NotNullWhen(returnValue: true)] out BasicRequest[]? requestsBuffer, out int requestCount)
    {
        requestCount = 0;
        requestsBuffer = _pendingRequestBuffer;

        var elapsedTime = _time.UtcNow - _parameters.StartTime;
        var expectedRequestCount = (int)(elapsedTime.TotalSeconds * _scenarioConfiguration.GlobalRequestsPerSecond);
        var missingRequestCount = expectedRequestCount - _requestsCreated;

        if (missingRequestCount <= 0)
            return false;

        requestCount = Math.Min(_pendingRequestBuffer.Length, missingRequestCount);
        _requestsCreated += requestCount;
        _metrics.RequestsCreated.Inc(requestCount);

        for (var i = 0; i < requestCount; i++)
        {
            var requestDuration = TimeSpan.FromSeconds(_scenarioConfiguration.MaxRequestDuration.TotalSeconds * Random.Shared.NextDouble());

            requestsBuffer[i] = new BasicRequest(requestDuration, _time, _resultsAggregator, _cancel);
        }

        return requestCount != 0;
    }
}
