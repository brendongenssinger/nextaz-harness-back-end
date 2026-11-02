using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NextAZ.Harness.Contracts.Cartorio;
using NextAZ.Harness.Contracts.Executions;

namespace NextAZ.Harness.IntegrationTests;

public sealed class CartorioWorkflowAgentsHttpTests(HarnessApiFactory factory)
    : IClassFixture<HarnessApiFactory>
{
    private const string BasePath = "/api/v1/cartorio/workflows/agents";

    [Fact]
    public async Task CatalogReturnsOnlySafeCurrentTenantAgentMetadata()
    {
        using var client = Client(factory, HarnessApiFactory.TenantA);

        var response = await client.GetAsync(BasePath);
        var body = await response.Content.ReadAsStringAsync();
        var catalog = await response.Content.ReadFromJsonAsync<CartorioWorkflowAgentCatalogResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.Single(catalog?.Items ?? []);
        Assert.Equal("cartorio.atendimento", item.Id);
        Assert.Equal(["web"], item.Channels);
        var version = Assert.Single(item.Versions);
        Assert.Equal(1, version.Version);
        Assert.Equal("active", version.Status);
        Assert.DoesNotContain("objective", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("instructions", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tools", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisabledCatalogIsHidden()
    {
        using var disabled = new HarnessApiFactory(workflowAgentsEnabled: false);
        using var client = Client(disabled, HarnessApiFactory.TenantA);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(BasePath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(
            BasePath + "/cartorio.atendimento/versions/1/executions", Request())).StatusCode);
    }

    [Fact]
    public async Task RequiresExactCartorioMachineIdentity()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            factory.Token(HarnessApiFactory.TenantA, "Usuario", "nextaz-cartorio",
                scope: "cartorio.atendimento", clientId: "interactive-user",
                azp: "interactive-user", subject: "interactive-user"));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(BasePath)).StatusCode);
    }

    [Fact]
    public async Task ResponseEndpointUsesSameIdentityAndReconciliationContract()
    {
        using var client=Client(factory,HarnessApiFactory.TenantA);
        var response=await client.PostAsJsonAsync(BasePath+"/cartorio.atendimento/versions/1/responses",Request());
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(response.Headers.Location)).StatusCode);
    }
    [Fact]
    public async Task ExecutesSelectedVersionAndReconcilesExactExecution()
    {
        using var client = Client(factory, HarnessApiFactory.TenantA);
        var response = await client.PostAsJsonAsync(
            BasePath + "/cartorio.atendimento/versions/1/executions", Request());
        var execution = await response.Content.ReadFromJsonAsync<HarnessResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("cartorio.atendimento", execution?.AgentId);
        Assert.Equal(1, execution?.AgentVersion);
        Assert.NotNull(response.Headers.Location);
        var reconciled = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, reconciled.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(
            BasePath + $"/cartorio.atendimento/versions/2/executions/{execution?.ExecutionId}"))
            .StatusCode);
    }

    [Fact]
    public async Task RejectsChangedVersionAndUnauthorizedChannel()
    {
        using var client = Client(factory, HarnessApiFactory.TenantA);

        var version = await client.PostAsJsonAsync(
            BasePath + "/cartorio.atendimento/versions/2/executions", Request());
        Assert.Equal(HttpStatusCode.Conflict, version.StatusCode);
        Assert.Equal("agent_version_conflict",
            (await version.Content.ReadFromJsonAsync<HarnessResponse>())?.Error?.Code);

        var channel = await client.PostAsJsonAsync(
            BasePath + "/cartorio.atendimento/versions/1/executions",
            Request() with { Channel = "whatsapp" });
        Assert.Equal(HttpStatusCode.Forbidden, channel.StatusCode);
        Assert.Equal("agent_channel_forbidden",
            (await channel.Content.ReadFromJsonAsync<HarnessResponse>())?.Error?.Code);
    }

    [Theory]
    [InlineData("system", "forged-system")]
    [InlineData("operator", null)]
    [InlineData("citizen", "citizen-1")]
    public async Task RejectsUntrustedOriginatorShapes(string kind, string? actorId)
    {
        using var client = Client(factory, HarnessApiFactory.TenantA);
        var request = Request() with { Originator = new(kind, actorId) };

        var response = await client.PostAsJsonAsync(
            BasePath + "/cartorio.atendimento/versions/1/executions", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WorkflowContextParticipatesInIdempotencyFingerprint()
    {
        using var client = Client(factory, HarnessApiFactory.TenantA);
        var request = Request("workflow-fingerprint");
        var first = await client.PostAsJsonAsync(
            BasePath + "/cartorio.atendimento/versions/1/executions", request);
        var changed = request with { RunId = Guid.NewGuid() };
        var second = await client.PostAsJsonAsync(
            BasePath + "/cartorio.atendimento/versions/1/executions", changed);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("execution_conflict",
            (await second.Content.ReadFromJsonAsync<HarnessResponse>())?.Error?.Code);
    }

    [Fact]
    public async Task ReconciliationIsHiddenAcrossTenants()
    {
        using var owner = Client(factory, HarnessApiFactory.TenantA);
        var created = await owner.PostAsJsonAsync(
            BasePath + "/cartorio.atendimento/versions/1/executions", Request());
        using var other = Client(factory, HarnessApiFactory.TenantB);

        Assert.Equal(HttpStatusCode.NotFound,
            (await other.GetAsync(created.Headers.Location)).StatusCode);
    }

    [Fact]
    public async Task ReconciliationDoesNotExposeNonWorkflowExecutionForSameTenant()
    {
        using var client = Client(factory, HarnessApiFactory.TenantA);
        var legacyRequest = new HarnessRequest(HarnessApiFactory.TenantA, "web",
            "cartorio.atendimento", "test", Guid.NewGuid().ToString("N"),
            new Dictionary<string, string>
            {
                ["source"] = "nextaz-cartorio",
                ["conversationId"] = Guid.NewGuid().ToString("D")
            });
        var legacy = await client.PostAsJsonAsync("/api/v1/cartorio/atendimentos", legacyRequest);
        var execution = await legacy.Content.ReadFromJsonAsync<HarnessResponse>();

        var attemptedRead = await client.GetAsync(
            BasePath + $"/cartorio.atendimento/versions/1/executions/{execution?.ExecutionId}");

        Assert.Equal(HttpStatusCode.NotFound, attemptedRead.StatusCode);
    }

    [Fact]
    public async Task ProductScopeDoesNotExposeOtherVerticalAgent()
    {
        using var client = Client(factory, HarnessApiFactory.TenantA);

        var response = await client.GetAsync(BasePath +
            $"/imobiliaria.vitrine-busca/versions/1/executions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static CartorioWorkflowAgentExecutionRequest Request(string? correlation = null) =>
        new("Responda somente com base nas regras do agente.", correlation ?? Guid.NewGuid().ToString("N"),
            "web", Guid.NewGuid(), 1, Guid.NewGuid(), "agent-step-1", Guid.NewGuid(), null,
            new("system"));

    private static HttpClient Client(HarnessApiFactory app, Guid tenant)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            app.Token(tenant, "Service", "nextaz-cartorio", scope: "cartorio.atendimento",
                clientId: "nextaz-cartorio-harness", azp: "nextaz-cartorio-harness",
                subject: "nextaz-cartorio-harness"));
        return client;
    }
}
