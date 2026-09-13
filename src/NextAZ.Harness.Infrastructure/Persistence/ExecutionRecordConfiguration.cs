using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace NextAZ.Harness.Infrastructure.Persistence;
internal sealed class ExecutionRecordConfiguration : IEntityTypeConfiguration<ExecutionRecord>
{
    public void Configure(EntityTypeBuilder<ExecutionRecord> builder)
    {
        builder.ToTable("executions");
        builder.HasKey(x => new { x.TenantId, x.ExecutionId }).HasName("pk_executions");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.ExecutionId).HasColumnName("execution_id");
        builder.Property(x => x.AgentId).HasColumnName("agent_id").HasMaxLength(120).IsRequired();
        builder.Property(x => x.AgentVersion).HasColumnName("agent_version");
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128).IsRequired();
        builder.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Operation).HasColumnName("operation").HasMaxLength(120).IsRequired();
        builder.Property(x => x.ContextTimestamp).HasColumnName("context_timestamp");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.Property(x => x.EndedAt).HasColumnName("ended_at");
        builder.Property(x => x.ProviderId).HasColumnName("provider_id").HasMaxLength(120);
        builder.Property(x => x.ModelId).HasColumnName("model_id").HasMaxLength(200);
        builder.Property(x => x.InputTokens).HasColumnName("input_tokens");
        builder.Property(x => x.OutputTokens).HasColumnName("output_tokens");
        builder.Property(x => x.EstimatedCost).HasColumnName("estimated_cost").HasPrecision(18, 8);
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(120);
        builder.Property(x => x.StepsJson).HasColumnName("steps").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ResponseContent).HasColumnName("response_content").HasMaxLength(65536);
        builder.Property(x => x.RequestFingerprint).HasColumnName("request_fingerprint").HasMaxLength(64).IsRequired();
        builder.Property(x => x.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(200);
        builder.Property(x => x.LeaseToken).HasColumnName("lease_token");
        builder.Property(x => x.LeaseExpiresAt).HasColumnName("lease_expires_at");
        builder.HasIndex(x => new { x.TenantId, x.Operation, x.CorrelationId }).IsUnique()
            .HasDatabaseName("ux_executions_tenant_operation_correlation");
        builder.HasIndex(x => new { x.TenantId, x.AgentId, x.StartedAt })
            .HasDatabaseName("ix_executions_tenant_agent_started_at");
        builder.ToTable(table => table.HasCheckConstraint("ck_executions_token_counts",
            "input_tokens >= 0 AND output_tokens >= 0"));
        builder.ToTable(table => table.HasCheckConstraint("ck_executions_lease_state",
            "(status = 'Running' AND lease_owner IS NOT NULL AND lease_token IS NOT NULL AND lease_expires_at IS NOT NULL) OR " +
            "(status <> 'Running' AND lease_owner IS NULL AND lease_token IS NULL AND lease_expires_at IS NULL)"));
    }
}
