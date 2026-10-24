using Microsoft.Extensions.Configuration;
using NextAZ.Harness.Api.Configuration;

namespace NextAZ.Harness.IntegrationTests;

public sealed class HarnessKeyVaultConfigurationTests
{
    [Fact]
    public void DisabledKeyVaultDoesNotReadSecrets()
    {
        var configuration = new ConfigurationManager();
        var called = false;

        configuration.AddHarnessKeyVault((_, _) =>
        {
            called = true;
            return "model";
        });

        Assert.False(called);
        Assert.Null(configuration[HarnessKeyVaultConfiguration.ModelConfigurationKey]);
    }

    [Fact]
    public void LoadsOnlyTheRequiredModelSecretAndOverridesLocalValue()
    {
        var configuration = EnabledConfiguration();
        configuration[HarnessKeyVaultConfiguration.ModelConfigurationKey] = "local-model";
        Uri? requestedVault = null;
        string? requestedSecret = null;

        configuration.AddHarnessKeyVault((vault, secret) =>
        {
            requestedVault = vault;
            requestedSecret = secret;
            return "vault-model";
        });

        Assert.Equal("https://nextaz-kv-prod-70bec273.vault.azure.net/", requestedVault!.AbsoluteUri);
        Assert.Equal(HarnessKeyVaultConfiguration.ModelSecretName, requestedSecret);
        Assert.Equal("vault-model", configuration[HarnessKeyVaultConfiguration.ModelConfigurationKey]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://nextaz-kv-prod-70bec273.vault.azure.net/")]
    [InlineData("not-a-uri")]
    public void RejectsInvalidVaultUriBeforeReadingSecrets(string vaultUri)
    {
        var configuration = EnabledConfiguration();
        configuration["Harness:KeyVault:VaultUri"] = vaultUri;

        var error = Assert.Throws<InvalidOperationException>(() =>
            configuration.AddHarnessKeyVault((_, _) => throw new Xunit.Sdk.XunitException("must not connect")));

        Assert.Contains("absolute HTTPS", error.Message);
    }

    [Fact]
    public void RejectsEmptyRequiredSecret()
    {
        var configuration = EnabledConfiguration();

        var error = Assert.Throws<InvalidOperationException>(() =>
            configuration.AddHarnessKeyVault((_, _) => "  "));

        Assert.Contains(HarnessKeyVaultConfiguration.ModelSecretName, error.Message);
    }

    private static ConfigurationManager EnabledConfiguration()
    {
        var configuration = new ConfigurationManager();
        configuration["Harness:KeyVault:Enabled"] = "true";
        configuration["Harness:KeyVault:VaultUri"] =
            "https://nextaz-kv-prod-70bec273.vault.azure.net/";
        return configuration;
    }
}
