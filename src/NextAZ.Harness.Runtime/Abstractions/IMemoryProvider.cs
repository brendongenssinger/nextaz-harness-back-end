namespace NextAZ.Harness.Runtime.Abstractions;
public interface IMemoryProvider
{
    Task<Memory.MemoryContext> LoadAsync(HarnessContext context, TenantAgentConfiguration configuration, CancellationToken cancellationToken);
}
