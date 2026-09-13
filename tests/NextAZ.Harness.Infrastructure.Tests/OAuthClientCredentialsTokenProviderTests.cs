using System.Net;
using System.Text;
using NextAZ.Harness.Infrastructure.Imobiliaria;
namespace NextAZ.Harness.Infrastructure.Tests;
public sealed class OAuthClientCredentialsTokenProviderTests
{
    [Fact] public async Task SendsTenantScopedClientCredentialsAndCachesToken()
    {
        var handler = new TokenHandler(HttpStatusCode.OK,
            "{\"access_token\":\"issued-token\",\"token_type\":\"Bearer\",\"expires_in\":300}");
        var provider = new OAuthClientCredentialsTokenProvider(new HttpClient(handler), Options(), TimeProvider.System);
        var tenant = Guid.NewGuid();
        Assert.Equal("issued-token", await provider.GetAsync(tenant, default));
        Assert.Equal("issued-token", await provider.GetAsync(tenant, default));
        Assert.Equal(1, handler.Calls);
        Assert.Contains("grant_type=client_credentials", handler.Body);
        Assert.Contains("scope=imobiliaria.tools", Uri.UnescapeDataString(handler.Body!));
        Assert.Contains("tenant_id=" + tenant, Uri.UnescapeDataString(handler.Body!));
    }
    [Fact] public async Task TokenFailureDoesNotExposeSecret()
    {
        var provider = new OAuthClientCredentialsTokenProvider(new HttpClient(
            new TokenHandler(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_client\"}")), Options(),
            TimeProvider.System);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync(Guid.NewGuid(), default));
        Assert.DoesNotContain("client-secret", error.Message);
    }
    private static ImobiliariaIntegrationOptions Options() => ImobiliariaIntegrationOptions.Create(
        "https://imobiliaria.test/", "https://auth.test/connect/token", "nextaz-harness",
        "client-secret", "imobiliaria.tools");
    private sealed class TokenHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new(status) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        }
    }
}
