using System.Diagnostics.CodeAnalysis;

namespace Tests.LoadBalancing;

/// <summary>
/// Provides requests for a load balancing simulation.
/// </summary>
internal interface ILoadGenerator<TRequest>
    where TRequest : IRequest
{
    /// <summary>
    /// Gets the next pending request (if any). Returns false if there are not more pending requests (for now).
    /// </summary>
    bool TryGetPendingRequest([NotNullWhen(returnValue: true)] out TRequest? request);
}
