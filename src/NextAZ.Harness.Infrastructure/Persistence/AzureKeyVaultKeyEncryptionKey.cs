using Azure.Core;
using Azure.Security.KeyVault.Keys.Cryptography;
using System.Security.Cryptography;

namespace NextAZ.Harness.Infrastructure.Persistence;

public sealed class AzureKeyVaultKeyEncryptionKey(Uri activeKeyId, TokenCredential credential) : IKeyEncryptionKey
{
    private const string Algorithm = "RSA-OAEP-256";

    public async Task<WrappedDataKey> WrapAsync(byte[] plaintextDataKey,
        CancellationToken cancellationToken)
    {
        var client = new CryptographyClient(activeKeyId, credential);
        var result = await client.WrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, plaintextDataKey,
            cancellationToken);
        ValidateKeyId(new Uri(result.KeyId, UriKind.Absolute));
        return new(result.EncryptedKey, result.KeyId, Algorithm);
    }

    public async Task<byte[]> UnwrapAsync(string keyId, string algorithm, byte[] encryptedDataKey,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(algorithm, Algorithm, StringComparison.Ordinal) ||
            !Uri.TryCreate(keyId, UriKind.Absolute, out var keyUri))
            throw new CryptographicException("Unsupported key encryption metadata.");
        ValidateKeyId(keyUri);
        var client = new CryptographyClient(keyUri, credential);
        var result = await client.UnwrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, encryptedDataKey,
            cancellationToken);
        return result.Key;
    }

    private void ValidateKeyId(Uri keyId)
    {
        var activeSegments = activeKeyId.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var candidateSegments = keyId.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (keyId.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(keyId.UserInfo) ||
            !string.Equals(keyId.Host, activeKeyId.Host, StringComparison.OrdinalIgnoreCase) ||
            activeSegments.Length != 3 || candidateSegments.Length != 3 ||
            !string.Equals(candidateSegments[0], "keys", StringComparison.Ordinal) ||
            !string.Equals(candidateSegments[1], activeSegments[1], StringComparison.Ordinal))
            throw new CryptographicException("The key identifier is outside the configured Key Vault key.");
    }
}
