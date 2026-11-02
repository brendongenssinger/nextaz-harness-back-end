using System.Security.Cryptography;
using System.Text;
using NextAZ.Harness.Infrastructure.Persistence;

namespace NextAZ.Harness.Infrastructure.Tests;

public sealed class EnvelopeProviderTokenProtectorTests
{
    [Fact]
    public async Task RoundTripKeepsPlaintextOutOfPersistedPayload()
    {
        using var key = new RsaKeyEncryptionKey();
        var protector = new EnvelopeProviderTokenProtector(key);
        var tenant = Guid.NewGuid();
        var connection = Guid.NewGuid();
        const string token = "provider-token-for-test";

        var protectedToken = await protector.ProtectAsync(tenant, connection, token, default);
        var restored = await protector.UnprotectAsync(tenant, connection, protectedToken, default);

        Assert.Equal(token, restored);
        Assert.DoesNotContain(token, Convert.ToBase64String(protectedToken.Ciphertext));
        Assert.DoesNotContain(token, Encoding.UTF8.GetString(protectedToken.EncryptedDataKey));
        Assert.Equal(12, protectedToken.Nonce.Length);
        Assert.Equal(16, protectedToken.Tag.Length);
        Assert.Equal("RSA-OAEP-256", protectedToken.KeyEncryptionAlgorithm);
    }

    [Fact]
    public async Task CiphertextCannotBeMovedAcrossTenantOrConnection()
    {
        using var key = new RsaKeyEncryptionKey();
        var protector = new EnvelopeProviderTokenProtector(key);
        var tenant = Guid.NewGuid();
        var connection = Guid.NewGuid();
        var protectedToken = await protector.ProtectAsync(tenant, connection, "secret", default);

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() =>
            protector.UnprotectAsync(Guid.NewGuid(), connection, protectedToken, default));
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() =>
            protector.UnprotectAsync(tenant, Guid.NewGuid(), protectedToken, default));
    }

    [Fact]
    public async Task ReencryptingSameTokenUsesFreshDataKeyAndNonce()
    {
        using var key = new RsaKeyEncryptionKey();
        var protector = new EnvelopeProviderTokenProtector(key);
        var tenant = Guid.NewGuid();
        var connection = Guid.NewGuid();

        var first = await protector.ProtectAsync(tenant, connection, "same-token", default);
        var second = await protector.ProtectAsync(tenant, connection, "same-token", default);

        Assert.False(first.Nonce.SequenceEqual(second.Nonce));
        Assert.False(first.EncryptedDataKey.SequenceEqual(second.EncryptedDataKey));
    }

    private sealed class RsaKeyEncryptionKey : IKeyEncryptionKey, IDisposable
    {
        private readonly RSA _rsa = RSA.Create(2048);

        public Task<WrappedDataKey> WrapAsync(byte[] plaintextDataKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new WrappedDataKey(
                _rsa.Encrypt(plaintextDataKey, RSAEncryptionPadding.OaepSHA256),
                "https://vault.test/keys/provider-kek/version-1", "RSA-OAEP-256"));
        }

        public Task<byte[]> UnwrapAsync(string keyId, string algorithm, byte[] encryptedDataKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("https://vault.test/keys/provider-kek/version-1", keyId);
            Assert.Equal("RSA-OAEP-256", algorithm);
            return Task.FromResult(_rsa.Decrypt(encryptedDataKey, RSAEncryptionPadding.OaepSHA256));
        }

        public void Dispose() => _rsa.Dispose();
    }
}
