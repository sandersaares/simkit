using Microsoft.Extensions.Logging;
using Simkit;

namespace Tests.LoadBalancing;

/// <summary>
/// A request that can be handled by BasicRequestTarget and lasts for a specific amount of time.
/// </summary>
internal sealed class BasicRequest : IRequest, IRoutedRequest
{
    public Guid Id { get; } = Guid.NewGuid();

    public BasicRequest(
        TimeSpan targetDuration,
        ITime time,
        ILogger<BasicRequest> logger)
    {
        _logger = logger;

        var expectedCompletion = time.UtcNow + targetDuration;
        _logger.LogDebug($"Request {Id} is expected to complete {expectedCompletion:u} if successfully handled.");

        // We assume (for now) that the request will be routed & processed on the same tick as it is created. To keep it simple.
        time.Delay(targetDuration, OnCompleted, CancellationToken.None);
    }

    private readonly ILogger _logger;

    public bool IsCompleted { get; private set; }
    public bool Succeeded { get; private set; }
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Asynchronously calls the callback when the request has completed.
    /// If the request has already completed, still calls the callback (still asynchronously).
    /// </summary>
    /// <remarks>
    /// Do not reenter BasicRequest from within the callback - deadlock may occur.
    /// </remarks>
    public void RegisterForCompletionNotification(Action callback)
    {
        lock (_lock)
        {
            _notifyOnCompleted = callback;

            if (IsCompleted)
                _notifyOnCompleted();
        }
    }

    private readonly object _lock = new();

    private Action _notifyOnCompleted = () => { };

    public void MarkAsFailed(string reason)
    {
        lock (_lock)
        {
            if (IsCompleted)
                return; // Unexpected but whatever - it already finished, so it no longer matters.

            IsCompleted = true;
            FailureReason = reason;

            _logger.LogDebug($"Request {Id} failed: {reason}");

            _notifyOnCompleted();
        }
    }

    private void OnCompleted(CancellationToken cancel)
    {
        lock (_lock)
        {
            if (IsCompleted)
                return;

            IsCompleted = true;
            Succeeded = true;

            _logger.LogDebug($"Request {Id} successfully completed.");

            _notifyOnCompleted();
        }
    }
}
