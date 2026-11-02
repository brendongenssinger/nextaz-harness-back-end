namespace NextAZ.Harness.Infrastructure.Persistence;

public sealed record WrappedDataKey(byte[] Ciphertext, string KeyId, string Algorithm);

public interface IKeyEncryptionKey
{
    Task<WrappedDataKey> WrapAsync(byte[] plaintextDataKey, CancellationToken cancellationToken);
    Task<byte[]> UnwrapAsync(string keyId, string algorithm, byte[] encryptedDataKey,
        CancellationToken cancellationToken);
}
