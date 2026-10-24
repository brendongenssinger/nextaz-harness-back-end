using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NextAZ.Harness.Infrastructure.Persistence;

internal sealed class AgentRouteBindingRecordConfiguration : IEntityTypeConfiguration<AgentRouteBindingRecord>
{
    public void Configure(EntityTypeBuilder<AgentRouteBindingRecord> builder)
    {
        builder.ToTable("agent_route_bindings");
        builder.HasKey(x => new { x.TenantId, x.Channel, x.Operation }).HasName("pk_agent_route_bindings");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Operation).HasColumnName("operation").HasMaxLength(120).IsRequired();
        builder.Property(x => x.AgentId).HasColumnName("agent_id").HasMaxLength(120).IsRequired();
        builder.Property(x => x.AgentVersion).HasColumnName("agent_version");
        builder.Property(x => x.Enabled).HasColumnName("enabled");
        builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200).IsRequired();
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_agent_route_bindings_tenant",
                "tenant_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_agent_route_bindings_channel",
                "channel ~ '^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)*$'");
            table.HasCheckConstraint("ck_agent_route_bindings_operation",
                "operation ~ '^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)*$'");
            table.HasCheckConstraint("ck_agent_route_bindings_agent_id",
                "agent_id ~ '^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$'");
            table.HasCheckConstraint("ck_agent_route_bindings_agent_version", "agent_version > 0");
            table.HasCheckConstraint("ck_agent_route_bindings_revision", "revision > 0");
            table.HasCheckConstraint("ck_agent_route_bindings_updated_by", "btrim(updated_by) <> ''");
        });
    }
}
