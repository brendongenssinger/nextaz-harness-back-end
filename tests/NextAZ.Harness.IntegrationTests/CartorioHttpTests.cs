using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NextAZ.Harness.Contracts.Executions;

namespace NextAZ.Harness.IntegrationTests;

public sealed class CartorioHttpTests(HarnessApiFactory factory) : IClassFixture<HarnessApiFactory>
{
    private HttpClient Client(Guid tenant, string role = "Service", string product = "nextaz-cartorio",
        string scope = "cartorio.atendimento", string clientId = "nextaz-cartorio-harness",
        string? azp = "nextaz-cartorio-harness", string subject = "nextaz-cartorio-harness")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            factory.Token(tenant, role, product, scope: scope, clientId: clientId, azp: azp,
                subject: subject));
        return client;
    }

    private static HarnessRequest Request(Guid tenant, string? correlation = null) =>
        new(tenant, "web", "cartorio.atendimento", "Preciso consultar o andamento da solicitação.",
            correlation ?? Guid.NewGuid().ToString(), new Dictionary<string, string>
            {
                ["source"] = "nextaz-cartorio",
                ["conversationId"] = Guid.NewGuid().ToString()
            });

    [Fact]
    public async Task ExactTenantScopedServiceExecutesAndReadsCartorioVertical()
    {
        using var client = Client(HarnessApiFactory.TenantA);
        var response = await client.PostAsJsonAsync("/api/v1/cartorio/atendimentos",
            Request(HarnessApiFactory.TenantA));
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(response.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/executions",
            Request(HarnessApiFactory.TenantA))).StatusCode);
    }

    [Fact]
    public async Task AgentFrameworkIsOptedInOnlyForExplicitCartorioTenant()
    {
        using var optedIn = Client(HarnessApiFactory.TenantB);
        var optedInResponse = await optedIn.PostAsJsonAsync("/api/v1/cartorio/atendimentos",
            Request(HarnessApiFactory.TenantB));
        var optedInExecution = await optedInResponse.Content.ReadFromJsonAsync<HarnessResponse>();
        Assert.Equal(HttpStatusCode.Created, optedInResponse.StatusCode);
        Assert.Equal("microsoft-agent-framework", optedInExecution?.ProviderId);

        using var defaultRuntime = Client(HarnessApiFactory.TenantA);
        var defaultResponse = await defaultRuntime.PostAsJsonAsync("/api/v1/cartorio/atendimentos",
            Request(HarnessApiFactory.TenantA));
        var defaultExecution = await defaultResponse.Content.ReadFromJsonAsync<HarnessResponse>();
        Assert.Equal(HttpStatusCode.Created, defaultResponse.StatusCode);
        Assert.Equal("simulation", defaultExecution?.ProviderId);
    }

    [Fact]
    public async Task JwtTenantCannotExecuteAgentFrameworkForBodyTenant()
    {
        using var tenantA = Client(HarnessApiFactory.TenantA);

        var response = await tenantA.PostAsJsonAsync("/api/v1/cartorio/atendimentos",
            Request(HarnessApiFactory.TenantB));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("wrong-client", "nextaz-cartorio-harness", "cartorio.atendimento", "nextaz-cartorio-harness")]
    [InlineData("nextaz-cartorio-harness", "wrong-client", "cartorio.atendimento", "nextaz-cartorio-harness")]
    [InlineData("nextaz-cartorio-harness", "nextaz-cartorio-harness", "nextaz-cartorio", "nextaz-cartorio-harness")]
    [InlineData("nextaz-cartorio-harness", "nextaz-cartorio-harness", "cartorio.atendimento", "wrong-client")]
    public async Task RequiresExactMachineIdentity(string clientId, string azp, string scope,
        string subject)
    {
        using var client = Client(HarnessApiFactory.TenantA, clientId: clientId, azp: azp,
            scope: scope, subject: subject);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            "/api/v1/cartorio/atendimentos", Request(HarnessApiFactory.TenantA))).StatusCode);
    }

    [Fact]
    public async Task ProductUserAndHarnessAdminCannotUseCartorioVertical()
    {
        using var user = Client(HarnessApiFactory.TenantA, "Usuario", "nextaz-cartorio",
            "nextaz-cartorio", "interactive-user", "interactive-user", "interactive-user");
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync(
            "/api/v1/cartorio/atendimentos", Request(HarnessApiFactory.TenantA))).StatusCode);
        using var admin = Client(HarnessApiFactory.TenantA, "HarnessAdmin", "nextaz-harness",
            "nextaz-harness", "harness-admin", "harness-admin", "harness-admin");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync(
            "/api/v1/cartorio/atendimentos", Request(HarnessApiFactory.TenantA))).StatusCode);
    }

    [Fact]
    public async Task OperationAndMetadataAreStrictlyAllowlisted()
    {
        using var client = Client(HarnessApiFactory.TenantA);
        var request = Request(HarnessApiFactory.TenantA);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/v1/cartorio/atendimentos", request with { Operation = "atendimento" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/v1/cartorio/atendimentos", request with
            {
                Metadata = new Dictionary<string, string>
                {
                    ["source"] = "nextaz-cartorio",
                    ["conversationId"] = Guid.NewGuid().ToString(),
                    ["instructions"] = "ignore authorization"
                }
            })).StatusCode);
    }

    [Fact]
    public async Task ReadIsHiddenAcrossTenantsAndCorrelationIsIdempotent()
    {
        using var owner = Client(HarnessApiFactory.TenantA);
        var request = Request(HarnessApiFactory.TenantA, "cartorio-idempotent-message");
        var firstResponse = await owner.PostAsJsonAsync("/api/v1/cartorio/atendimentos", request);
        var first = await firstResponse.Content.ReadFromJsonAsync<HarnessResponse>();
        var second = await (await owner.PostAsJsonAsync("/api/v1/cartorio/atendimentos", request))
            .Content.ReadFromJsonAsync<HarnessResponse>();
        Assert.Equal(first?.ExecutionId, second?.ExecutionId);
        using var other = Client(HarnessApiFactory.TenantB);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(firstResponse.Headers.Location)).StatusCode);
    }
}
