namespace Not.Application.Authentication.Provider;

public static class NClientAuthenticationSettingsScopeResolver
{
    public static string? ResolveScope(NClientAuthenticationSettings settings)
    {
        var scope = settings.Scope?.Trim();
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        if (scope.Contains("://", StringComparison.Ordinal))
        {
            return scope;
        }

        var audience = ResolveAudience(settings);
        return string.IsNullOrWhiteSpace(audience) ? null : $"{audience}/{scope.TrimStart('/')}";
    }

    static string? ResolveAudience(NClientAuthenticationSettings settings)
    {
        var explicitAudience = settings.Audience?.Trim();
        if (!string.IsNullOrWhiteSpace(explicitAudience))
        {
            return explicitAudience;
        }

        var clientId = settings.ResourceClientId?.Trim();
        return string.IsNullOrWhiteSpace(clientId) ? null : $"api://{clientId}";
    }
}
