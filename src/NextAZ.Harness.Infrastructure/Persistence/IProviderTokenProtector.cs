namespace NextAZ.Harness.Infrastructure.Persistence;

public sealed record ProtectedProviderToken(byte[] Ciphertext, byte[] Nonce, byte[] Tag,
    byte[] EncryptedDataKey, string KeyEncryptionKeyId, string KeyEncryptionAlgorithm);

public interface IProviderTokenProtector
{
    Task<ProtectedProviderToken> ProtectAsync(Guid tenantId, Guid providerConnectionId,
        string plaintextToken, CancellationToken cancellationToken);
    Task<string> UnprotectAsync(Guid tenantId, Guid providerConnectionId,
        ProtectedProviderToken protectedToken, CancellationToken cancellationToken);
}
