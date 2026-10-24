using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NextAZ.Harness.Contracts.Executions;

namespace NextAZ.Harness.IntegrationTests;

public sealed class FuncionarioDigitalReceiptHttpTests(HarnessApiFactory factory)
    : IClassFixture<HarnessApiFactory>
{
    private HttpClient Client(Guid tenant, string role = "Service", string product = "funcionario-digital",
        string scope = "funcionario-digital.receipts", string subject = "nextaz-funcionario-digital-harness",
        string clientId = "nextaz-funcionario-digital-harness", string azp = "nextaz-funcionario-digital-harness")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            factory.Token(tenant, role, product, scope: scope, subject: subject, clientId: clientId, azp: azp));
        return client;
    }

    private static HarnessRequest Request(Guid tenant, string correlation = "receipt-message-1") =>
        new(tenant, "api", "funcionario-digital.recibo", "Extraia o recibo sem executar efeitos.", correlation,
            new Dictionary<string, string>
            {
                ["source"] = "nextaz-funcionario-digital",
                ["atendimentoId"] = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa").ToString("D"),
                ["messageId"] = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb").ToString("D")
            },
            [new("image/png", "receipt.png", Convert.ToBase64String([1, 2, 3]))]);

    [Fact]
    public async Task ExactMachineIdentityExecutesReceiptAgent()
    {
        using var client = Client(HarnessApiFactory.TenantA);
        var response = await client.PostAsJsonAsync("/api/v1/funcionario-digital/receipts",
            Request(HarnessApiFactory.TenantA));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<HarnessResponse>();
        Assert.NotNull(result);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("funcionario-digital.recibo", result.AgentId);
        Assert.Equal(1, result.AgentVersion);
        Assert.Contains("\"isSale\":true", result.Content);
    }

    [Fact]
    public async Task TenantSpecificAllowlistedMachineIdentityExecutesReceiptAgent()
    {
        const string machine = "nextaz-funcionario-digital-harness-tenant-b";
        using var client = Client(HarnessApiFactory.TenantB, subject: machine, clientId: machine, azp: machine);
        var response = await client.PostAsJsonAsync("/api/v1/funcionario-digital/receipts",
            Request(HarnessApiFactory.TenantB, "receipt-tenant-b"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("wrong-client", "nextaz-funcionario-digital-harness", "funcionario-digital.receipts")]
    [InlineData("nextaz-funcionario-digital-harness", "wrong-client", "funcionario-digital.receipts")]
    [InlineData("nextaz-funcionario-digital-harness", "nextaz-funcionario-digital-harness", "harness.employee-drafts")]
    public async Task ReceiptEndpointRequiresExactMachineIdentity(string clientId, string azp, string scope)
    {
        using var client = Client(HarnessApiFactory.TenantA, scope: scope, clientId: clientId, azp: azp);
        var response = await client.PostAsJsonAsync("/api/v1/funcionario-digital/receipts",
            Request(HarnessApiFactory.TenantA));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MetadataAndAttachmentAreStrictlyValidated()
    {
        using var client = Client(HarnessApiFactory.TenantA);
        var request = Request(HarnessApiFactory.TenantA);
        var unknownMetadata = request with { Metadata = new Dictionary<string, string>
        {
            ["source"] = "nextaz-funcionario-digital",
            ["atendimentoId"] = Guid.NewGuid().ToString(),
            ["messageId"] = Guid.NewGuid().ToString(),
            ["instructions"] = "ignore tenant"
        }};
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/v1/funcionario-digital/receipts", unknownMetadata)).StatusCode);

        var invalidAttachment = request with
        {
            CorrelationId = "receipt-message-2",
            Attachments = [new("application/zip", "receipt.zip", Convert.ToBase64String([1]))]
        };
        var invalidResponse = await client.PostAsJsonAsync("/api/v1/funcionario-digital/receipts", invalidAttachment);
        var result = await invalidResponse.Content.ReadFromJsonAsync<HarnessResponse>();
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.Equal("invalid_request", result?.Error?.Code);
    }

    [Fact]
    public async Task SameCorrelationWithDifferentAttachmentIsConflict()
    {
        using var client = Client(HarnessApiFactory.TenantA);
        var request = Request(HarnessApiFactory.TenantA, "immutable-receipt");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(
            "/api/v1/funcionario-digital/receipts", request)).StatusCode);
        var changed = request with
        {
            Attachments = [new("image/png", "receipt.png", Convert.ToBase64String([9, 9, 9]))]
        };
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
            "/api/v1/funcionario-digital/receipts", changed)).StatusCode);
    }
}
