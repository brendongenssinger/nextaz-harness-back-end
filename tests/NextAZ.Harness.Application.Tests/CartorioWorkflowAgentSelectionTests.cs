using System.Collections.Frozen;
using NextAZ.Harness.Application.Cartorio;
using NextAZ.Harness.Application.Executions;
using NextAZ.Harness.Contracts.Cartorio;
using NextAZ.Harness.Infrastructure.Configuration;
using NextAZ.Harness.Infrastructure.Persistence;
using NextAZ.Harness.Runtime.ModelRouting;
using NextAZ.Harness.Tests.Fixtures;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Executions;
using HarnessExecutionContext = NextAZ.Harness.Domain.Executions.ExecutionContext;

namespace NextAZ.Harness.Application.Tests;

public sealed class CartorioWorkflowAgentSelectionTests
{
    [Fact]
    public async Task CatalogOmitsRevokedTenantConfiguration()
    {
        var fixture = new HarnessFixture();
        var handler = new GetCartorioWorkflowAgentsHandler(
            new CurrentTenant(fixture.TenantId, fixture.Permissions),
            new InMemoryAgentRegistry([fixture.Agent]),
            new InMemoryTenantAgentConfigurationRepository(
                [fixture.Configuration with { Enabled = false }]),
            Router(fixture));

        var result = await handler.Handle(new(["web"],
            new[] { fixture.Agent.AgentId }.ToFrozenSet()), default);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ExecutionRevalidatesRevocationBeforeDispatch()
    {
        var fixture = new HarnessFixture();
        var handler = new ExecuteCartorioWorkflowAgentHandler(
            new CurrentTenant(fixture.TenantId, fixture.Permissions),
            new InMemoryAgentRegistry([fixture.Agent]),
            new InMemoryTenantAgentConfigurationRepository(
                [fixture.Configuration with { Enabled = false }]), null!);
        var command = Command(fixture);

        var result = await handler.Handle(command, default);

        Assert.Equal("agent_disabled", result.Error?.Code);
    }

    [Fact]
    public async Task ExecutionRejectsAgentVersionChangedSincePublication()
    {
        var fixture = new HarnessFixture();
        var handler = new ExecuteCartorioWorkflowAgentHandler(
            new CurrentTenant(fixture.TenantId, fixture.Permissions),
            new InMemoryAgentRegistry([fixture.Agent]),
            new InMemoryTenantAgentConfigurationRepository([fixture.Configuration]), null!);
        var command = Command(fixture) with { AgentVersion = fixture.Agent.Version.Value + 1 };

        var result = await handler.Handle(command, default);

        Assert.Equal("agent_version_conflict", result.Error?.Code);
    }

    [Fact]
    public async Task CatalogOmitsConfiguredAgentWithoutModelRoute()
    {
        var fixture = new HarnessFixture();
        var handler = new GetCartorioWorkflowAgentsHandler(
            new CurrentTenant(fixture.TenantId, fixture.Permissions),
            new InMemoryAgentRegistry([fixture.Agent]),
            new InMemoryTenantAgentConfigurationRepository([fixture.Configuration]),
            new ConfiguredModelRouter(new Dictionary<(Guid, string, int, string),
                IReadOnlyList<ModelRoute>>()));

        var result = await handler.Handle(new(["web"],
            new[] { fixture.Agent.AgentId }.ToFrozenSet()), default);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ReconciliationRejectsOtherProductEvenWhenOperationIsForged()
    {
        var fixture = new HarnessFixture();
        var otherAgent = fixture.Agent with
        {
            AgentId = new AgentId("imobiliaria.vitrine-busca"),
            Vertical = "imobiliaria"
        };
        var repository = new InMemoryExecutionRepository();
        var executionId = ExecutionId.New();
        var context = new HarnessExecutionContext(fixture.TenantId, otherAgent.AgentId,
            otherAgent.Version, executionId, "same-tenant-forged-operation", "web",
            "cartorio.workflow.agent.execute", DateTimeOffset.UtcNow);
        await repository.TryCreateAsync(new(context, AgentExecutionStatus.Pending,
            DateTimeOffset.UtcNow, null, null, null, 0, 0, null, null, [],
            RequestFingerprint: new string('a', 64)), default);
        var handler = new GetCartorioWorkflowAgentExecutionHandler(
            new CurrentTenant(fixture.TenantId, fixture.Permissions), repository,
            new InMemoryAgentRegistry([fixture.Agent, otherAgent]));

        var result = await handler.Handle(new(executionId.Value, otherAgent.AgentId,
            otherAgent.Version.Value, new[] { fixture.Agent.AgentId }.ToFrozenSet()), default);
        var allowedButWrongVertical = await handler.Handle(new(executionId.Value,
            otherAgent.AgentId, otherAgent.Version.Value,
            new[] { otherAgent.AgentId }.ToFrozenSet()), default);

        Assert.Null(result);
        Assert.Null(allowedButWrongVertical);
    }

    [Fact]
    public async Task ResponseOnlyForcesToolsOffRegardlessOfPrompt()
    {
        var fixture=new HarnessFixture();
        var sender=System.Reflection.DispatchProxy.Create<MediatR.ISender,CaptureSender>();
        var capture=(CaptureSender)(object)sender;
        var handler=new ExecuteCartorioWorkflowAgentHandler(new CurrentTenant(fixture.TenantId,fixture.Permissions),
            new InMemoryAgentRegistry([fixture.Agent]),new InMemoryTenantAgentConfigurationRepository([fixture.Configuration]),sender);
        var command=Command(fixture) with {ResponseOnly=true};
        await handler.Handle(command,default);
        Assert.NotNull(capture.Command);
        Assert.True(capture.Command!.Profile!.DisableTools);
        Assert.Equal("cartorio.workflow.response.generate",capture.Command.Request.Operation);
    }
    public class CaptureSender : System.Reflection.DispatchProxy
    {
        public ExecuteHarnessRequest? Command;
        protected override object? Invoke(System.Reflection.MethodInfo? method,object?[]? args)
        {
            Command=(ExecuteHarnessRequest)args![0]!;
            return Task.FromResult(new NextAZ.Harness.Contracts.Executions.HarnessResponse(Guid.NewGuid(),"test","cartorio.atendimento",1,"Completed","Resposta",null));
        }
    }
    private static ExecuteCartorioWorkflowAgent Command(HarnessFixture fixture)
    {
        var request = new CartorioWorkflowAgentExecutionRequest("test", "corr-workflow-agent", "web",
            Guid.NewGuid(), 1, Guid.NewGuid(), "step-1", Guid.NewGuid(), null,
            new("system"));
        var origin = new TrustedExecutionOrigin("nextaz-cartorio-workflow", "system",
            "nextaz-cartorio-workflow", request.WorkflowId, request.WorkflowVersion, request.RunId,
            request.StepId, request.ConversationId, request.SolicitacaoId);
        return new(fixture.Agent.AgentId, fixture.Agent.Version.Value,
            new[] { "web" }.ToFrozenSet(), new[] { fixture.Agent.AgentId }.ToFrozenSet(),
            request, origin);
    }

    private static ConfiguredModelRouter Router(HarnessFixture fixture) => new(
        new Dictionary<(Guid, string, int, string), IReadOnlyList<ModelRoute>>
        {
            [(fixture.TenantId, fixture.Agent.AgentId.Value, fixture.Agent.Version.Value,
                fixture.Configuration.ModelPolicy.Name)] =
                [new("simulation", "deterministic-v1", TimeSpan.FromSeconds(1))]
        });
}
