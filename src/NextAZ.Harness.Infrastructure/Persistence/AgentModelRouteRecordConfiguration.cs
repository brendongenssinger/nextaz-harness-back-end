using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NextAZ.Harness.Infrastructure.Persistence;

internal sealed class AgentModelRouteRecordConfiguration : IEntityTypeConfiguration<AgentModelRouteRecord>
{
    public void Configure(EntityTypeBuilder<AgentModelRouteRecord> builder)
    {
        builder.ToTable("agent_model_routes");
        builder.HasKey(x => new { x.TenantId, x.AgentId, x.AgentVersion, x.ModelPolicy, x.Priority })
            .HasName("pk_agent_model_routes");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.AgentId).HasColumnName("agent_id").HasMaxLength(120).IsRequired();
        builder.Property(x => x.AgentVersion).HasColumnName("agent_version");
        builder.Property(x => x.Priority).HasColumnName("priority");
        builder.Property(x => x.ProviderConnectionId).HasColumnName("provider_connection_id");
        builder.Property(x => x.ModelPolicy).HasColumnName("model_policy").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ModelId).HasColumnName("model_id").HasMaxLength(200).IsRequired();
        builder.Property(x => x.TimeoutSeconds).HasColumnName("timeout_seconds");
        builder.Property(x => x.Enabled).HasColumnName("enabled");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne(x => x.ProviderConnection).WithMany(x => x.AgentModelRoutes)
            .HasForeignKey(x => new { x.TenantId, x.ProviderConnectionId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_agent_model_routes_provider_connection");
        builder.HasIndex(x => new { x.TenantId, x.ProviderConnectionId })
            .HasDatabaseName("ix_agent_model_routes_tenant_provider_connection");
        builder.ToTable(table => table.HasCheckConstraint("ck_agent_model_routes_values",
            "agent_version > 0 AND priority BETWEEN 1 AND 4 AND timeout_seconds BETWEEN 1 AND 300 AND " +
            "agent_id ~ '^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$' AND " +
            "length(model_policy) BETWEEN 1 AND 100 AND btrim(model_policy) <> '' AND " +
            "length(model_id) BETWEEN 1 AND 200 AND btrim(model_id) <> ''"));
    }
}
