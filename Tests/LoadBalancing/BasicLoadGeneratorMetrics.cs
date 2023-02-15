using Prometheus;

namespace Tests.LoadBalancing;

internal sealed class BasicLoadGeneratorMetrics
{
    public BasicLoadGeneratorMetrics(IMetricFactory metricFactory)
    {
        RequestsCreated = metricFactory.CreateCounter("demo_load_generator_requests_created_total", "Number of requests the load generator has created.");
    }

    public Counter RequestsCreated { get; }
}
