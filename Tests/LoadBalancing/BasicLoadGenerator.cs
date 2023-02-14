using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Prometheus;
using Simkit;

namespace Tests.LoadBalancing;

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

    private readonly object _lock = new();

    public bool TryGetPendingRequest([NotNullWhen(returnValue: true)] out BasicRequest? request)
    {
        lock (_lock)
        {
            var elapsedTime = _time.UtcNow - _parameters.StartTime;
            var expectedRequestCount = elapsedTime.TotalSeconds * _scenarioConfiguration.GlobalRequestsPerSecond;

            if (expectedRequestCount <= _requestsCreated)
            {
                request = default;
                return false;
            }

            var requestDuration = TimeSpan.FromSeconds(_scenarioConfiguration.MaxRequestDuration.TotalSeconds * Random.Shared.NextDouble());

            _requestsCreated++;
            _metrics.RequestsCreated.Inc();
            request = new BasicRequest(requestDuration, _time, _loggerFactory.CreateLogger<BasicRequest>());
            return true;
        }
    }
}
