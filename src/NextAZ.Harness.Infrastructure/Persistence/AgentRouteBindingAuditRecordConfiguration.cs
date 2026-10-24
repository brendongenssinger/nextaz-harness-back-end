using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NextAZ.Harness.Infrastructure.Persistence;

internal sealed class AgentRouteBindingAuditRecordConfiguration
    : IEntityTypeConfiguration<AgentRouteBindingAuditRecord>
{
    public void Configure(EntityTypeBuilder<AgentRouteBindingAuditRecord> builder)
    {
        builder.ToTable("agent_route_binding_events");
        builder.HasKey(x => x.EventId).HasName("pk_agent_route_binding_events");
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Operation).HasColumnName("operation").HasMaxLength(120).IsRequired();
        builder.Property(x => x.AgentId).HasColumnName("agent_id").HasMaxLength(120).IsRequired();
        builder.Property(x => x.AgentVersion).HasColumnName("agent_version");
        builder.Property(x => x.Enabled).HasColumnName("enabled");
        builder.Property(x => x.Revision).HasColumnName("revision");
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        builder.Property(x => x.Actor).HasColumnName("actor").HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Channel, x.Operation, x.Revision }).IsUnique()
            .HasDatabaseName("ux_agent_route_events_tenant_route_revision");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_agent_route_events_tenant",
                "tenant_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_agent_route_events_channel",
                "channel ~ '^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)*$'");
            table.HasCheckConstraint("ck_agent_route_events_operation",
                "operation ~ '^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)*$'");
            table.HasCheckConstraint("ck_agent_route_events_agent_id",
                "agent_id ~ '^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$'");
            table.HasCheckConstraint("ck_agent_route_events_agent_version", "agent_version > 0");
            table.HasCheckConstraint("ck_agent_route_events_revision", "revision > 0");
            table.HasCheckConstraint("ck_agent_route_events_actor", "btrim(actor) <> ''");
        });
    }
}
