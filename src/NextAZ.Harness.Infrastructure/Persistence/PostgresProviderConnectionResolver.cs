using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Azure;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.Execution;
using NextAZ.Harness.Runtime.ModelRouting;

namespace NextAZ.Harness.Infrastructure.Persistence;

public sealed class PostgresProviderConnectionResolver(
    IDbContextFactory<HarnessDbContext> contextFactory,
    IProviderTokenProtector tokenProtector) : IProviderConnectionResolver
{
    public async Task<ProviderConnectionCredentials> ResolveAsync(Guid tenantId, Guid providerConnectionId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || providerConnectionId == Guid.Empty)
            throw new HarnessExecutionException("provider_connection_invalid");
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var connection = await context.ProviderConnections.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.ProviderConnectionId == providerConnectionId && item.Enabled,
            cancellationToken);
        if (connection is null || !Uri.TryCreate(connection.Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(endpoint.UserInfo))
            throw new HarnessExecutionException("provider_connection_invalid");
        string apiKey;
        try
        {
            apiKey = await tokenProtector.UnprotectAsync(tenantId, providerConnectionId,
                new(connection.EncryptedApiKey, connection.ApiKeyNonce, connection.ApiKeyTag,
                    connection.EncryptedDataKey, connection.KeyEncryptionKeyId,
                    connection.EncryptionAlgorithm), cancellationToken);
        }
        catch (RequestFailedException exception) when
            (exception.Status is 408 or 429 || exception.Status >= 500)
        {
            throw new ModelProviderUnavailableException();
        }
        catch (CryptographicException)
        {
            throw new HarnessExecutionException("provider_connection_invalid");
        }
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new HarnessExecutionException("provider_connection_invalid");
        return new(connection.ProviderConnectionId, connection.ProviderId, endpoint, apiKey);
    }
}
