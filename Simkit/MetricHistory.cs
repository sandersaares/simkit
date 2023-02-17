using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Prometheus;

namespace Simkit;

/// <summary>
/// Observes the metrics in a metric registry and exports them for Azure Data Explorer analysis at a configured sampling interval.
/// </summary>
/// <remarks>
/// Not thread-safe.
/// 
/// We only record data on every Nth tick (although N may be 1) because typically for whole-simulation analysis the useful granularity is something realistic.
/// For production scenarios, the typical recording interval is 10-60 seconds, although for debugging simulations 1 second can also make a lot of sense.
/// 
/// The data set generated can be large but ultimatley will be analyzed in Azure Data Explorer, so this is merely a storage and upload convenience concern.
/// We serialize the data (via MetricHistorySerializer) in real time, without building up big buffers, to avoid consuming excessive amounts of memory.
/// </remarks>
internal sealed class MetricHistory
{
    internal MetricHistory(
        SimulationParameters parameters,
        SimulationRunIdentifier simulationRunIdentifier,
        CollectorRegistry metricsRegistry,
        MetricHistorySerializer serializer)
    {
        _parameters = parameters;
        _simulationRunIdentifier = simulationRunIdentifier;
        _metricsRegistry = metricsRegistry;
        _serializer = serializer;

        // We record the first sample immediately, to help ensure we start from a nice clean state.
        _captureNextSampleOnOrAfter = _parameters.StartTime;
    }

    private readonly SimulationParameters _parameters;
    private readonly SimulationRunIdentifier _simulationRunIdentifier;
    private readonly CollectorRegistry _metricsRegistry;
    private readonly MetricHistorySerializer _serializer;

    private DateTimeOffset _captureNextSampleOnOrAfter;

    // We reuse the same MemoryStream to avoid constantly allocating new memory to serialize the metrics.
    // We allow it to scale up freely and maintain whatever buffer it takes for itself.
    private readonly MemoryStream _serializationBuffer = new(1024 * 1024);

    /// <summary>
    /// Captures a sample of the current metrics state, if the time is right for it.
    /// If it is not the time do capture metrics (e.g. because not enough time has elapsed), this call does nothing.
    /// </summary>
    internal async Task SampleMetricsIfAppropriateAsync(DateTimeOffset now, CancellationToken cancel)
    {
        if (_captureNextSampleOnOrAfter > now)
            return;

        _serializationBuffer.SetLength(0);
        await _metricsRegistry.CollectAndExportAsTextAsync(_serializationBuffer, ExpositionFormat.PrometheusText, cancel);
        var prometheusMetrics = Encoding.UTF8.GetString(_serializationBuffer.GetBuffer(), 0, (int)_serializationBuffer.Length);

        ConsumeMetrics(now, prometheusMetrics);

        await _serializer.FlushAsync(cancel);

        while (_captureNextSampleOnOrAfter <= now)
            _captureNextSampleOnOrAfter += _parameters.MetricsSamplingInterval;
    }

    private void ConsumeMetrics(DateTimeOffset now, string prometheusMetrics)
    {
        foreach (ReadOnlySpan<char> line in prometheusMetrics.SplitLines())
        {
            if (line.Length == 0 || line[0] == '#')
                continue;

            // We need to convert it to a string for regex-processing now.
            // The most we win here, really, is avoiding the allocations for the comment lines.
            var lineAsString = line.ToString();

            // Remaining lines should be one of:
            // metric_name 123.456
            // metric_name{"label1"="value1","label2"="value2"} 123.456e66
            var componentsMatch = ParseLineRegex.Match(lineAsString);

            if (!componentsMatch.Success)
                throw new NotSupportedException($"Failed to parse components from metric line: {lineAsString}");

            var name = componentsMatch.Groups[1].Value;
            var labelsString = componentsMatch.Groups[2].Value;
            var valueString = componentsMatch.Groups[3].Value;

            if (!double.TryParse(valueString, CultureInfo.InvariantCulture, out var value))
                throw new NotSupportedException($"Failed to parse value from metric line: {lineAsString}");

            var labels = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(labelsString))
            {
                var labelsMatches = ParseLabelsRegex.Matches(labelsString);

                foreach (Match match in labelsMatches)
                    labels[match.Groups[1].Value] = match.Groups[2].Value;
            }

            _serializer.WriteMetricPoint(name, now, value, _simulationRunIdentifier, labels);
        }
    }

    private static readonly Regex ParseLineRegex = new Regex(@"^(\w+)({.+})? (.+)$", RegexOptions.Compiled);

    // Loose parsing, assumes no embedded quotes. Multiple matches per string.
    private static readonly Regex ParseLabelsRegex = new Regex(@"""(.+?)""=""(.*?)""", RegexOptions.Compiled);
}
