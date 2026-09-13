using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Policies;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.Execution;
using NextAZ.Harness.Runtime.Memory;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Infrastructure.Memory;
public sealed class DisabledMemoryProvider : IMemoryProvider
{
    public Task<MemoryContext> LoadAsync(HarnessContext context, TenantAgentConfiguration configuration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.TenantId != configuration.TenantId || context.AgentId != configuration.AgentId)
            throw new HarnessExecutionException("memory_scope_mismatch");
        if (configuration.MemoryPolicy != MemoryPolicy.None || configuration.KnowledgeBaseId.HasValue)
            throw new HarnessExecutionException("memory_not_configured");
        return Task.FromResult(new MemoryContext(context.TenantId, null, Array.Empty<string>()));
    }
}
