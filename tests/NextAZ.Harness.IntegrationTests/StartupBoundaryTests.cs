using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
namespace NextAZ.Harness.IntegrationTests;
public sealed class StartupBoundaryTests
{
    [Fact] public void RefusesProductionWithFoundationAdapters()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Production requires", error.Message);
    }
}
