using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NextAZ.Harness.Infrastructure.Persistence;

internal sealed class ProviderConnectionRecordConfiguration : IEntityTypeConfiguration<ProviderConnectionRecord>
{
    public void Configure(EntityTypeBuilder<ProviderConnectionRecord> builder)
    {
        builder.ToTable("provider_connections");
        builder.HasKey(x => new { x.TenantId, x.ProviderConnectionId })
            .HasName("pk_provider_connections");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.ProviderConnectionId).HasColumnName("provider_connection_id");
        builder.Property(x => x.ConnectionKey).HasColumnName("connection_key").HasMaxLength(80).IsRequired();
        builder.Property(x => x.ProviderId).HasColumnName("provider_id").HasMaxLength(120).IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Endpoint).HasColumnName("endpoint").HasMaxLength(2048).IsRequired();
        builder.Property(x => x.EncryptedApiKey).HasColumnName("encrypted_api_key").IsRequired();
        builder.Property(x => x.ApiKeyNonce).HasColumnName("api_key_nonce").IsRequired();
        builder.Property(x => x.ApiKeyTag).HasColumnName("api_key_tag").IsRequired();
        builder.Property(x => x.EncryptedDataKey).HasColumnName("encrypted_data_key").IsRequired();
        builder.Property(x => x.KeyEncryptionKeyId).HasColumnName("key_encryption_key_id")
            .HasMaxLength(2048).IsRequired();
        builder.Property(x => x.EncryptionAlgorithm).HasColumnName("encryption_algorithm")
            .HasMaxLength(80).IsRequired();
        builder.Property(x => x.Enabled).HasColumnName("enabled");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(x => new { x.TenantId, x.ConnectionKey }).IsUnique()
            .HasDatabaseName("ux_provider_connections_tenant_key");
        builder.HasIndex(x => new { x.TenantId, x.ProviderId, x.Enabled })
            .HasDatabaseName("ix_provider_connections_tenant_provider_enabled");
        builder.ToTable(table => table.HasCheckConstraint("ck_provider_connections_key",
            "connection_key ~ '^[a-z0-9][a-z0-9._-]{1,79}$'"));
        builder.ToTable(table => table.HasCheckConstraint("ck_provider_connections_text",
            "btrim(provider_id) <> '' AND btrim(display_name) <> '' AND btrim(endpoint) <> ''"));
        builder.ToTable(table => table.HasCheckConstraint("ck_provider_connections_endpoint",
            "endpoint ~ '^https://[^[:space:]]+$'"));
        builder.ToTable(table => table.HasCheckConstraint("ck_provider_connections_encrypted_credential",
            "octet_length(encrypted_api_key) > 0 AND octet_length(api_key_nonce) = 12 AND " +
            "octet_length(api_key_tag) = 16 AND octet_length(encrypted_data_key) > 0 AND " +
            "octet_length(encrypted_api_key) <= 16384 AND octet_length(encrypted_data_key) <= 1024"));
        builder.ToTable(table => table.HasCheckConstraint("ck_provider_connections_key_encryption",
            "key_encryption_key_id ~ '^https://[^[:space:]]+/keys/[^/[:space:]]+/[^/[:space:]]+$' AND " +
            "encryption_algorithm = 'RSA-OAEP-256'"));
    }
}
