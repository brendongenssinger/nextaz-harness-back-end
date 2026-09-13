using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
namespace NextAZ.Harness.Infrastructure.Imobiliaria;
public sealed class OAuthClientCredentialsTokenProvider(HttpClient httpClient,
    ImobiliariaIntegrationOptions options, TimeProvider timeProvider) : IImobiliariaAccessTokenProvider
{
    private readonly ConcurrentDictionary<Guid, CachedAccessToken> _cache = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task<string> GetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (TryCached(tenantId, out var token)) return token;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (TryCached(tenantId, out token)) return token;
            using var request = new HttpRequestMessage(HttpMethod.Post, options.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = options.ClientId,
                    ["client_secret"] = options.ClientSecret,
                    ["scope"] = options.Scope,
                    ["tenant_id"] = tenantId.ToString()
                })
            };
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("The imobiliaria service token could not be acquired.");
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (!payload.TryGetProperty("access_token", out var accessToken) ||
                accessToken.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(accessToken.GetString()) ||
                !payload.TryGetProperty("expires_in", out var expiresIn) || !expiresIn.TryGetInt32(out var seconds) ||
                seconds is < 60 or > 86400)
                throw new InvalidOperationException("The imobiliaria service token response is invalid.");
            token = accessToken.GetString()!;
            _cache[tenantId] = new(token, timeProvider.GetUtcNow().AddSeconds(seconds - 30));
            return token;
        }
        finally { _gate.Release(); }
    }
    private bool TryCached(Guid tenantId, out string token)
    {
        if (_cache.TryGetValue(tenantId, out var cached) && cached.ExpiresAt > timeProvider.GetUtcNow())
        { token = cached.Token; return true; }
        token = string.Empty;
        return false;
    }
    private sealed record CachedAccessToken(string Token, DateTimeOffset ExpiresAt);
}
