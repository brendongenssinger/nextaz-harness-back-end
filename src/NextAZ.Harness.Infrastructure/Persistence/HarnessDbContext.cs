using Microsoft.EntityFrameworkCore;
namespace NextAZ.Harness.Infrastructure.Persistence;
public sealed class HarnessDbContext(DbContextOptions<HarnessDbContext> options) : DbContext(options)
{
    internal DbSet<ExecutionRecord> Executions => Set<ExecutionRecord>();
    internal DbSet<OutboxEventRecord> OutboxEvents => Set<OutboxEventRecord>();
    internal DbSet<ProviderConnectionRecord> ProviderConnections => Set<ProviderConnectionRecord>();
    internal DbSet<AgentModelRouteRecord> AgentModelRoutes => Set<AgentModelRouteRecord>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("harness");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HarnessDbContext).Assembly);
    }
}
