namespace Simkit;

/// <summary>
/// Encapsulates a single metric timeseries with all its values.
/// Size predefined at creation time (to cover extent of an entire simulation run).
/// </summary>
internal sealed class MetricTimeseries
{
    internal string Name { get; }

    internal MetricTimeseries(
        string name,
        SimulationParameters parameters)
    {
        Name = name;
        _parameters = parameters;

        Data = new double?[_parameters.TicksPerSimulation];
    }

    private readonly SimulationParameters _parameters;

    internal readonly double?[] Data;

    /// <summary>
    /// Returns a reference to the data slot for the given timestamp.
    /// </summary>
    internal ref double? At(DateTimeOffset timestamp)
    {
        var elapsedTime = timestamp - _parameters.StartTime;
        var slotIndex = elapsedTime.Ticks / _parameters.MetricsSamplingInterval.Ticks;

        return ref Data[slotIndex];
    }
}