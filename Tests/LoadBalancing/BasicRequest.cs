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
        ITime time)
    {
        var expectedCompletion = time.UtcNow + targetDuration;

        // We assume (for now) that the request will be routed & processed on the same tick as it is created. To keep it simple.
        time.Delay(targetDuration, OnCompleted, CancellationToken.None);
    }

    public bool IsCompleted { get; private set; }
    public bool Succeeded { get; private set; }
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Requests a callback to be called when the request has completed.
    /// </summary>
    /// <remarks>
    /// Do not reenter BasicRequest from within the callback - deadlock may occur.
    /// </remarks>
    public void RegisterForCompletionNotification(Action callback)
    {
        lock (_lock)
        {
            // Sanity check, to avoid complex reentrancy logic.
            if (IsCompleted)
                throw new InvalidOperationException("The request is already completed - you cannot register for completion notifications anymore.");

            _notifyOnCompleted = callback;
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

            _notifyOnCompleted();
        }
    }
}
