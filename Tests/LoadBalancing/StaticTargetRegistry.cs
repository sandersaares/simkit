namespace Tests.LoadBalancing;

/// <summary>
/// A target registry that always returns the same snapshot of the same set of targets.
/// </summary>
internal sealed class StaticTargetRegistry : ITargetRegistry
{
    public StaticTargetRegistry(IReadOnlyList<ITargetSnapshot> targets)
    {
        _targets = targets;
    }

    private readonly IReadOnlyList<ITargetSnapshot> _targets;

    private sealed class Snapshot : ITargetRegistrySnapshot
    {
        public Snapshot(IReadOnlyList<ITargetSnapshot> targets)
        {
            Targets = targets;
        }

        public IReadOnlyList<ITargetSnapshot> Targets { get; }
    }

    public ITargetRegistrySnapshot GetSnapshot()
    {
        return new Snapshot(_targets);
    }
}
