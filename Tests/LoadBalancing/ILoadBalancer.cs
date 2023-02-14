namespace Tests.LoadBalancing;

/// <summary>
/// One replica of the load balancer, making decisions on which targets will handle which requests.
/// </summary>
internal interface ILoadBalancer
{
    /// <summary>
    /// Makes a routing decision for a specific request, returning the ID of the target that is to handle this request.
    /// </summary>
    Guid RouteRequest(IRequest request);
}
