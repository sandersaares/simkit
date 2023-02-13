namespace Simkit;

/// <summary>
/// Figures out where we are supposed to save our simulation artifacts.
/// </summary>
internal sealed class ArtifactPathProvider
{
    public ArtifactPathProvider(
        SimulationRunIdentifier runIdentifier)
    {
        _runIdentifier = runIdentifier;
    }

    private readonly SimulationRunIdentifier _runIdentifier;

    /// <summary>
    /// The file that will contain all the metrics data generated as part of a simulation (all runs, merged as one compressed file).
    /// </summary>
    public string MetricsExportPath()
    {
        return Path.Combine(Root, _runIdentifier.SimulationId + ".json.gz");
    }

    internal string Root => "SimulationArtifacts";

    private const string RunFormatString = "D3";
}
