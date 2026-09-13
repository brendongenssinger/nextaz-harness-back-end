using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NextAZ.Harness.Contracts.Executions;
namespace NextAZ.Harness.IntegrationTests;
public sealed class HttpExecutionTests(HarnessApiFactory factory) : IClassFixture<HarnessApiFactory>
{
    private HttpClient Client(string? token = null)
    {
        var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    private static HarnessRequest Request(Guid? tenant = null) =>
        new(tenant ?? HarnessApiFactory.TenantA, "api", "atendimento", "test customer content", Guid.NewGuid().ToString());
    [Fact] public async Task HealthClearlyReportsSimulation()
    {
        using var client = Client();
        var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("simulation", await response.Content.ReadAsStringAsync());
    }
    [Fact] public async Task AnonymousExecutionIsRejected()
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/executions", Request())).StatusCode);
    }
    [Fact] public async Task FullDiExecutesAndRetrievesTimeline()
    {
        using var client = Client(factory.Token(HarnessApiFactory.TenantA));
        var request = Request();
        var response = await client.PostAsJsonAsync("/api/v1/executions", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<HarnessResponse>();
        Assert.NotNull(result);
        Assert.Equal("Completed", result.Status);
        Assert.Equal(request.CorrelationId, result.CorrelationId);
        Assert.Equal("simulation", result.ProviderId);
        var detail = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var json = await detail.Content.ReadAsStringAsync();
        Assert.Contains("tool.completed:harness.execution-info", json);
        Assert.DoesNotContain(request.Content, json);
    }
    [Fact] public async Task CannotReadAnotherTenantExecution()
    {
        using var first = Client(factory.Token(HarnessApiFactory.TenantA));
        var response = await first.PostAsJsonAsync("/api/v1/executions", Request());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var other = Client(factory.Token(HarnessApiFactory.TenantB));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(response.Headers.Location)).StatusCode);
    }
    [Fact] public async Task CannotSelectTenantThroughBody()
    {
        using var client = Client(factory.Token(HarnessApiFactory.TenantA));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/v1/executions", Request(HarnessApiFactory.TenantB))).StatusCode);
    }
    [Theory]
    [InlineData("viewer")] [InlineData("product")] [InlineData("scope")]
    [InlineData("missing-tenant")] [InlineData("empty-tenant")] [InlineData("duplicate-tenant")]
    public async Task MissingAuthorizationIsForbidden(string scenario)
    {
        var token = factory.Token(scenario == "missing-tenant" ? null :
                scenario == "empty-tenant" ? Guid.Empty : HarnessApiFactory.TenantA,
            role: scenario == "viewer" ? "HarnessViewer" : "HarnessOperator",
            product: scenario == "product" ? "erp-nextaz" : "nextaz-harness",
            scope: scenario == "scope" ? "erp-nextaz" : "nextaz-harness",
            duplicateTenant: scenario == "duplicate-tenant");
        using var client = Client(token);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/executions", Request())).StatusCode);
    }
    [Theory]
    [InlineData("issuer")] [InlineData("audience")] [InlineData("expired")] [InlineData("signature")]
    public async Task InvalidJwtIsUnauthorized(string scenario)
    {
        using var client = Client(factory.Token(HarnessApiFactory.TenantA,
            issuer: scenario == "issuer" ? "https://untrusted.test" : HarnessApiFactory.Issuer,
            audience: scenario == "audience" ? "erp-nextaz-api" : "nextaz-harness-api",
            expired: scenario == "expired", wrongSignature: scenario == "signature"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/executions", Request())).StatusCode);
    }
    [Fact] public async Task MalformedRequestFailsValidation()
    {
        using var client = Client(factory.Token(HarnessApiFactory.TenantA));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/v1/executions", Request() with { Content = "" })).StatusCode);
    }
    [Fact] public async Task ViewerMayReadOwnTenantExecution()
    {
        using var creator = Client(factory.Token(HarnessApiFactory.TenantA));
        var created = await creator.PostAsJsonAsync("/api/v1/executions", Request());
        using var viewer = Client(factory.Token(HarnessApiFactory.TenantA, role: "HarnessViewer"));
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(created.Headers.Location)).StatusCode);
    }
}
