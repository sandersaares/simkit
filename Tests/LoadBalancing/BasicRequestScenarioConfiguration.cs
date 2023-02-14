namespace Tests.LoadBalancing;

/// <summary>
/// Configuration for a load balancing scenario that combines BasicRequest, BasicLoadGenerator and BasicRequestTarget.
/// </summary>
/// <param name="MaxRequestDuration">Max duration of a request (the duration of work desired by client).</param>
/// <param name="MaxConcurrentRequestsPerTarget">Max number of requests a target will accept at the same time.</param>
/// <param name="GlobalRequestsPerSecond">Number of requests that are generated every second, more or less at a steady rate (tick interval permitting).</param>
internal sealed record BasicRequestScenarioConfiguration(TimeSpan MaxRequestDuration, int MaxConcurrentRequestsPerTarget, double GlobalRequestsPerSecond);