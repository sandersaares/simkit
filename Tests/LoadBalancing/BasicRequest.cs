using Simkit;

namespace Tests.LoadBalancing;

/// <summary>
/// A request that can be handled by BasicRequestTarget and lasts for a specific amount of time.
/// </summary>
internal sealed class BasicRequest : IRequest, IRoutedRequest
{
    public BasicRequest(
        TimeSpan targetDuration,
        ITime time,
        IResultsAggregator resultsAggregator,
        CancellationToken cancel)
    {
        _resultsAggregator = resultsAggregator;

        _resultsAggregator.OnRequestCreated();

        var expectedCompletion = time.UtcNow + targetDuration;

        // We assume (for now) that the request will be routed & processed on the same tick as it is created. To keep it simple.
        // Once the target duration elapses, we mark it as completed via this timer (unless something has already marked it as failed earlier).
        time.Delay(targetDuration, OnCompletedByTarget, this, cancel);
    }

    private readonly IResultsAggregator _resultsAggregator;

    public bool IsCompleted { get; private set; }
    public bool Succeeded { get; private set; }
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Requests a callback to be called when the request has completed.
    /// </summary>
    /// <remarks>
    /// Do not reenter BasicRequest from within the callback - deadlock may occur.
    /// </remarks>
    public void RegisterForCompletionNotification(Action<BasicRequest> callback)
    {
        lock (this)
        {
            // Sanity check, to avoid complex reentrancy logic.
            if (IsCompleted)
                throw new InvalidOperationException("The request is already completed - you cannot register for completion notifications anymore.");

            _notifyOnCompleted = callback;
        }
    }

    private Action<BasicRequest> _notifyOnCompleted = _ => { };

    // Called by the delay started in the ctor.
    private static void OnCompletedByTarget(object state)
    {
        var request = (BasicRequest)state;
        request.MarkAsCompletedByTarget();
    }

    public void MarkAsFailed(string reason)
    {
        lock (this)
        {
            if (IsCompleted)
                return; // Unexpected but whatever - it already finished, so it no longer matters.

            IsCompleted = true;
            FailureReason = reason;
        }

        _notifyOnCompleted(this);
        _resultsAggregator.OnRequestFailed();
    }

    public void MarkAsCompletedByTarget()
    {
        lock (this)
        {
            if (IsCompleted)
                return;

            IsCompleted = true;
            Succeeded = true;
        }

        _notifyOnCompleted(this);
        _resultsAggregator.OnRequestCompletedByTarget();
    }

    public void MarkAsCompletedByClient()
    {
        lock (this)
        {
            if (IsCompleted)
                return;

            IsCompleted = true;
            Succeeded = true;
        }

        _notifyOnCompleted(this);
        _resultsAggregator.OnRequestCompletedByClient();
    }
}
