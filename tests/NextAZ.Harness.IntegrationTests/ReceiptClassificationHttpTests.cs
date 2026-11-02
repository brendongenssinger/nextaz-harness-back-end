using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NextAZ.Harness.Contracts.Receipts;

namespace NextAZ.Harness.IntegrationTests;

public sealed class ReceiptClassificationHttpTests(HarnessApiFactory factory)
    : IClassFixture<HarnessApiFactory>
{
    private const string ClientId = "nextaz-funcionario-digital-harness";

    [Fact]
    public async Task ExecutesTenantScopedReceiptRouteWithoutConfirmingPayment()
    {
        using var client = Client(HarnessApiFactory.TenantA);
        var response = await client.PostAsJsonAsync("/api/v1/receipt-classifications", Request());
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var result = System.Text.Json.JsonSerializer.Deserialize<ReceiptClassificationResponse>(body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal("Completed", result?.Status);
        Assert.False(result?.Classification?.IsReceipt);
        Assert.False(result?.Classification?.RequiresDetailedExtraction);
        Assert.Equal("deterministic-v1", result?.ModelId);
        Assert.Equal("simulation", result?.ProviderId);
    }

    [Fact]
    public async Task RejectsTenantDifferentFromSignedClaim()
    {
        using var client = Client(HarnessApiFactory.TenantA);
        var response = await client.PostAsJsonAsync("/api/v1/receipt-classifications",
            Request() with { TenantId = HarnessApiFactory.TenantB });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("wrong.scope", "funcionario-digital", "nextaz-funcionario-digital-harness")]
    [InlineData("receipts.classify", "nextaz-harness", "nextaz-funcionario-digital-harness")]
    [InlineData("receipts.classify", "funcionario-digital", "untrusted-client")]
    public async Task RequiresExactServiceContract(string scope, string product, string clientId)
    {
        using var client = Client(HarnessApiFactory.TenantA, scope, product, clientId);
        var response = await client.PostAsJsonAsync("/api/v1/receipt-classifications", Request());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient Client(Guid tenant, string scope = "receipts.classify",
        string product = "funcionario-digital", string clientId = ClientId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            factory.Token(tenant, "Service", product, scope: scope, clientId: clientId,
                azp: clientId, subject: clientId));
        return client;
    }

    private static ClassifyReceiptRequest Request() => new(
        HarnessApiFactory.TenantA,
        Guid.NewGuid(),
        "https://nextazstorage.blob.core.windows.net/midias/receipt.jpg?sig=test-only");
}
