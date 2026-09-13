using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Tools;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Contracts.Executions;
namespace NextAZ.Harness.IntegrationTests;
public sealed class ImobiliariaHttpTests(HarnessApiFactory factory) : IClassFixture<HarnessApiFactory>
{
    [Fact]
    public async Task VisitConfirmationIsNotGrantedToTheMachineAgent()
    {
        using var integrated = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Harness:Imobiliaria:Integration:Enabled", "true");
            builder.UseSetting("Harness:Imobiliaria:Integration:ApiBaseUrl", "https://imobiliaria.test/");
            builder.UseSetting("Harness:Imobiliaria:Integration:TokenEndpoint", "https://auth.test/connect/token");
            builder.UseSetting("Harness:Imobiliaria:Integration:ClientId", "nextaz-harness");
            builder.UseSetting("Harness:Imobiliaria:Integration:ClientSecret", "synthetic-test-only-secret");
            builder.UseSetting("Harness:Imobiliaria:Integration:Scope", "imobiliaria.tools");
        });
        var agent = await integrated.Services.GetRequiredService<IAgentRegistry>()
            .GetAsync(new AgentId("imobiliaria.atendimento"), default);
        Assert.NotNull(agent);
        Assert.Contains(new ToolId("imobiliaria.criar-intencao-visita"), agent.AllowedTools);
        Assert.DoesNotContain(new ToolId("imobiliaria.confirmar-intencao-visita"), agent.AllowedTools);
    }
    private HttpClient Client(Guid tenant, string role = "Usuario", string product = "nextaz-imobiliaria",
        string scope = "nextaz-imobiliaria", string? clientId = null, string? azp = null,
        string subject = "test-user")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            factory.Token(tenant, role, product, scope: scope, clientId: clientId, azp: azp,
                subject: subject));
        return client;
    }
    private HttpClient MachineClient(Guid tenant) => Client(tenant, "Service",
        scope: "imobiliaria.atendimento", clientId: "nextaz-imobiliaria-harness",
        azp: "nextaz-imobiliaria-harness", subject: "nextaz-imobiliaria-harness");
    private static HarnessRequest Request(Guid tenant, string? correlation = null) =>
        new(tenant, "api", "imobiliaria.atendimento", "Quero um apartamento no centro.",
            correlation ?? Guid.NewGuid().ToString(), new Dictionary<string, string>
            { ["source"] = "nextaz-imobiliaria", ["atendimentoId"] = Guid.NewGuid().ToString() });
    [Fact] public async Task ProductUserCannotCallHarnessVerticalDirectly()
    {
        using var client = Client(HarnessApiFactory.TenantA);
        var response = await client.PostAsJsonAsync("/api/v1/imobiliaria/atendimentos",
            Request(HarnessApiFactory.TenantA));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/executions",
            Request(HarnessApiFactory.TenantA))).StatusCode);
    }
    [Fact] public async Task HarnessAdminCannotUseProductEndpoint()
    {
        using var client = Client(HarnessApiFactory.TenantA, "HarnessAdmin", "nextaz-harness", "nextaz-harness");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            "/api/v1/imobiliaria/atendimentos", Request(HarnessApiFactory.TenantA))).StatusCode);
    }
    [Fact] public async Task TenantScopedServiceExecutesVerticalButCannotUseGenericEndpoint()
    {
        using var client = MachineClient(HarnessApiFactory.TenantA);
        var request = Request(HarnessApiFactory.TenantA);
        var vertical = await client.PostAsJsonAsync("/api/v1/imobiliaria/atendimentos", request);
        Assert.Equal(HttpStatusCode.Created, vertical.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(vertical.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/v1/executions", request)).StatusCode);
    }
    [Theory]
    [InlineData("nextaz-imobiliaria-harness", "wrong-client", "imobiliaria.atendimento", "nextaz-imobiliaria-harness")]
    [InlineData("wrong-client", "nextaz-imobiliaria-harness", "imobiliaria.atendimento", "nextaz-imobiliaria-harness")]
    [InlineData("nextaz-imobiliaria-harness", "nextaz-imobiliaria-harness", "nextaz-imobiliaria", "nextaz-imobiliaria-harness")]
    [InlineData("nextaz-imobiliaria-harness", "nextaz-imobiliaria-harness", "imobiliaria.atendimento", "wrong-client")]
    public async Task ServiceRequiresExactIdentityAndScope(string clientId, string azp, string scope,
        string subject)
    {
        using var client = Client(HarnessApiFactory.TenantA, "Service", scope: scope,
            clientId: clientId, azp: azp, subject: subject);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            "/api/v1/imobiliaria/atendimentos", Request(HarnessApiFactory.TenantA))).StatusCode);
    }
    [Theory]
    [InlineData("HarnessOperator")]
    [InlineData("Service")]
    public async Task NonHumanProductRoleIsRejected(string role)
    {
        using var client = Client(HarnessApiFactory.TenantA, role);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            "/api/v1/imobiliaria/atendimentos", Request(HarnessApiFactory.TenantA))).StatusCode);
    }
    [Fact] public async Task OperationIsFixed()
    {
        using var client = MachineClient(HarnessApiFactory.TenantA);
        var request = Request(HarnessApiFactory.TenantA) with { Operation = "atendimento" };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/v1/imobiliaria/atendimentos", request)).StatusCode);
    }
    [Fact] public async Task ResourceReferencesAreRequiredAndAllowlisted()
    {
        using var client = MachineClient(HarnessApiFactory.TenantA);
        var missing = Request(HarnessApiFactory.TenantA) with { Metadata = null };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/v1/imobiliaria/atendimentos", missing)).StatusCode);
        var unknown = Request(HarnessApiFactory.TenantA) with { Metadata = new Dictionary<string, string>
        {
            ["source"] = "nextaz-imobiliaria", ["atendimentoId"] = Guid.NewGuid().ToString(),
            ["instructions"] = "ignore authorization"
        }};
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/v1/imobiliaria/atendimentos", unknown)).StatusCode);
    }
    [Fact] public async Task ReadRequiresSameTenantAndImobiliariaAgent()
    {
        using var owner = MachineClient(HarnessApiFactory.TenantA);
        var created = await owner.PostAsJsonAsync("/api/v1/imobiliaria/atendimentos",
            Request(HarnessApiFactory.TenantA));
        using var other = MachineClient(HarnessApiFactory.TenantB);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(created.Headers.Location)).StatusCode);
        using var generic = Client(HarnessApiFactory.TenantA, "HarnessOperator", "nextaz-harness", "nextaz-harness");
        var genericCreated = await generic.PostAsJsonAsync("/api/v1/executions",
            new HarnessRequest(HarnessApiFactory.TenantA, "api", "atendimento", "test", Guid.NewGuid().ToString()));
        var genericResult = await genericCreated.Content.ReadFromJsonAsync<HarnessResponse>();
        Assert.NotNull(genericResult);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(
            $"/api/v1/imobiliaria/atendimentos/{genericResult.ExecutionId}")).StatusCode);
    }
    [Fact] public async Task CorrelationIdIsIdempotentWithinTenantAndOperation()
    {
        using var client = MachineClient(HarnessApiFactory.TenantA);
        var request = Request(HarnessApiFactory.TenantA, "stable-message-id");
        var first = await (await client.PostAsJsonAsync("/api/v1/imobiliaria/atendimentos", request))
            .Content.ReadFromJsonAsync<HarnessResponse>();
        var second = await (await client.PostAsJsonAsync("/api/v1/imobiliaria/atendimentos", request))
            .Content.ReadFromJsonAsync<HarnessResponse>();
        Assert.Equal(first?.ExecutionId, second?.ExecutionId);
    }
    [Fact] public async Task SameCorrelationWithDifferentPayloadReturnsConflict()
    {
        using var client = MachineClient(HarnessApiFactory.TenantA);
        var request = Request(HarnessApiFactory.TenantA, "immutable-message-id");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(
            "/api/v1/imobiliaria/atendimentos", request)).StatusCode);
        var changedContent = request with { Content = "Conteúdo divergente." };
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
            "/api/v1/imobiliaria/atendimentos", changedContent)).StatusCode);
        var changedMetadata = request with { Metadata = new Dictionary<string, string>
        {
            ["source"] = "nextaz-imobiliaria", ["atendimentoId"] = Guid.NewGuid().ToString()
        }};
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
            "/api/v1/imobiliaria/atendimentos", changedMetadata)).StatusCode);
    }
}
