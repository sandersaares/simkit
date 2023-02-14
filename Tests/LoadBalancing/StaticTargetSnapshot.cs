namespace Tests.LoadBalancing;

/// <summary>
/// A static target snapshot that just carries the target ID.
/// </summary>
internal sealed class StaticTargetSnapshot : ITargetSnapshot
{
    public StaticTargetSnapshot(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; }
}
