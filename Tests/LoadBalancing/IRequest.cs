namespace Tests.LoadBalancing;

/// <summary>
/// One request to the service, with relevant metadata that can be used by the load balancer logic.
/// This is the load balancer's view. See IRoutedRequest for the representation of a request that has been handed over to the target.
/// </summary>
internal interface IRequest
{
    Guid Id { get; }

    // For now, we treat each request as an opaque object - we do not know what the caller wants, we need to make a blind decision.
}
