using System.Collections.Frozen;
using System.Text.Json;
using NextAZ.Harness.Domain.Policies;
using NextAZ.Harness.Domain.Tools;
using NextAZ.Harness.Infrastructure.Diagnostics;
using NextAZ.Harness.Runtime.Execution;
using NextAZ.Harness.Runtime.ModelRouting;
using NextAZ.Harness.Runtime.Tooling;
using NextAZ.Harness.Tests.Fixtures;
namespace NextAZ.Harness.Runtime.Tests;
public sealed class RuntimeBoundaryTests
{
    [Fact] public async Task ExecutesSimulationAndAuthorizedTool()
    {
        var f = new HarnessFixture();
        var result = await f.Runtime().ExecuteAsync(f.RuntimeRequest(), default);
        Assert.Contains("[SIMULACAO]", result.Content);
        Assert.Equal("simulation", result.ProviderId);
    }
    [Fact] public async Task RejectsToolSuggestedOutsideAllowlist()
    {
        var f = new HarnessFixture();
        var provider = new StubModelProvider("simulation", (_, _) => Task.FromResult(new ModelExecutionResult(null,
            [new("1", new("billing.confirm-payment"), JsonSerializer.SerializeToElement(new { }))], 0, 0, 0)));
        var ex = await Assert.ThrowsAsync<HarnessExecutionException>(() => f.Runtime(provider).ExecuteAsync(f.RuntimeRequest(), default));
        Assert.Equal("tool_forbidden", ex.Code);
    }
    [Fact] public async Task TenantCannotExpandAgentAllowlist()
    {
        var f = new HarnessFixture();
        var registry = new ToolRegistry([new ExecutionInfoTool()]);
        var agent = f.Agent with { AllowedTools = Array.Empty<ToolId>().ToFrozenSet() };
        Assert.Empty(await registry.GetAllowedToolsAsync(agent, f.Configuration, f.Permissions, default));
    }
    [Fact] public async Task AgentCannotExpandTenantAllowlist()
    {
        var f = new HarnessFixture();
        var registry = new ToolRegistry([new ExecutionInfoTool()]);
        var config = f.Configuration with { AllowedTools = Array.Empty<ToolId>().ToFrozenSet() };
        Assert.Empty(await registry.GetAllowedToolsAsync(f.Agent, config, f.Permissions, default));
    }
    [Fact] public async Task RevalidatesContextAtToolInvocation()
    {
        var f = new HarnessFixture();
        var executor = new ToolExecutor(new ToolRegistry([new ExecutionInfoTool()]), f.Policies);
        var request = f.RuntimeRequest() with { Configuration = f.Configuration with { TenantId = Guid.NewGuid() } };
        var ex = await Assert.ThrowsAsync<HarnessExecutionException>(() => executor.ExecuteAsync(request,
            new("1", new("harness.execution-info"), JsonSerializer.SerializeToElement(new { })), default));
        Assert.Equal("tenant_context_mismatch", ex.Code);
    }
    [Fact] public async Task RejectsUnexpectedToolArguments()
    {
        var f = new HarnessFixture();
        var executor = new ToolExecutor(new ToolRegistry([new ExecutionInfoTool()]), f.Policies);
        var ex = await Assert.ThrowsAsync<HarnessExecutionException>(() => executor.ExecuteAsync(f.RuntimeRequest(),
            new("1", new("harness.execution-info"), JsonSerializer.SerializeToElement(new { tenantId = Guid.NewGuid() })), default));
        Assert.Equal("tool_arguments_invalid", ex.Code);
    }
    [Fact] public async Task StopsRepeatedToolCalls()
    {
        var f = new HarnessFixture();
        var provider = new StubModelProvider("simulation", (_, _) => Task.FromResult(new ModelExecutionResult(null,
            [new("1", new("harness.execution-info"), JsonSerializer.SerializeToElement(new { }))], 0, 0, 0)));
        var ex = await Assert.ThrowsAsync<HarnessExecutionException>(() => f.Runtime(provider).ExecuteAsync(f.RuntimeRequest(), default));
        Assert.Equal("tool_call_repeated_or_invalid", ex.Code);
    }
    [Fact] public async Task LimitsTotalTools()
    {
        var f = new HarnessFixture();
        var ex = await Assert.ThrowsAsync<HarnessExecutionException>(() =>
            f.Runtime().ExecuteAsync(f.RuntimeRequest(new ExecutionLimits(maxToolCalls: 0)), default));
        Assert.Equal("model_output_invalid", ex.Code);
    }
    [Fact] public async Task UsesConfiguredFallbackOnUnavailableProvider()
    {
        var f = new HarnessFixture();
        var first = new StubModelProvider("down", (_, _) => throw new ModelProviderUnavailableException());
        var second = new StubModelProvider("up", (_, _) => Task.FromResult(new ModelExecutionResult("fallback", [], 3, 2, null)));
        var request = f.RuntimeRequest() with { Routes = [
            new("down", "first", TimeSpan.FromSeconds(1)), new("up", "second", TimeSpan.FromSeconds(1))] };
        var result = await f.Runtime(first, second).ExecuteAsync(request, default);
        Assert.Equal("up", result.ProviderId);
        Assert.Null(result.EstimatedCost);
    }
    [Fact] public async Task ReportsTotalProviderFailure()
    {
        var f = new HarnessFixture();
        var provider = new StubModelProvider("simulation", (_, _) => throw new ModelProviderUnavailableException());
        var ex = await Assert.ThrowsAsync<HarnessExecutionException>(() => f.Runtime(provider).ExecuteAsync(f.RuntimeRequest(), default));
        Assert.Equal("provider_unavailable", ex.Code);
    }
    [Fact] public async Task ProviderTimeoutCanUseFallback()
    {
        var f = new HarnessFixture();
        var slow = new StubModelProvider("slow", async (_, ct) => {
            await Task.Delay(Timeout.Infinite, ct);
            return new ModelExecutionResult("never", [], 0, 0, 0);
        });
        var up = new StubModelProvider("up", (_, _) => Task.FromResult(new ModelExecutionResult("ok", [], 0, 0, 0)));
        var result = await f.Runtime(slow, up).ExecuteAsync(f.RuntimeRequest() with {
            Routes = [new("slow", "slow", TimeSpan.FromMilliseconds(20)), new("up", "up", TimeSpan.FromSeconds(1))]
        }, default);
        Assert.Equal("up", result.ProviderId);
    }
    [Fact] public async Task CallerCancellationNeverTriggersFallback()
    {
        var f = new HarnessFixture();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Runtime().ExecuteAsync(f.RuntimeRequest(), cts.Token));
    }
    [Fact] public async Task RejectsMemoryFromAnotherTenant()
    {
        var f = new HarnessFixture();
        var request = f.RuntimeRequest();
        var ex = await Assert.ThrowsAsync<HarnessExecutionException>(() =>
            f.Runtime().ExecuteAsync(request with { Memory = request.Memory with { TenantId = Guid.NewGuid() } }, default));
        Assert.Equal("memory_scope_mismatch", ex.Code);
    }
    [Fact] public void UnknownPolicyFailsClosed()
    {
        var f = new HarnessFixture();
        var request = f.RuntimeRequest();
        var agent = f.Agent with { RequiredPolicies = new[] { "unknown-policy" }.ToFrozenSet() };
        Assert.Throws<HarnessExecutionException>(() => f.Policies.Validate(request.Context, agent, f.Configuration, f.Permissions));
    }
}
