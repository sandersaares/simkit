namespace Simkit;

internal static class SimulationArtifacts
{
    public static string GetArtifactsPath(string simulationId) => Path.Combine("SimulationArtifacts", simulationId);

    public const string MetricsExportFilename = "metrics.json.gz";
}
