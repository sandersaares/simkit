namespace Tests.LoadBalancing;

/// <summary>
/// A target registry that just acts like a variable - always returning a specific set of targets it is told to return.
/// </summary>
internal sealed class StaticTargetRegistry : ITargetRegistry
{
    public StaticTargetRegistry()
    {
    }

    public IReadOnlyList<ITargetSnapshot> Targets { get; private set; } = Array.Empty<ITargetSnapshot>();

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
        if (_snapshot == null)
            throw new InvalidOperationException("Targets have not been set yet.");

        return _snapshot;
    }

    private ITargetRegistrySnapshot? _snapshot;

    public void SetTargets(IReadOnlyList<ITargetSnapshot> targets)
    {
        Targets = targets;
        _snapshot = new Snapshot(Targets);
    }
}
