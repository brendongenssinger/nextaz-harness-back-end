using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NextAZ.Harness.Contracts.Administration;
using NextAZ.Harness.Contracts.Executions;

namespace NextAZ.Harness.IntegrationTests;

public sealed class HarnessAdministrationHttpTests(HarnessApiFactory factory)
    : IClassFixture<HarnessApiFactory>
{
    [Fact]
    public async Task Admin_reads_runtime_catalog_for_own_tenant()
    {
        using var client = CreateClient(factory.Token(
            HarnessApiFactory.TenantA,
            role: "HarnessAdmin"));

        var response = await client.GetAsync(
            $"/api/v1/admin/tenants/{HarnessApiFactory.TenantA:D}/overview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var overview = await response.Content.ReadFromJsonAsync<HarnessAdministrationOverview>();
        Assert.NotNull(overview);
        Assert.Equal(HarnessApiFactory.TenantA, overview.TenantId);
        Assert.Contains(overview.Agents, agent =>
            agent.AgentId == "cartorio.atendimento"
            && agent.TenantConfigured
            && agent.ModelRoutes.Count > 0);
    }

    [Fact]
    public async Task Admin_cannot_read_another_tenant_from_route()
    {
        using var client = CreateClient(factory.Token(
            HarnessApiFactory.TenantA,
            role: "HarnessAdmin"));

        var response = await client.GetAsync(
            $"/api/v1/admin/tenants/{HarnessApiFactory.TenantB:D}/overview");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_reads_tenant_scoped_usage_grouped_by_agent_and_provider()
    {
        using var client = CreateClient(factory.Token(
            HarnessApiFactory.TenantA,
            role: "HarnessAdmin"));
        var request = new HarnessRequest(
            HarnessApiFactory.TenantA,
            "api",
            "atendimento",
            "usage report test",
            Guid.NewGuid().ToString());
        var execution = await client.PostAsJsonAsync("/api/v1/executions", request);
        Assert.Equal(HttpStatusCode.Created, execution.StatusCode);

        var response = await client.GetAsync(
            $"/api/v1/admin/tenants/{HarnessApiFactory.TenantA:D}/usage");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<HarnessUsageReport>();
        Assert.NotNull(report);
        Assert.Equal(HarnessApiFactory.TenantA, report.TenantId);
        Assert.True(report.TotalExecutions >= 1);
        Assert.Contains(report.Groups, group =>
            group.AgentId == "cartorio.atendimento" &&
            group.Operation == "atendimento" &&
            group.Provider == "simulation");
    }

    [Fact]
    public async Task Admin_cannot_read_another_tenant_usage()
    {
        using var client = CreateClient(factory.Token(
            HarnessApiFactory.TenantA,
            role: "HarnessAdmin"));

        var response = await client.GetAsync(
            $"/api/v1/admin/tenants/{HarnessApiFactory.TenantB:D}/usage");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Usage_rejects_periods_longer_than_the_operational_limit()
    {
        using var client = CreateClient(factory.Token(
            HarnessApiFactory.TenantA,
            role: "HarnessAdmin"));
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-94).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));

        var response = await client.GetAsync(
            $"/api/v1/admin/tenants/{HarnessApiFactory.TenantA:D}/usage?fromUtc={from}&toUtc={to}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient CreateClient(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
