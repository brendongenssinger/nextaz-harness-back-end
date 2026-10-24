using Microsoft.EntityFrameworkCore;
namespace NextAZ.Harness.Infrastructure.Persistence;
public sealed class HarnessDbContext(DbContextOptions<HarnessDbContext> options) : DbContext(options)
{
    internal DbSet<ExecutionRecord> Executions => Set<ExecutionRecord>();
    internal DbSet<OutboxEventRecord> OutboxEvents => Set<OutboxEventRecord>();
    internal DbSet<AgentRouteBindingRecord> AgentRouteBindings => Set<AgentRouteBindingRecord>();
    internal DbSet<AgentRouteBindingAuditRecord> AgentRouteBindingEvents => Set<AgentRouteBindingAuditRecord>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("harness");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HarnessDbContext).Assembly);
    }
}
