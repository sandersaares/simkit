namespace Tests.LoadBalancing;

/// <summary>
/// An immutable snapshot of a target at a certain point in time.
/// </summary>
internal interface ITargetSnapshot
{
    /// <summary>
    /// The unique ID of the target. In some real scenario we might use IP addresses or URLs but for simulation demo purposes a GUID is enough.
    /// </summary>
    string Id { get; }
}
