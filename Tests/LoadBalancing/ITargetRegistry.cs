namespace Tests.LoadBalancing;

/// <summary>
/// Maintains a set of targets that can be loaded, updating the set over time.
/// We can request read-only snapshots of the current state on demand.
/// </summary>
internal interface ITargetRegistry
{
    ITargetRegistrySnapshot GetSnapshot();
}
