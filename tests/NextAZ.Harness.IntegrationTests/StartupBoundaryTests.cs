using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using NextAZ.Harness.Api.Configuration;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.ModelRouting;
namespace NextAZ.Harness.IntegrationTests;
public sealed class StartupBoundaryTests
{
    [Fact] public void RefusesProductionWithFoundationAdapters()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Production requires", error.Message);
    }

    [Fact]
    public void AllowsProductionWithReceiptAgentAndRequiredAdapters()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Authentication:Authority", "https://auth.example.com");
            builder.UseSetting("Authentication:Audience", "nextaz-harness-api");
            builder.UseSetting("ConnectionStrings:Harness",
                "Host=localhost;Database=harness;Username=harness;Password=test-only");
            builder.UseSetting("Harness:Persistence:Provider", "PostgreSql");
            builder.UseSetting("Harness:Simulation:Enabled", "false");
            builder.UseSetting("Harness:Providers:OpenAI:Enabled", "true");
            builder.UseSetting("Harness:Providers:OpenAI:Endpoint", "https://api.openai.com/v1/");
            builder.UseSetting("Harness:ProviderEncryption:KeyVaultKeyId",
                "https://example.vault.azure.net/keys/nextaz-harness-provider-kek/version");
            builder.UseSetting("Harness:Receipts:Enabled", "true");
            builder.UseSetting("Harness:Receipts:TenantIds:0",
                "11111111-1111-1111-1111-111111111111");
            builder.UseSetting("Harness:Imobiliaria:Enabled", "false");
            builder.UseSetting("Harness:Imobiliaria:Integration:Enabled", "false");
        });

        using var client = factory.CreateClient();
        Assert.NotNull(client);
    }

    [Fact]
    public void ReadinessRejectsRouteWithoutRegisteredProviderAdapter()
    {
        var registeredConnection = Guid.NewGuid();
        var route = new ModelRoute("gemini", "gemini-pro", TimeSpan.FromSeconds(10),
            registeredConnection);
        var readiness = new HarnessRouteReadiness(Array.Empty<IModelProvider>());

        Assert.False(readiness.AreOperational([route]));
        Assert.False(readiness.AreOperational([]));
    }
}
