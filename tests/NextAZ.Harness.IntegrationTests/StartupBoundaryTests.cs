using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using NextAZ.Harness.Api.Configuration;
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
    public void RefusesProductionOpenAiWithoutDirectKeyVaultConfiguration()
    {
        var configuration = new ConfigurationManager();
        configuration["Harness:Providers:OpenAI:Enabled"] = "true";
        var services = new ServiceCollection();

        var error = Assert.Throws<InvalidOperationException>(() =>
            services.AddHarnessFoundation(configuration, new ProductionEnvironment()));

        Assert.Contains("requires direct Azure Key Vault", error.Message);
    }

    private sealed class ProductionEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "NextAZ.Harness.IntegrationTests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Production";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
