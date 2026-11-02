using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NextAZ.Harness.Application.Administration;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Policies;

namespace NextAZ.Harness.Infrastructure.Persistence;

public sealed class PostgresProviderRoutingAdministration(
    IDbContextFactory<HarnessDbContext> contextFactory,
    IProviderTokenProtector tokenProtector,
    TimeProvider timeProvider) : IProviderRoutingAdministration
{
    private static readonly Regex KeyPattern = new("^[a-z0-9][a-z0-9._-]{1,79}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ProviderIdentifierPattern = new("^[a-zA-Z0-9][a-zA-Z0-9._:-]{1,119}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<ProviderConnectionView> UpsertConnectionAsync(Guid tenantId,
        ProviderConnectionDraft draft, CancellationToken cancellationToken)
    {
        ValidateConnection(tenantId, draft);
        var providerConnectionId = DeterministicConnectionId(tenantId, draft.ConnectionKey);
        var normalizedEndpoint = NormalizeEndpoint(draft.Endpoint);
        if (normalizedEndpoint.Length > 2048)
            throw new ArgumentException("Invalid provider connection.", nameof(draft));
        // Key Vault I/O stays outside the database transaction. The deterministic connection ID
        // keeps authenticated encryption stable even if concurrent creates race on the same key.
        var protectedToken = await tokenProtector.ProtectAsync(tenantId, providerConnectionId,
            draft.ApiKey, cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", cancellationToken);
        var lockKey = $"provider-connection:{tenantId:N}:{draft.ConnectionKey}";
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
        var record = await context.ProviderConnections.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.ConnectionKey == draft.ConnectionKey, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (record is null)
        {
            record = new ProviderConnectionRecord
            {
                TenantId = tenantId,
                ProviderConnectionId = providerConnectionId,
                ConnectionKey = draft.ConnectionKey,
                CreatedAt = now
            };
            context.ProviderConnections.Add(record);
        }
        if (record.ProviderConnectionId != providerConnectionId)
            throw new InvalidOperationException("provider_connection_identity_conflict");
        if (!string.IsNullOrEmpty(record.ProviderId) &&
            (!string.Equals(record.ProviderId, draft.ProviderId, StringComparison.Ordinal) ||
             !string.Equals(record.Endpoint, normalizedEndpoint, StringComparison.Ordinal)))
            throw new InvalidOperationException("provider_connection_identity_immutable");
        record.EncryptedApiKey = protectedToken.Ciphertext;
        record.ApiKeyNonce = protectedToken.Nonce;
        record.ApiKeyTag = protectedToken.Tag;
        record.EncryptedDataKey = protectedToken.EncryptedDataKey;
        record.KeyEncryptionKeyId = protectedToken.KeyEncryptionKeyId;
        record.EncryptionAlgorithm = protectedToken.KeyEncryptionAlgorithm;
        record.ProviderId = draft.ProviderId;
        record.DisplayName = draft.DisplayName.Trim();
        record.Endpoint = normalizedEndpoint;
        record.Enabled = draft.Enabled;
        record.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(record.ProviderConnectionId, record.ConnectionKey, record.ProviderId,
            record.DisplayName, record.Endpoint, record.Enabled);
    }

    public async Task ReplaceAgentRoutesAsync(Guid tenantId, string agentId, int agentVersion,
        IReadOnlyList<AgentModelRouteDraft> routes, CancellationToken cancellationToken)
    {
        AgentId parsedAgentId;
        try { parsedAgentId = new AgentId(agentId); }
        catch (ArgumentException) { throw new ArgumentException("Invalid agent model routes.", nameof(routes)); }
        var policies = routes.Select(route => route.ModelPolicy).Distinct(StringComparer.Ordinal).ToArray();
        if (tenantId == Guid.Empty || agentVersion < 1 || policies.Length != 1 ||
            routes.Count is < 1 or > 4 || routes.Any(route => route.ProviderConnectionId == Guid.Empty ||
                !IsValidModelPolicy(route.ModelPolicy) || string.IsNullOrWhiteSpace(route.ModelId) ||
                route.ModelId.Length > 200 || route.TimeoutSeconds is < 1 or > 300))
            throw new ArgumentException("Invalid agent model routes.", nameof(routes));
        var modelPolicy = policies[0];
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", cancellationToken);
        var lockKey = $"agent-model-routes:{tenantId:N}:{parsedAgentId.Value}:{agentVersion}:{modelPolicy}";
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
        var connectionIds = routes.Select(route => route.ProviderConnectionId).Distinct().ToArray();
        var ownedConnections = await context.ProviderConnections.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.Enabled &&
                connectionIds.Contains(item.ProviderConnectionId))
            .Select(item => item.ProviderConnectionId).ToArrayAsync(cancellationToken);
        if (ownedConnections.Length != connectionIds.Length)
            throw new InvalidOperationException("provider_connection_not_available");
        var existing = await context.AgentModelRoutes.AsNoTracking()
            .Where(route => route.TenantId == tenantId && route.AgentId == parsedAgentId.Value &&
                route.AgentVersion == agentVersion && route.ModelPolicy == modelPolicy)
            .OrderBy(route => route.Priority)
            .ToArrayAsync(cancellationToken);
        if (existing.Length > 0)
        {
            if (RouteChainMatches(existing, routes))
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            throw new InvalidOperationException("agent_model_route_immutable");
        }
        var now = timeProvider.GetUtcNow();
        for (var index = 0; index < routes.Count; index++)
        {
            var route = routes[index];
            context.AgentModelRoutes.Add(new AgentModelRouteRecord
            {
                TenantId = tenantId,
                AgentId = parsedAgentId.Value,
                AgentVersion = agentVersion,
                Priority = index + 1,
                ProviderConnectionId = route.ProviderConnectionId,
                ModelPolicy = route.ModelPolicy,
                ModelId = route.ModelId.Trim(),
                TimeoutSeconds = route.TimeoutSeconds,
                Enabled = route.Enabled,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static void ValidateConnection(Guid tenantId, ProviderConnectionDraft draft)
    {
        if (tenantId == Guid.Empty || !KeyPattern.IsMatch(draft.ConnectionKey) ||
            !ProviderIdentifierPattern.IsMatch(draft.ProviderId) || string.IsNullOrWhiteSpace(draft.DisplayName) ||
            draft.DisplayName.Length > 120 || string.IsNullOrWhiteSpace(draft.ApiKey) || draft.ApiKey.Length > 4096 ||
            !Uri.TryCreate(draft.Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(endpoint.UserInfo))
            throw new ArgumentException("Invalid provider connection.", nameof(draft));
    }

    private static bool IsValidModelPolicy(string value)
    {
        try { _ = new ModelPolicy(value); return true; }
        catch (ArgumentException) { return false; }
    }

    private static bool RouteChainMatches(IReadOnlyList<AgentModelRouteRecord> existing,
        IReadOnlyList<AgentModelRouteDraft> requested) =>
        existing.Count == requested.Count && existing.Select((route, index) =>
            route.Priority == index + 1 &&
            route.ProviderConnectionId == requested[index].ProviderConnectionId &&
            route.ModelPolicy == requested[index].ModelPolicy &&
            route.ModelId == requested[index].ModelId.Trim() &&
            route.TimeoutSeconds == requested[index].TimeoutSeconds &&
            route.Enabled == requested[index].Enabled).All(matches => matches);

    private static string NormalizeEndpoint(string value)
    {
        var endpoint = new Uri(value, UriKind.Absolute).AbsoluteUri;
        return endpoint.EndsWith("/", StringComparison.Ordinal) ? endpoint : endpoint + "/";
    }

    private static Guid DeterministicConnectionId(Guid tenantId, string connectionKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"nextaz-harness-provider-connection-v1:{tenantId:N}:{connectionKey}"));
        hash[6] = (byte)((hash[6] & 0x0f) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return new Guid(hash.AsSpan(0, 16));
    }
}
