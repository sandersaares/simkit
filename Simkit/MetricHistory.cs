using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Prometheus;

namespace Simkit;

/// <summary>
/// Observes the metrics in a metric registry and exports them for Azure Data Explorer analysis at a configured sampling interval.
/// </summary>
/// <remarks>
/// We only record data on every Nth tick (although N may be 1) because typically for whole-simulation analysis the useful granularity is something realistic.
/// For production scenarios, the typical recording interval is 10-60 seconds, although for debugging simulations 1 second can also make a lot of sense.
/// 
/// The data set generated can be large but ultimatley will be analyzed in Azure Data Explorer, so this is merely a storage & upload convenience concern.
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

    /// <summary>
    /// Captures a sample of the current metrics state, if the time is right for it.
    /// If it is not the time do capture metrics (e.g. because not enough time has elapsed), this call does nothing.
    /// </summary>
    internal async Task SampleMetricsIfAppropriateAsync(DateTimeOffset now, CancellationToken cancel)
    {
        if (_captureNextSampleOnOrAfter > now)
            return;

        using var buffer = new MemoryStream();
        await _metricsRegistry.CollectAndExportAsTextAsync(buffer, ExpositionFormat.PrometheusText, cancel);
        var serialized = Encoding.UTF8.GetString(buffer.ToArray());

        foreach (var line in serialized.Split('\n'))
        {
            if (line.StartsWith('#'))
                continue;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Remaining lines should be one of:
            // metric_name 123.456
            // metric_name{"label1"="value1","label2"="value2"} 123.456e66
            var componentsMatch = ParseLineRegex.Match(line);

            if (!componentsMatch.Success)
                throw new NotSupportedException($"Failed to parse components from metric line: {line}");

            var name = componentsMatch.Groups[1].Value;
            var labelsString = componentsMatch.Groups[2].Value;
            var valueString = componentsMatch.Groups[3].Value;

            if (!double.TryParse(valueString, CultureInfo.InvariantCulture, out var value))
                throw new NotSupportedException($"Failed to parse value from metric line: {line}");

            var labels = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(labelsString))
            {
                var labelsMatches = ParseLabelsRegex.Matches(labelsString);

                foreach (Match match in labelsMatches)
                    labels[match.Groups[1].Value] = match.Groups[2].Value;
            }

            await _serializer.WriteMetricPointAsync(name, now, value, _simulationRunIdentifier, labels, cancel);
        }

        while (_captureNextSampleOnOrAfter <= now)
            _captureNextSampleOnOrAfter += _parameters.MetricsSamplingInterval;
    }

    private static readonly Regex ParseLineRegex = new Regex(@"^(\w+)({.+})? (.+)$", RegexOptions.Compiled);

    // Loose parsing, assumes no embedded quotes. Multiple matches per string.
    private static readonly Regex ParseLabelsRegex = new Regex(@"""(.+?)""=""(.*?)""", RegexOptions.Compiled);
}
