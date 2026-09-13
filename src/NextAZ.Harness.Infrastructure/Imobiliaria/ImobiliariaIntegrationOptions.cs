namespace NextAZ.Harness.Infrastructure.Imobiliaria;
public sealed record ImobiliariaIntegrationOptions(Uri ApiBaseUrl, Uri TokenEndpoint,
    string ClientId, string ClientSecret, string Scope)
{
    public static ImobiliariaIntegrationOptions Create(string? apiBaseUrl, string? tokenEndpoint,
        string? clientId, string? clientSecret, string? scope, bool allowHttpLoopback = false)
    {
        if (!ValidEndpoint(apiBaseUrl, allowHttpLoopback, out var api) ||
            !ValidEndpoint(tokenEndpoint, allowHttpLoopback, out var token))
            throw new InvalidOperationException("Configure HTTPS imobiliaria API and OAuth token endpoints.");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) ||
            !string.Equals(scope, "imobiliaria.tools", StringComparison.Ordinal))
            throw new InvalidOperationException("Configure the restricted imobiliaria.tools service client.");
        return new(api, token, clientId, clientSecret, scope!);
    }
    private static bool ValidEndpoint(string? value, bool allowHttpLoopback, out Uri uri) =>
        Uri.TryCreate(value, UriKind.Absolute, out uri!) &&
        (uri.Scheme == Uri.UriSchemeHttps || allowHttpLoopback && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
}
