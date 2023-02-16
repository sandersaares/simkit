namespace Simkit;

public sealed record SimulationParameters
{
    /// <summary>
    /// Start of the simulation timeline - the first clock tick will occur on this moment.
    /// </summary>
    public DateTimeOffset StartTime { get; init; } = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Amount of wall clock time taken up by one simulation tick.
    /// </summary>
    /// <remarks>
    /// If you set this too high, simulated periodic timers may skip overlapping updates and other weird stuff might happen to decrease realism.
    /// </remarks>
    public TimeSpan TickDuration { get; init; } = TimeSpan.FromSeconds(1 / 60.0);

    /// <summary>
    /// How much simulated wall clock time does the simulation cover.
    /// </summary>
    public TimeSpan SimulationDuration { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How many times do we execute the simulation.
    /// </summary>
    /// <remarks>
    /// Data from all executions is published and available for comparison. More runs may help prove stability of the code under test.
    /// </remarks>
    public int RunCount { get; init; } = 5;

    /// <summary>
    /// Maximum amount of real time the simulation is allowed to take before we give up and consider it a failure.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How often the current values of metrics are recorded. These data points may be exported at the end of the simulation as CSV files.
    /// </summary>
    public TimeSpan MetricsSamplingInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Convenience method. The duration is driven by time-based parameters but this lets you quickly calculate the total tick count.
    /// </summary>
    /// <remarks>
    /// The +1 is because the first tick is the zero tick and has no effective duration.
    /// </remarks>
    public int TicksPerSimulation => (int)Math.Ceiling(SimulationDuration.TotalSeconds / TickDuration.TotalSeconds) + 1;

    internal void Validate()
    {
        // We require that metrics sampling happens on tick boundaries, to keep the logic sane.
        if (MetricsSamplingInterval.Ticks % TickDuration.Ticks != 0)
            throw new ArgumentException($"{nameof(MetricsSamplingInterval)} must be divisible by {nameof(TickDuration)}.");
    }
}
