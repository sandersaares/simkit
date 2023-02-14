using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
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
        ITime time,
        IMetricFactory metricFactory,
        ILogger<BasicLoadGenerator> logger,
        ILoggerFactory loggerFactory)
    {
        _parameters = parameters;
        _scenarioConfiguration = scenarioConfiguration;
        _time = time;
        _logger = logger;
        _loggerFactory = loggerFactory;

        _metrics = new BasicLoadGeneratorMetrics(metricFactory);
    }

    private readonly SimulationParameters _parameters;
    private readonly BasicRequestScenarioConfiguration _scenarioConfiguration;
    private readonly ITime _time;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;

    private readonly BasicLoadGeneratorMetrics _metrics;

    private int _requestsCreated;

    private readonly BasicRequest[] _pendingRequestBuffer = new BasicRequest[1024];

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

            requestsBuffer[i] = new BasicRequest(requestDuration, _time);
        }

        return requestCount != 0;
    }
}
