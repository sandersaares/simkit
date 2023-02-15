using System.IO.Compression;
using System.Text.Json;
using Nito.AsyncEx;

namespace Simkit;

/// <summary>
/// Serializes metric data points in compressed JSON lines format suitable for Azure Data Explorer bulk import.
/// </summary>
/// <remarks>
/// Thread-safe.
/// 
/// Standard fields:
/// * name (timeseries name)
/// * timestamp (timestamp from simulated timeline)
/// * value (numeric value of timeseries at this point in time)
/// * simulation_id (unique ID of simulation)
/// * run (index of the specific run of this simulation)
/// 
/// All other fields are simply filled from the metric labels as-is, as strings.
/// </remarks>
internal sealed class MetricHistorySerializer : IAsyncDisposable
{
    public MetricHistorySerializer(
        Stream outputStream)
    {
        _stream = new GZipStream(outputStream, CompressionLevel.Optimal);
        _writer = new Utf8JsonWriter(_stream, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = true,
        });
    }

    private readonly Stream _stream;
    private readonly Utf8JsonWriter _writer;

    public async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync();
        await _stream.DisposeAsync();
    }

    private readonly AsyncLock _lock = new();

    public async Task WriteMetricPointAsync(string name, DateTimeOffset time, double value, SimulationRunIdentifier simulationRunIdentifier, IDictionary<string, string> labels, CancellationToken cancel)
    {
        using var lockHolder = await _lock.LockAsync(cancel);

        _writer.WriteStartObject();

        _writer.WritePropertyName("name");
        _writer.WriteStringValue(name);

        _writer.WritePropertyName("timestamp");
        _writer.WriteStringValue(time.ToString("u"));

        _writer.WritePropertyName("value");
        _writer.WriteNumberValue(value);

        _writer.WritePropertyName("simulation_id");
        _writer.WriteStringValue(simulationRunIdentifier.SimulationId);

        _writer.WritePropertyName("run");
        _writer.WriteNumberValue(simulationRunIdentifier.RunIndex);

        foreach (var pair in labels)
        {
            _writer.WritePropertyName(pair.Key);
            _writer.WriteStringValue(pair.Value);
        }

        _writer.WriteEndObject();
    }

    public async Task FlushAsync(CancellationToken cancel)
    {
        using var lockHolder = await _lock.LockAsync(cancel);

        await _writer.FlushAsync(cancel);
    }
}
