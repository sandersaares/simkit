namespace Tests.LoadBalancing;

/// <summary>
/// An immutable snapshot of all the knowledge we had about our targets at a certain point in time.
/// </summary>
internal interface ITargetRegistrySnapshot
{
    IReadOnlyList<ITargetSnapshot> Targets { get; }
}
