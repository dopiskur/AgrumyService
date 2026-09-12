using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Agrumy.Api.Diagnostics
{
    /// Replaces MapHealthChecks()'s bare status string with per-check detail so a monitor can tell WHICH dependency is degraded.
    internal static class HealthCheckResponseWriter
    {
        /// AssemblyInformationalVersionAttribute is "version+commit" when the publish command passed -p:SourceRevisionId (CLAUDE.md's deploy recipe does); both are null on a plain local build with no SourceRevisionId set.
        private static readonly (string? Version, string? Commit) BuildInfo = ResolveBuildInfo();

        private static (string?, string?) ResolveBuildInfo()
        {
            string? info = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrEmpty(info))
            {
                return (null, null);
            }
            int plus = info.IndexOf('+');
            return plus < 0 ? (info, null) : (info[..plus], info[(plus + 1)..]);
        }

        public static Task WriteResponse(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            var payload = new
            {
                status = report.Status.ToString(),
                version = BuildInfo.Version,
                commit = BuildInfo.Commit,
                totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 2)
                })
            };

            return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}
