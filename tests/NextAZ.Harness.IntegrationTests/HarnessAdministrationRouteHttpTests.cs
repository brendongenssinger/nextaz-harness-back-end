using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NextAZ.Harness.Contracts.Administration;

namespace NextAZ.Harness.IntegrationTests;

public sealed class HarnessAdministrationRouteHttpTests(HarnessApiFactory factory)
    : IClassFixture<HarnessApiFactory>
{
    private HttpClient Client(string role = "HarnessAdmin", Guid? tenant = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            factory.Token(tenant ?? HarnessApiFactory.TenantA, role, subject: "nextaz-auth-harness-admin"));
        return client;
    }

    [Fact]
    public async Task HarnessAdmin_creates_updates_and_lists_versioned_route()
    {
        using var client = Client();
        var operation = $"tests.route-{Guid.NewGuid():N}";
        var url = $"/api/v1/admin/tenants/{HarnessApiFactory.TenantA:D}/routes";
        var create = new SaveHarnessAdministrationRouteBinding(
            "api", operation, "funcionario-digital.recibo", 1, true, null);

        var createdResponse = await client.PutAsJsonAsync(url, create);
        Assert.Equal(HttpStatusCode.OK, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<HarnessAdministrationRouteBinding>();
        Assert.Equal(1, created?.Revision);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(url, create)).StatusCode);

        var updatedResponse = await client.PutAsJsonAsync(url, create with
        {
            Enabled = false,
            ExpectedRevision = created!.Revision
        });
        var updated = await updatedResponse.Content.ReadFromJsonAsync<HarnessAdministrationRouteBinding>();
        Assert.Equal(2, updated?.Revision);
        Assert.False(updated?.Enabled);

        var overview = await client.GetFromJsonAsync<HarnessAdministrationOverview>(
            $"/api/v1/admin/tenants/{HarnessApiFactory.TenantA:D}/overview");
        Assert.Contains(overview!.Routes, route => route.Operation == operation && route.Revision == 2);
    }

    [Fact]
    public async Task Operator_cannot_change_routes_and_tenant_mismatch_is_forbidden()
    {
        var payload = new SaveHarnessAdministrationRouteBinding(
            "api", $"tests.route-{Guid.NewGuid():N}", "funcionario-digital.recibo", 1, true, null);
        using var operatorClient = Client("HarnessOperator");
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.PutAsJsonAsync(
            $"/api/v1/admin/tenants/{HarnessApiFactory.TenantA:D}/routes", payload)).StatusCode);

        using var admin = Client(tenant: HarnessApiFactory.TenantA);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsJsonAsync(
            $"/api/v1/admin/tenants/{HarnessApiFactory.TenantB:D}/routes", payload)).StatusCode);
    }
}
