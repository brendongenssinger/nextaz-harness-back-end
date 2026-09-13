using System.Collections.Frozen;
using System.Text.Json;
using NextAZ.Harness.Domain.Tools;
using NextAZ.Harness.Runtime.Execution;
using NextAZ.Harness.Runtime.Tooling;
using NextAZ.Harness.Tests.Fixtures;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Runtime.Tests;
public sealed class ToolSafetyTests
{
    [Theory]
    [InlineData(ToolRiskLevel.WriteLowRisk)]
    [InlineData(ToolRiskLevel.WriteHighRisk)]
    public async Task WriteToolCannotRunEvenWithAllPermissions(ToolRiskLevel risk)
    {
        var f = new HarnessFixture();
        var handler = new SpyTool(risk);
        var allowed = new[] { handler.Definition.ToolId }.ToFrozenSet();
        var request = f.RuntimeRequest() with {
            Agent = f.Agent with { AllowedTools = allowed },
            Configuration = f.Configuration with { AllowedTools = allowed },
            Tools = [handler.Definition]
        };
        var executor = new ToolExecutor(new ToolRegistry([handler]), f.Policies);
        var error = await Assert.ThrowsAsync<HarnessExecutionException>(() =>
            executor.ExecuteAsync(request, new("call", handler.Definition.ToolId, JsonSerializer.SerializeToElement(new { })), default));
        Assert.Equal("tool_write_not_enabled", error.Code);
        Assert.False(handler.Invoked);
    }
    [Fact] public async Task StepLimitStopsUnfinishedExecution()
    {
        var f = new HarnessFixture();
        var error = await Assert.ThrowsAsync<HarnessExecutionException>(() =>
            f.Runtime().ExecuteAsync(f.RuntimeRequest(new(maxSteps: 1)), default));
        Assert.Equal("step_limit_exceeded", error.Code);
    }
    [Fact] public async Task ImobiliariaToolCannotChangeTrustedAtendimentoReference()
    {
        var f = new HarnessFixture();
        var handler = new SpyTool(ToolRiskLevel.Read, "imobiliaria.contexto-atendimento");
        var allowed = new[] { handler.Definition.ToolId }.ToFrozenSet();
        var trustedId = Guid.NewGuid();
        var request = f.RuntimeRequest() with {
            Agent = f.Agent with { AllowedTools = allowed },
            Configuration = f.Configuration with { AllowedTools = allowed },
            Tools = [handler.Definition],
            ResourceReferences = new Dictionary<string, string> { ["atendimentoId"] = trustedId.ToString() }
        };
        var executor = new ToolExecutor(new ToolRegistry([handler]), f.Policies);
        var error = await Assert.ThrowsAsync<HarnessExecutionException>(() => executor.ExecuteAsync(request,
            new("call", handler.Definition.ToolId,
                JsonSerializer.SerializeToElement(new { atendimentoId = Guid.NewGuid().ToString() })), default));
        Assert.Equal("tool_resource_forbidden", error.Code);
        Assert.False(handler.Invoked);
    }
    private sealed class SpyTool(ToolRiskLevel risk, string toolId = "test.write") : IToolHandler
    {
        public bool Invoked { get; private set; }
        public ToolDefinition Definition { get; } = new(new(toolId), "test", "test",
            "{}", risk, true, TimeSpan.FromSeconds(1), "harness.execute");
        public bool ValidateArguments(JsonElement arguments) => true;
        public Task<JsonElement> ExecuteAsync(HarnessContext context, JsonElement arguments, CancellationToken cancellationToken)
        {
            Invoked = true;
            return Task.FromResult(JsonSerializer.SerializeToElement(new { }));
        }
    }
}
