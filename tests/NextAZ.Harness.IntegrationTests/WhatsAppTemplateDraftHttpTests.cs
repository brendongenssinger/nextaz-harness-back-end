using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NextAZ.Harness.Contracts.WhatsAppTemplates;

namespace NextAZ.Harness.IntegrationTests;

public sealed class WhatsAppTemplateDraftHttpTests(HarnessApiFactory factory)
    : IClassFixture<HarnessApiFactory>
{
    private const string ClientId = "nextaz-funcionario-digital-harness";

    [Fact]
    public async Task GeneratesGovernedDraftWithEconomicalAgent()
    {
        using var client = Client(HarnessApiFactory.TenantA);
        var response = await client.PostAsJsonAsync("/api/v1/whatsapp-template-drafts", Request());
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var result = JsonSerializer.Deserialize<WhatsAppTemplateDraftResponse>(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(result?.Draft);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("mensagem_de_exemplo", result.Draft.Name);
        Assert.Equal("pt_BR", result.Draft.Language);
        Assert.Equal("UTILITY", result.Draft.Category);
        Assert.Equal("microsoft-agent-framework", result.ProviderId);
        Assert.Equal("deterministic-v1", result.ModelId);
    }

    [Fact]
    public async Task RejectsTenantDifferentFromSignedClaim()
    {
        using var client = Client(HarnessApiFactory.TenantA);
        var response = await client.PostAsJsonAsync("/api/v1/whatsapp-template-drafts",
            Request() with { TenantId = HarnessApiFactory.TenantB });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PreservesRequestedMarketingCategoryInStructuredDraft()
    {
        using var client = Client(HarnessApiFactory.TenantA);
        var response = await client.PostAsJsonAsync("/api/v1/whatsapp-template-drafts",
            Request() with { Category = "MARKETING" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<WhatsAppTemplateDraftResponse>();
        Assert.Equal("MARKETING", result?.Draft?.Category);
    }

    [Theory]
    [InlineData("wrong.scope", "funcionario-digital", "nextaz-funcionario-digital-harness")]
    [InlineData("whatsapp.templates.generate", "nextaz-harness", "nextaz-funcionario-digital-harness")]
    [InlineData("whatsapp.templates.generate", "funcionario-digital", "untrusted-client")]
    public async Task RequiresExactServiceContract(string scope, string product, string clientId)
    {
        using var client = Client(HarnessApiFactory.TenantA, scope, product, clientId);
        var response = await client.PostAsJsonAsync("/api/v1/whatsapp-template-drafts", Request());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient Client(Guid tenant, string scope = "whatsapp.templates.generate",
        string product = "funcionario-digital", string clientId = ClientId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            factory.Token(tenant, "Service", product, scope: scope, clientId: clientId,
                azp: clientId, subject: clientId));
        return client;
    }

    private static GenerateWhatsAppTemplateRequest Request() => new(
        HarnessApiFactory.TenantA,
        Guid.NewGuid(),
        "Confirmar o recebimento de uma solicitação e informar que ela será analisada.",
        "Clientes que iniciaram atendimento",
        "pt_BR",
        "UTILITY",
        "profissional e acolhedor");
}
