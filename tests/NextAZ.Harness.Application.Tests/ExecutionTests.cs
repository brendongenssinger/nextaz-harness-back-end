using NextAZ.Harness.Application.Executions;
using NextAZ.Harness.Contracts.Executions;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.Execution;
using NextAZ.Harness.Tests.Fixtures;
namespace NextAZ.Harness.Application.Tests;
public sealed class ExecutionTests
{
    private static ExecuteHarnessRequest Command(HarnessFixture f) =>
        new(new HarnessRequest(f.TenantId, "api", "atendimento", "Customer input", "corr-preserved"));
    [Fact] public async Task PreservesCorrelationAndCommitsScopedTerminalAudit()
    {
        var f = new HarnessFixture();
        var result = await f.Handler().Handle(Command(f), default);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("corr-preserved", result.CorrelationId);
        var persisted = await f.Executions.GetAsync(f.TenantId, result.ExecutionId, default);
        Assert.NotNull(persisted);
        Assert.Equal(AgentExecutionStatus.Completed, persisted.Status);
        Assert.Contains(persisted.Steps, s => s.Name == "tool.completed:harness.execution-info");
        Assert.All(f.Executions.GetAuditEventsForTenant(f.TenantId), e => {
            Assert.Equal("corr-preserved", e.CorrelationId);
            Assert.Equal(result.ExecutionId, e.ExecutionId);
        });
        Assert.Single(f.Executions.GetAuditEventsForTenant(f.TenantId));
    }
    [Fact] public async Task RejectsTenantProvidedByAnotherCaller()
    {
        var f = new HarnessFixture();
        var result = await f.Handler(currentTenant: Guid.NewGuid()).Handle(Command(f), default);
        Assert.Equal("tenant_forbidden", result.Error?.Code);
        Assert.Empty(f.Executions.GetAuditEventsForTenant(f.TenantId));
    }
    [Fact] public async Task UnknownTenantHasNoDefaultAgent()
    {
        var f = new HarnessFixture();
        var other = Guid.NewGuid();
        var result = await f.Handler(currentTenant: other).Handle(
            new(Command(f).Request with { TenantId = other }), default);
        Assert.Equal("agent_not_found", result.Error?.Code);
    }
    [Fact] public async Task RejectsMissingTenantConfiguration()
    {
        var f = new HarnessFixture();
        var result = await f.Handler(configurations: []).Handle(Command(f), default);
        Assert.Equal("tenant_agent_not_configured", result.Error?.Code);
    }
    [Fact] public async Task DisabledAgentCannotReachRuntime()
    {
        var f = new HarnessFixture();
        f.Configuration = f.Configuration with { Enabled = false };
        var spy = new FailingRuntime();
        var result = await f.Handler(spy).Handle(Command(f), default);
        Assert.Equal("agent_disabled", result.Error?.Code);
        Assert.False(spy.Invoked);
        Assert.Equal(AgentExecutionStatus.Failed, f.Telemetry.Last?.Status);
    }
    [Fact] public async Task RuntimeFailureIsSanitizedAndPersisted()
    {
        var f = new HarnessFixture();
        var result = await f.Handler(new FailingRuntime()).Handle(Command(f), default);
        Assert.Equal("execution_failed", result.Error?.Code);
        Assert.DoesNotContain("private-provider-data", result.Error!.Message);
        var snapshot = await f.Executions.GetAsync(f.TenantId, result.ExecutionId, default);
        Assert.Equal(AgentExecutionStatus.Failed, snapshot?.Status);
        Assert.NotNull(snapshot?.EndedAt);
        Assert.Contains(f.Executions.GetAuditEventsForTenant(f.TenantId),
            e => e.EventType == "harness.execution.failed.v1");
    }
    [Fact] public async Task DoesNotPretendRagIsAvailable()
    {
        var f = new HarnessFixture();
        f.Configuration = f.Configuration with { MemoryPolicy = new("customer-context"), KnowledgeBaseId = Guid.NewGuid() };
        var result = await f.Handler().Handle(Command(f), default);
        Assert.Equal("memory_not_configured", result.Error?.Code);
    }
    [Fact] public async Task MetadataDoesNotSelectAgentOrTenant()
    {
        var f = new HarnessFixture();
        var result = await f.Handler().Handle(new(Command(f).Request with {
            Metadata = new Dictionary<string, string> { ["agentId"] = "billing.confirm-payment", ["tenantId"] = Guid.NewGuid().ToString() }
        }), default);
        Assert.Equal("cartorio.atendimento", result.AgentId);
        Assert.Equal("Completed", result.Status);
    }
    [Fact] public async Task RejectsOversizedContentBeforeExecution()
    {
        var f = new HarnessFixture();
        var result = await f.Handler().Handle(new(Command(f).Request with { Content = new string('a', 16001) }), default);
        Assert.Equal("invalid_request", result.Error?.Code);
    }
    private sealed class FailingRuntime : IAgentRuntime
    {
        public bool Invoked { get; private set; }
        public Task<AgentExecutionResult> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken)
        {
            Invoked = true;
            throw new InvalidOperationException("private-provider-data");
        }
    }
}
