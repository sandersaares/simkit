using System.IO.Compression;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simkit;

namespace Tests;

[TestClass]
public sealed class MetricHistorySerializerTests
{
    [TestMethod]
    public async Task WriteMetricPoint_WritesExpectedOutput()
    {
        await using var buffer = new MemoryStream();
        await using var serializer = new MetricHistorySerializer(buffer);

        var time = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var labels = new Dictionary<string, string>
        {
            { "foo", "bar" }
        };

        var simulationRunIdentifier = new SimulationRunIdentifier("simulation-id", 123);

        await serializer.WriteMetricPointAsync("name1", time, 1, simulationRunIdentifier, labels, CancellationToken.None);
        await serializer.WriteMetricPointAsync("name2", time, 2, simulationRunIdentifier, labels, CancellationToken.None);

        await using var buffer2 = new MemoryStream(buffer.ToArray());
        await using var decompressor = new GZipStream(buffer2, CompressionMode.Decompress);

        await using var buffer3 = new MemoryStream();
        await decompressor.CopyToAsync(buffer3);

        var asString = Encoding.UTF8.GetString(buffer3.ToArray());

        var expected = """{"name":"name1","timestamp":"2000-01-01 00:00:00Z","value":1,"simulation_id":"simulation-id","run":123,"foo":"bar"},{"name":"name2","timestamp":"2000-01-01 00:00:00Z","value":2,"simulation_id":"simulation-id","run":123,"foo":"bar"}""";

        Assert.AreEqual(expected, asString);
    }
}
