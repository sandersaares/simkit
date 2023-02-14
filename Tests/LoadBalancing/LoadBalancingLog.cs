using Microsoft.Extensions.Logging;

namespace Tests.LoadBalancing;

internal static partial class LoadBalancingLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "{successCount:N0} requests successfully handled; {failureCount:N0} requests failed; {pendingCount:N0} still pending at end of simulation.")]
    public static partial void ScenarioCompleted(ILogger logger, int successCount, int failureCount, int pendingCount);
}
