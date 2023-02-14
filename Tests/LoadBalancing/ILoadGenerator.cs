using System.Diagnostics.CodeAnalysis;

namespace Tests.LoadBalancing;

/// <summary>
/// Provides requests for a load balancing simulation.
/// </summary>
internal interface ILoadGenerator<TRequest>
    where TRequest : IRequest
{
    /// <summary>
    /// Gets a set of pending requests (if any). Returns false if there are not more pending requests (for now).
    /// The returned buffer may only be used until the next call to TryGetPendingRequests().
    /// </summary>
    bool TryGetPendingRequests([NotNullWhen(returnValue: true)] out TRequest[]? requestsBuffer, out int requestCount);
}
