using System.Security.Cryptography;
using System.Text;

namespace NextAZ.Harness.Infrastructure.Persistence;

public sealed class EnvelopeProviderTokenProtector(IKeyEncryptionKey keyEncryptionKey) : IProviderTokenProtector
{
    private const int DataKeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public async Task<ProtectedProviderToken> ProtectAsync(Guid tenantId, Guid providerConnectionId,
        string plaintextToken, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || providerConnectionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(plaintextToken))
            throw new ArgumentException("A tenant-scoped provider token is required.", nameof(plaintextToken));
        var dataKey = RandomNumberGenerator.GetBytes(DataKeySize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var plaintext = Encoding.UTF8.GetBytes(plaintextToken);
        var ciphertext = new byte[plaintext.Length];
        try
        {
            using (var aes = new AesGcm(dataKey, TagSize))
                aes.Encrypt(nonce, plaintext, ciphertext, tag, AssociatedData(tenantId, providerConnectionId));
            var wrapped = await keyEncryptionKey.WrapAsync(dataKey, cancellationToken);
            return new(ciphertext, nonce, tag, wrapped.Ciphertext, wrapped.KeyId, wrapped.Algorithm);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public async Task<string> UnprotectAsync(Guid tenantId, Guid providerConnectionId,
        ProtectedProviderToken protectedToken, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || providerConnectionId == Guid.Empty ||
            protectedToken.Nonce.Length != NonceSize || protectedToken.Tag.Length != TagSize ||
            protectedToken.Ciphertext.Length == 0 || protectedToken.EncryptedDataKey.Length == 0)
            throw new CryptographicException("Invalid encrypted provider credential.");
        var dataKey = await keyEncryptionKey.UnwrapAsync(protectedToken.KeyEncryptionKeyId,
            protectedToken.KeyEncryptionAlgorithm, protectedToken.EncryptedDataKey, cancellationToken);
        var plaintext = new byte[protectedToken.Ciphertext.Length];
        try
        {
            if (dataKey.Length != DataKeySize)
                throw new CryptographicException("Invalid provider data key.");
            using (var aes = new AesGcm(dataKey, TagSize))
                aes.Decrypt(protectedToken.Nonce, protectedToken.Ciphertext, protectedToken.Tag,
                    plaintext, AssociatedData(tenantId, providerConnectionId));
            return new UTF8Encoding(false, true).GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] AssociatedData(Guid tenantId, Guid providerConnectionId) =>
        Encoding.UTF8.GetBytes($"nextaz-harness-provider-token-v1:{tenantId:N}:{providerConnectionId:N}");
}
