using Microsoft.Extensions.DependencyInjection;

namespace Simkit;

public static class ExtensionsForISimulation
{
    // Convenience method to avoid peppering everything with ".Services."
    public static T GetRequiredService<T>(this ISimulation simulation) where T : notnull
        => simulation.Services.GetRequiredService<T>();
}
