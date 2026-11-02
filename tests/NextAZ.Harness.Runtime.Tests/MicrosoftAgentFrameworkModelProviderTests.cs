using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Conversations;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Domain.Policies;
using NextAZ.Harness.Providers.AgentFramework;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.Execution;
using NextAZ.Harness.Runtime.ModelRouting;
using NextAZ.Harness.Runtime.Tooling;
using NextAZ.Harness.Tests.Fixtures;

namespace NextAZ.Harness.Runtime.Tests;

public sealed class MicrosoftAgentFrameworkModelProviderTests
{
    [Fact]
    public async Task ExecutesMicrosoftAgentFrameworkWithSchemaOnlyAllowlist()
    {
        var request = Request();
        var client = new FakeChatClient((messages, options, _) =>
        {
            Assert.Equal(request.Content, Assert.Single(messages, message => message.Role == ChatRole.User).Text);
            Assert.Contains(request.Objective, options?.Instructions);
            Assert.Equal(request.MaxOutputTokens, options?.MaxOutputTokens);
            var tool = Assert.IsAssignableFrom<AIFunctionDeclaration>(Assert.Single(options?.Tools ?? []));
            Assert.Equal("harness_execution_info", tool.Name);
            Assert.Equal(JsonValueKind.Object, tool.JsonSchema.ValueKind);
            return Task.FromResult(Response("resposta MAF", inputTokens: 7, outputTokens: 3,
                estimatedCost: 0.012m));
        });
        var provider = Provider(new FakeChatClientFactory(_ => client), request.Context.TenantId);

        var result = await provider.ExecuteAsync(request, default);

        Assert.Equal("resposta MAF", result.Content);
        Assert.Equal(7, result.InputTokens);
        Assert.Equal(3, result.OutputTokens);
        Assert.Equal(0.012m, result.EstimatedCost);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task ReturnsToolProposalWithoutExecutingItInsideSdk()
    {
        var request = Request();
        var client = new FakeChatClient((_, _, _) => Task.FromResult(new ChatResponse(
            new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("call-1", "harness_execution_info",
                new Dictionary<string, object?>())]))));
        var provider = Provider(new FakeChatClientFactory(_ => client), request.Context.TenantId);

        var result = await provider.ExecuteAsync(request, default);

