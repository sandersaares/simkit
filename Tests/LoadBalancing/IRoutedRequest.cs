namespace Tests.LoadBalancing;

/// <summary>
/// A request that has been handed over to a target for processing (or one that failed to be handed over).
/// This represents the state of a request after the load balancing decision is made.
/// </summary>
/// <remarks>
/// Thread-safe.
/// </remarks>
internal interface IRoutedRequest
{
    /// <summary>
    /// Indicates that the request has completed.
    /// 
    /// For successfully completed requests, the request object itself decides that it has succeeded and sets this flag,
    /// so the target handling the request knows to stop processing this request.
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Whether a completed request was successful. Value is undefined if IsCompleted is false.
    /// </summary>
    bool Succeeded { get; }

    /// <summary>
    /// If the request has completed but not succeeded, this will contain the reason.
    /// </summary>
    string? FailureReason { get; }

    /// <summary>
    /// Marks the request as failed.
    /// May be called either by the target handling the request (if that is where the failure occurred) or by previous logic (if the request never even made it to the target).
    /// </summary>
    void MarkAsFailed(string reason);

    /// <summary>
    /// Marks the request as completed by initiative of the target (it finished the request and signaled no error to the caller).
    /// </summary>
    void MarkAsCompletedByTarget();

    /// <summary>
    /// Marks the request as canceled by initiative of the client (this can be a normal and successful thing if it no longer has a need for it).
    /// </summary>
    void MarkAsCompletedByClient();
}
