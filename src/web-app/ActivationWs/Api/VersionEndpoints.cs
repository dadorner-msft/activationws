using System.Reflection;

namespace ActivationWs.Api;

/// <summary>
/// Exposes the version of the running build so the static pages can display it.
/// </summary>
public static class VersionEndpoints {
    /// <summary>
    /// Resolved once, because the value cannot change while the process is running.
    /// </summary>
    private static readonly VersionResponse Response = new(ReadInformationalVersion());

    public static IEndpointRouteBuilder MapVersionEndpoint(this IEndpointRouteBuilder builder) {
        builder.MapGet("/api/v1/version", static () => TypedResults.Ok(Response))
            .WithName("GetVersion")
            .WithTags("Meta")
            .WithSummary("Returns the version of the running build.");

        return builder;
    }

    private static string ReadInformationalVersion() {
        Assembly assembly = typeof(VersionEndpoints).Assembly;

        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational)) {
            return assembly.GetName().Version?.ToString(3) ?? "unknown";
        }

        // When the build runs inside a repository the source revision is appended as "+<commit>",
        // which is noise in the footer.
        int plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? informational : informational[..plus];
    }
}

/// <summary>Version of the running build.</summary>
/// <param name="Version">Informational version, e.g. <c>1.0.0</c>.</param>
public sealed record VersionResponse(string Version);
