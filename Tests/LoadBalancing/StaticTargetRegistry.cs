namespace Tests.LoadBalancing;

/// <summary>
/// A target registry that just acts like a variable - always returning a specific set of targets it is told to return.
/// </summary>
internal sealed class StaticTargetRegistry : ITargetRegistry
{
    public StaticTargetRegistry()
    {
    }

    public IReadOnlyList<ITargetSnapshot> Targets { get; set; } = Array.Empty<ITargetSnapshot>();

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
        return new Snapshot(Targets);
    }
}
