using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace NextAZ.Harness.Infrastructure.Persistence;
internal sealed class OutboxEventRecordConfiguration : IEntityTypeConfiguration<OutboxEventRecord>
{
    public void Configure(EntityTypeBuilder<OutboxEventRecord> builder)
    {
        builder.ToTable("outbox_events");
        builder.HasKey(x => x.MessageId).HasName("pk_outbox_events");
        builder.Property(x => x.MessageId).HasColumnName("message_id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.ExecutionId).HasColumnName("execution_id");
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128).IsRequired();
        builder.Property(x => x.AgentId).HasColumnName("agent_id").HasMaxLength(120).IsRequired();
        builder.Property(x => x.AgentVersion).HasColumnName("agent_version");
        builder.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Operation).HasColumnName("operation").HasMaxLength(120).IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(160).IsRequired();
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(120);
        builder.Property(x => x.SchemaVersion).HasColumnName("schema_version");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.TenantId, x.ExecutionId }).HasDatabaseName("ix_outbox_tenant_execution");
        builder.HasOne<ExecutionRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ExecutionId })
            .HasPrincipalKey(x => new { x.TenantId, x.ExecutionId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_outbox_events_executions");
    }
}
