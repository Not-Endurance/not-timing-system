using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Not.Server.Authentication;

internal static class ServerAuthenticationSettingsHelper
{
    internal static NServerAuthenticationSettings? GetSettings(IConfiguration configuration)
    {
        return configuration
            .GetSection(nameof(NServerAuthenticationSettings))
            .Get<NServerAuthenticationSettings>();
    }

    internal static bool HasTokenValidationConfiguration(NServerAuthenticationSettings? settings)
    {
        return !string.IsNullOrWhiteSpace(ResolveAuthority(settings))
            && ResolveValidAudiences(settings).Count > 0;
    }

    internal static string? ResolveAuthority(NServerAuthenticationSettings? settings)
    {
        var instance = settings?.AuthorityInstance?.TrimEnd('/');
        var tenantId = settings?.AuthorityTenantId?.Trim('/');
        if (string.IsNullOrWhiteSpace(instance) || string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        if (instance.EndsWith("/v2.0", StringComparison.OrdinalIgnoreCase))
        {
            instance = instance[..^"/v2.0".Length];
        }

        if (!instance.EndsWith($"/{tenantId}", StringComparison.OrdinalIgnoreCase))
        {
            instance = $"{instance}/{tenantId}";
        }

        return $"{instance}/v2.0";
    }

    internal static IReadOnlyList<string> ResolveValidAudiences(NServerAuthenticationSettings? settings)
    {
        var audience = ResolveAudience(settings);
        if (string.IsNullOrWhiteSpace(audience))
        {
            return [];
        }

        var validAudiences = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { audience };
        if (audience.StartsWith("api://", StringComparison.OrdinalIgnoreCase))
        {
            validAudiences.Add(audience["api://".Length..]);
        }
        else if (!audience.Contains("://", StringComparison.Ordinal))
        {
            validAudiences.Add($"api://{audience}");
        }

        return validAudiences.ToArray();
    }

    internal static IReadOnlyList<PathString> ResolveAccessTokenQueryPaths(NServerAuthenticationSettings? settings)
    {
        return settings?
                .AccessTokenQueryPaths?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizePath)
                .Distinct()
                .ToArray()
            ?? [];
    }

    static string? ResolveAudience(NServerAuthenticationSettings? settings)
    {
        var explicitAudience = settings?.Audience?.Trim();
        if (!string.IsNullOrWhiteSpace(explicitAudience))
        {
            return explicitAudience;
        }

        var clientId = settings?.ResourceClientId?.Trim();
        return string.IsNullOrWhiteSpace(clientId) ? null : $"api://{clientId}";
    }

    static PathString NormalizePath(string path)
    {
        var trimmedPath = path.Trim();
        return trimmedPath.StartsWith('/') ? new PathString(trimmedPath) : new PathString($"/{trimmedPath}");
    }
}
