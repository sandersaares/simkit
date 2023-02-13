namespace Simkit;

internal static class SimulationArtifacts
{
    public static string GetArtifactsPath(string simulationId) => Path.Combine("SimulationArtifacts", simulationId);

    public static string GetLogFileName(SimulationRunIdentifier simulationRunIdentifier) => Path.Combine($"run-{simulationRunIdentifier.RunIndex:D3}.log");

    public const string MetricsExportFilename = "metrics.json.gz";
}
