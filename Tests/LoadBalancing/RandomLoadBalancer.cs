namespace Tests.LoadBalancing;

/// <summary>
/// Just picks a random target for each request.
/// </summary>
internal sealed class RandomLoadBalancer : ILoadBalancer
{
    public RandomLoadBalancer(ITargetRegistry targetRegistry)
    {
        _targetRegistry = targetRegistry;
    }

    private readonly ITargetRegistry _targetRegistry;

    public Guid RouteRequest(IRequest request)
    {
        var targets = _targetRegistry.GetSnapshot();

        if (targets.Targets.Count == 0)
        {
            // There is nothing we can do if there are no valid targets.
            // Just return the ID of a target that does not exist, so the request gets blackholed and fails.
            return default;
        }

        var winnerIndex = Random.Shared.Next(targets.Targets.Count);
        return targets.Targets[winnerIndex].Id;
    }
}
