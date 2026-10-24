using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace NextAZ.Harness.Api.Configuration;

public static class HarnessKeyVaultConfiguration
{
    internal const string ModelSecretName = "Harness--Providers--OpenAI--Model";
    internal const string ModelConfigurationKey = "Harness:Providers:OpenAI:Model";

    public static ConfigurationManager AddHarnessKeyVault(this ConfigurationManager configuration) =>
        AddHarnessKeyVault(configuration, ReadSecret);

    internal static ConfigurationManager AddHarnessKeyVault(this ConfigurationManager configuration,
        Func<Uri, string, string?> readSecret)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(readSecret);

        if (!configuration.GetValue<bool>("Harness:KeyVault:Enabled"))
            return configuration;

        var vaultUri = ReadVaultUri(configuration["Harness:KeyVault:VaultUri"]);
        var model = readSecret(vaultUri, ModelSecretName);
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException(
                $"Azure Key Vault secret '{ModelSecretName}' is required and cannot be empty.");

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ModelConfigurationKey] = model
        });
        return configuration;
    }

    private static Uri ReadVaultUri(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException(
                "Harness:KeyVault:VaultUri must be an absolute HTTPS Azure Key Vault URI.");
        }

        return uri;
    }

    private static string? ReadSecret(Uri vaultUri, string secretName)
    {
        try
        {
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = true
            });
            var client = new SecretClient(vaultUri, credential);
            return client.GetSecret(secretName).Value.Value;
        }
        catch (Exception exception) when (exception is AuthenticationFailedException or RequestFailedException)
        {
            throw new InvalidOperationException(
                $"Could not load required Harness configuration from Azure Key Vault '{vaultUri.Host}'.",
                exception);
        }
    }
}
