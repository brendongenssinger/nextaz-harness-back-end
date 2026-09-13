using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace NextAZ.Harness.Infrastructure.Persistence;
public sealed class HarnessDbContextDesignFactory : IDesignTimeDbContextFactory<HarnessDbContext>
{
    public HarnessDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Harness") ??
            "Host=localhost;Database=harness_design;Username=design";
        var options = new DbContextOptionsBuilder<HarnessDbContext>()
            .UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "harness"))
            .Options;
        return new(options);
    }
}
