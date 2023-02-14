using Microsoft.Extensions.Logging;

namespace Tests.LoadBalancing;

internal static partial class LoadBalancingLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Request {id} is expected to complete {completesOn:u} if successfully handled.")]
    public static partial void RequestCreated(ILogger logger, Guid id, DateTimeOffset completesOn);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Request {id} failed: {reason}")]
    public static partial void RequestFailed(ILogger logger, Guid id, string reason);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Request {id} successfully completed.")]
    public static partial void RequestSucceeded(ILogger logger, Guid id);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "{successCount:N0} requests successfully handled; {failureCount:N0} requests failed; {pendingCount:N0} still pending at end of simulation.")]
    public static partial void ScenarioCompleted(ILogger logger, int successCount, int failureCount, int pendingCount);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Target {targetId} rejecting request {requestId} because we are already handling the maximum number of requests.")]
    public static partial void TargetCapacityReached(ILogger logger, Guid targetId, Guid requestId);
}