        var call = Assert.Single(result.ToolCalls);
        Assert.Equal("harness.execution-info", call.ToolId.Value);
        Assert.Equal(JsonValueKind.Object, call.Arguments.ValueKind);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task RejectsToolOutsideTenantAndAgentAllowlist()
    {
        var request = Request();
        var client = new FakeChatClient((_, _, _) => Task.FromResult(new ChatResponse(
            new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("call-1", "billing_confirm_payment",
                new Dictionary<string, object?>())]))));
        var provider = Provider(new FakeChatClientFactory(_ => client), request.Context.TenantId);

        var exception = await Assert.ThrowsAsync<HarnessExecutionException>(() =>
            provider.ExecuteAsync(request, default));

        Assert.Equal("tool_forbidden", exception.Code);
    }

    [Fact]
    public async Task PreservesCallerCancellationToken()
    {
        var request = Request();
        using var source = new CancellationTokenSource();
        CancellationToken observed = default;
        var client = new FakeChatClient(async (_, _, token) =>
        {
            observed = token;
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Response("never");
        });
        var provider = Provider(new FakeChatClientFactory(_ => client), request.Context.TenantId);

        var execution = provider.ExecuteAsync(request, source.Token);
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        Assert.Equal(source.Token, observed);
    }

    [Fact]
    public async Task CreatesFreshTenantScopedChatClientForEveryExecution()
    {
        var captured = new List<ModelExecutionRequest>();
        var factory = new FakeChatClientFactory(request =>
        {
            captured.Add(request);
            return new FakeChatClient((_, _, _) => Task.FromResult(Response(request.Context.TenantId.ToString())));
        });
        var first = Request();
        var second = Request(Guid.NewGuid());
        var provider = Provider(factory, first.Context.TenantId, second.Context.TenantId);

        var firstResult = await provider.ExecuteAsync(first, default);
        var secondResult = await provider.ExecuteAsync(second, default);

        Assert.Equal(first.Context.TenantId.ToString(), firstResult.Content);
        Assert.Equal(second.Context.TenantId.ToString(), secondResult.Content);
        Assert.Collection(captured,
            item => Assert.Equal(first.Context.TenantId, item.Context.TenantId),
            item => Assert.Equal(second.Context.TenantId, item.Context.TenantId));
    }

    [Fact]
    public async Task BoundedRuntimeStillOwnsToolLimitAndExecution()
    {
        var fixture = new HarnessFixture();
        var maf = Provider(new FakeChatClientFactory(_ =>
            new FakeChatClient((_, _, _) => Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("call-1", "harness_execution_info",
                    new Dictionary<string, object?>())]))))), fixture.TenantId);
        var runtimeRequest = fixture.RuntimeRequest(new ExecutionLimits(maxToolCalls: 0)) with
        {
            Routes = [new(maf.ProviderId, "deterministic-v1", TimeSpan.FromSeconds(1))]
        };

        var exception = await Assert.ThrowsAsync<HarnessExecutionException>(() =>
            fixture.Runtime(maf).ExecuteAsync(runtimeRequest, default));

        Assert.Equal("model_output_invalid", exception.Code);
    }

    [Fact]
    public async Task RejectsNonCartorioAgent()
    {
        var request = Request();
        request = request with
        {
            Context = CopyContext(request.Context, agentId: new("imobiliaria.atendimento"))
        };
        var provider = Provider(new FakeChatClientFactory(_ =>
            throw new InvalidOperationException("chat client must not be created")), request.Context.TenantId);

        var exception = await Assert.ThrowsAsync<HarnessExecutionException>(() =>
            provider.ExecuteAsync(request, default));

        Assert.Equal("model_route_invalid", exception.Code);
    }

    [Fact]
    public async Task PreservesHarnessConversationAndToolResultContractThroughSdkBridge()
    {
        var original = Request() with
        {
            TenantInstructions = "Instrucoes exclusivas do tenant.",
            Messages = [new ConversationMessage("user", "mensagem anterior")],
            ToolResults = [new ToolResult("prior-call", new("harness.execution-info"),
                JsonSerializer.SerializeToElement(new { tenantScoped = true }),
                JsonSerializer.SerializeToElement(new { }))]
        };
        ModelExecutionRequest? delegated = null;
        var inner = new StubModelProvider("inner-provider", (request, _) =>
        {
            delegated = request;
            return Task.FromResult(new ModelExecutionResult("ok", [], 1, 1, 0));
        });
        var provider = Provider(new ModelProviderAgentFrameworkChatClientFactory(inner),
            original.Context.TenantId);

        var result = await provider.ExecuteAsync(original, default);

        Assert.Equal("ok", result.Content);
        Assert.NotNull(delegated);
        Assert.Equal("inner-provider", delegated.Route.ProviderId);
        Assert.Equal(original.Context, delegated.Context);
        Assert.Equal(original.TenantInstructions, delegated.TenantInstructions);
        Assert.Equal(original.MaxOutputTokens, delegated.MaxOutputTokens);
        Assert.Equal(original.Messages, delegated.Messages);
        Assert.Equal(original.ToolResults, delegated.ToolResults);
        Assert.Equal(original.ResourceReferences, delegated.ResourceReferences);
    }

    [Fact]
    public async Task MalformedInnerProviderOutputFailsWithHarnessError()
    {
        var inner = new StubModelProvider("inner-provider", (_, _) => Task.FromResult(
            new ModelExecutionResult(null, null!, int.MaxValue, int.MaxValue, null)));
        var request = Request();
        var provider = Provider(new ModelProviderAgentFrameworkChatClientFactory(inner),
            request.Context.TenantId);

        var exception = await Assert.ThrowsAsync<HarnessExecutionException>(() =>
            provider.ExecuteAsync(request, default));

        Assert.Equal("model_output_invalid", exception.Code);
    }

    [Fact]
    public async Task RouteTimeoutCanFallBackWithoutLosingBoundedRuntimePolicy()
    {
        var fixture = new HarnessFixture();
        var maf = Provider(new FakeChatClientFactory(_ =>
            new FakeChatClient(async (_, _, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return Response("never");
            })), fixture.TenantId);
        Guid? fallbackTenant = null;
        var fallback = new StubModelProvider("fallback", (request, _) =>
        {
            fallbackTenant = request.Context.TenantId;
            return Task.FromResult(new ModelExecutionResult("fallback-ok", [], 0, 0, null));
        });
        var runtimeRequest = fixture.RuntimeRequest() with
        {
            Routes =
            [
                new(maf.ProviderId, "deterministic-v1", TimeSpan.FromMilliseconds(20)),
                new(fallback.ProviderId, "fallback-v1", TimeSpan.FromSeconds(1))
            ]
        };

        var result = await fixture.Runtime(maf, fallback).ExecuteAsync(runtimeRequest, default);

        Assert.Equal("fallback-ok", result.Content);
        Assert.Equal("fallback", result.ProviderId);
        Assert.Equal(runtimeRequest.Context.TenantId, fallbackTenant);
    }

    [Fact]
    public async Task InnerProviderUnavailableCanUseExplicitSameRequestFallback()
    {
        var fixture = new HarnessFixture();
        var unavailable = new StubModelProvider("inner-provider",
            (_, _) => throw new ModelProviderUnavailableException());
        var maf = Provider(new ModelProviderAgentFrameworkChatClientFactory(unavailable),
            fixture.TenantId);
        Guid? fallbackTenant = null;
        var fallback = new StubModelProvider("fallback", (request, _) =>
        {
            fallbackTenant = request.Context.TenantId;
            return Task.FromResult(new ModelExecutionResult("fallback-ok", [], 0, 0, null));
        });
        var runtimeRequest = fixture.RuntimeRequest() with
        {
            Routes =
            [
                new(maf.ProviderId, "deterministic-v1", TimeSpan.FromSeconds(1)),
                new(fallback.ProviderId, "fallback-v1", TimeSpan.FromSeconds(1))
            ]
        };

        var result = await fixture.Runtime(maf, fallback).ExecuteAsync(runtimeRequest, default);

        Assert.Equal("fallback-ok", result.Content);
        Assert.Equal(runtimeRequest.Context.TenantId, fallbackTenant);
    }

    [Fact]
    public async Task RejectsMemoryFromAnotherTenantBeforeCreatingSdkClient()
    {
        var request = Request();
        request = request with { Memory = request.Memory with { TenantId = Guid.NewGuid() } };
        var provider = Provider(new FakeChatClientFactory(_ =>
            throw new InvalidOperationException("chat client must not be created")), request.Context.TenantId);

        var exception = await Assert.ThrowsAsync<HarnessExecutionException>(() =>
            provider.ExecuteAsync(request, default));

        Assert.Equal("model_route_invalid", exception.Code);
    }

    [Fact]
    public async Task RejectsCartorioTenantOutsideExplicitOptInScope()
    {
        var request = Request();
        var provider = Provider(new FakeChatClientFactory(_ =>
            throw new InvalidOperationException("chat client must not be created")), Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<HarnessExecutionException>(() =>
            provider.ExecuteAsync(request, default));

        Assert.Equal("model_route_invalid", exception.Code);
    }

    private static ModelExecutionRequest Request(Guid? tenant = null)
    {
        var fixture = new HarnessFixture();
        var runtime = fixture.RuntimeRequest();
        if (tenant.HasValue)
        {
            runtime = runtime with
            {
                Context = CopyContext(runtime.Context, tenant.Value),
                Configuration = runtime.Configuration with { TenantId = tenant.Value },
                Memory = runtime.Memory with { TenantId = tenant.Value }
            };
        }
        return new(runtime.Context, new("microsoft-agent-framework", "deterministic-v1",
                TimeSpan.FromSeconds(1)), runtime.Agent.Objective, runtime.Configuration.Instructions,
            runtime.Content, runtime.Memory, runtime.Tools, [],
            runtime.Configuration.Limits.MaxOutputTokens, runtime.Messages, runtime.ResourceReferences);
    }

    private static MicrosoftAgentFrameworkModelProvider Provider(
        IAgentFrameworkChatClientFactory factory, params Guid[] tenants) =>
        new(factory, new AgentFrameworkTenantScope(tenants));

    private static NextAZ.Harness.Domain.Executions.ExecutionContext CopyContext(
        NextAZ.Harness.Domain.Executions.ExecutionContext source, Guid? tenantId = null,
        AgentId? agentId = null) => new(tenantId ?? source.TenantId, agentId ?? source.AgentId,
        source.AgentVersion, source.ExecutionId, source.CorrelationId, source.Channel,
        source.Operation, source.Timestamp);

    private static ChatResponse Response(string content, int inputTokens = 0, int outputTokens = 0,
        decimal? estimatedCost = null) => new(new ChatMessage(ChatRole.Assistant, content))
    {
        Usage = new UsageDetails
        {
            InputTokenCount = inputTokens,
            OutputTokenCount = outputTokens,
            TotalTokenCount = inputTokens + outputTokens
        },
        AdditionalProperties = new AdditionalPropertiesDictionary
        {
            ["nextaz.estimated_cost"] = estimatedCost
        }
    };

    private sealed class FakeChatClientFactory(Func<ModelExecutionRequest, IChatClient> create)
        : IAgentFrameworkChatClientFactory
    {
        public IChatClient Create(ModelExecutionRequest request) => create(request);
    }

    private sealed class FakeChatClient(
        Func<IReadOnlyList<ChatMessage>, ChatOptions?, CancellationToken, Task<ChatResponse>> response)
        : IChatClient
    {
        public int CallCount { get; private set; }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return response(messages.ToArray(), options, cancellationToken);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var result = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var update in result.ToChatResponseUpdates()) yield return update;
        }

        public void Dispose() { }
    }
}
