using System.Text.Json;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.Execution;
using NextAZ.Harness.Runtime.ModelRouting;
using NextAZ.Harness.Runtime.Tooling;
using NextAZ.Harness.Tests.Fixtures;

namespace NextAZ.Harness.Runtime.Tests;

public sealed class ToolLoopRegressionTests
{
    [Theory]
    [InlineData("{\"a\":1,\"b\":2}", "{ \"b\" : 2, \"a\" : 1 }")]
    [InlineData("{\"filter\":{\"a\":1,\"b\":2}}", "{\"filter\":{\"b\":2,\"a\":1}}")]
    [InlineData("{\"items\":[{\"a\":1,\"b\":2}]}", "{\"items\":[{\"b\":2,\"a\":1}]}")]
    [InlineData("{\"name\":\"ana\"}", "{\"name\":\"\\u0061na\"}")]
    public async Task StopsEquivalentArgumentsEvenWithANewCallId(string first, string second)
    {
        var (runtime, fixture, executor) = Create(first, second);
        var error = await Assert.ThrowsAsync<HarnessExecutionException>(() =>
            runtime.ExecuteAsync(fixture.RuntimeRequest(), default));

        Assert.Equal("tool_call_repeated_or_invalid", error.Code);
        Assert.Equal(1, executor.Calls);
    }

    [Theory]
    [InlineData("{\"items\":[1,2]}", "{\"items\":[2,1]}")]
    [InlineData("{\"page\":1}", "{\"page\":2}")]
    [InlineData("{\"value\":1}", "{\"value\":\"1\"}")]
    [InlineData("{\"value\":null}", "{}")]
    public async Task PreservesMeaningfulArgumentDifferences(string first, string second)
    {
        var (runtime, fixture, executor) = Create(first, second);
        var result = await runtime.ExecuteAsync(fixture.RuntimeRequest(), default);

        Assert.Equal("done", result.Content);
        Assert.Equal(2, executor.Calls);
    }

    [Theory]
    [InlineData("{\"id\":1,\"id\":2}")]
    [InlineData("{\"filter\":{\"id\":1,\"id\":2}}")]
    [InlineData("{\"items\":[{\"id\":1,\"id\":2}]}")]
    public async Task RejectsAmbiguousDuplicatePropertiesBeforeInvokingTool(string arguments)
    {
        var (runtime, fixture, executor) = Create(arguments);
        var error = await Assert.ThrowsAsync<HarnessExecutionException>(() =>
            runtime.ExecuteAsync(fixture.RuntimeRequest(), default));

        Assert.Equal("tool_call_repeated_or_invalid", error.Code);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task HistoryBelongsToExecutionNotSharedRuntimeOrTenant()
    {
        var (runtime, firstTenant, executor) = Create("{\"id\":1}");
        var secondTenant = new HarnessFixture();

        Assert.Equal("done", (await runtime.ExecuteAsync(firstTenant.RuntimeRequest(), default)).Content);
        Assert.Equal("done", (await runtime.ExecuteAsync(firstTenant.RuntimeRequest(), default)).Content);
        Assert.Equal("done", (await runtime.ExecuteAsync(secondTenant.RuntimeRequest(), default)).Content);
        Assert.Equal(3, executor.Calls);
    }

    private static (BoundedAgentRuntime Runtime, HarnessFixture Fixture, CountingExecutor Executor)
        Create(params string[] arguments)
    {
        var fixture = new HarnessFixture();
        var executor = new CountingExecutor();
        var provider = new StubModelProvider("simulation", (request, _) =>
        {
            var index = request.ToolResults.Count;
            return Task.FromResult(index < arguments.Length
                ? new ModelExecutionResult(null,
                    [new ToolCall($"call-{index}", new("harness.execution-info"),
                        JsonSerializer.Deserialize<JsonElement>(arguments[index]))], 0, 0, null)
                : new ModelExecutionResult("done", [], 0, 0, null));
        });
        return (new BoundedAgentRuntime([provider], executor, fixture.Policies), fixture, executor);
    }

    // This fixture isolates loop detection; authorization is covered by ToolSafety/RuntimeBoundary tests.
    private sealed class CountingExecutor : IToolExecutor
    {
        public int Calls { get; private set; }

        public Task<ToolResult> ExecuteAsync(AgentExecutionRequest execution, ToolCall call,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new ToolResult(call.CallId, call.ToolId,
                JsonSerializer.SerializeToElement(new { ok = true }), call.Arguments));
        }
    }
}
