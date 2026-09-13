using System.Collections.Frozen;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Domain.Policies;
using NextAZ.Harness.Domain.Tools;
using NextAZ.Harness.Infrastructure.Configuration;
using NextAZ.Harness.Infrastructure.Diagnostics;
using NextAZ.Harness.Infrastructure.Memory;
using NextAZ.Harness.Infrastructure.Persistence;
using NextAZ.Harness.Providers.Simulation;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.AgentResolution;
using NextAZ.Harness.Runtime.Execution;
using NextAZ.Harness.Runtime.Memory;
using NextAZ.Harness.Runtime.ModelRouting;
using NextAZ.Harness.Runtime.Policies;
using NextAZ.Harness.Runtime.Tooling;
using NextAZ.Harness.Application.Executions;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Tests.Fixtures;
internal sealed class HarnessFixture
{
    public Guid TenantId { get; } = Guid.NewGuid();
    public AgentDefinition Agent { get; }
    public TenantAgentConfiguration Configuration { get; set; }
    public IReadOnlySet<string> Permissions { get; set; } = new[] { "harness.execute", "harness.read" }.ToFrozenSet();
    public ExecutionPolicyEvaluator Policies { get; } = new();
    public InMemoryExecutionRepository Executions { get; } = new();
    public CapturingTelemetry Telemetry { get; } = new();
    public HarnessFixture()
    {
        var allowed = new[] { new ToolId("harness.execution-info") }.ToFrozenSet();
        Agent = new(new("cartorio.atendimento"), new(1), "Atendimento", "cartorio", "Test", true,
            new("fast-cheap"), MemoryPolicy.None, allowed, new[] { "tenant-isolation", "tools-allowlist" }.ToFrozenSet());
        Configuration = new(TenantId, Agent.AgentId, true, 1, "", allowed, null, Agent.ModelPolicy, MemoryPolicy.None, new());
    }
    public AgentExecutionRequest RuntimeRequest(ExecutionLimits? limits = null)
    {
        var config = limits is null ? Configuration : Configuration with { Limits = limits };
        return new(new HarnessContext(TenantId, Agent.AgentId, Agent.Version, ExecutionId.New(), "corr-123",
            "api", "atendimento", DateTimeOffset.UtcNow), Agent, config, Permissions, "test",
            new MemoryContext(TenantId, null, []), [new("simulation", "deterministic-v1", TimeSpan.FromSeconds(1))],
            [new ExecutionInfoTool().Definition], _ => { });
    }
    public BoundedAgentRuntime Runtime(params IModelProvider[] providers)
    {
        var registry = new ToolRegistry([new ExecutionInfoTool()]);
        return new(providers.Length == 0 ? [new SimulationModelProvider()] : providers,
            new ToolExecutor(registry, Policies), Policies);
    }
    public ExecuteHarnessRequestHandler Handler(IAgentRuntime? runtime = null, IMemoryProvider? memory = null,
        Guid? currentTenant = null, IEnumerable<TenantAgentConfiguration>? configurations = null)
    {
        var toolRegistry = new ToolRegistry([new ExecutionInfoTool()]);
        return new(new CurrentTenant(currentTenant ?? TenantId, Permissions),
            new ConfiguredAgentResolver(new Dictionary<(Guid, string, string), AgentId> {
                [(TenantId, "api", "atendimento")] = Agent.AgentId }),
            new InMemoryAgentRegistry([Agent]),
            new InMemoryTenantAgentConfigurationRepository(configurations ?? [Configuration]),
            Policies, memory ?? new DisabledMemoryProvider(),
            new ConfiguredModelRouter(new Dictionary<(Guid, string), IReadOnlyList<ModelRoute>> {
                [(TenantId, "fast-cheap")] = [new("simulation", "deterministic-v1", TimeSpan.FromSeconds(1))] }),
            toolRegistry, runtime ?? Runtime(), Executions, Telemetry, TimeProvider.System);
    }
}
internal sealed record CurrentTenant(Guid TenantId, IReadOnlySet<string> Permissions) : ICurrentTenant;
internal sealed class CapturingTelemetry : IExecutionTelemetry
{
    public ExecutionSnapshot? Last { get; private set; }
    public void Record(ExecutionSnapshot execution) => Last = execution;
}
internal sealed class StubModelProvider(string providerId,
    Func<ModelExecutionRequest, CancellationToken, Task<ModelExecutionResult>> execute) : IModelProvider
{
    public string ProviderId => providerId;
    public Task<ModelExecutionResult> ExecuteAsync(ModelExecutionRequest request, CancellationToken cancellationToken) =>
        execute(request, cancellationToken);
}
