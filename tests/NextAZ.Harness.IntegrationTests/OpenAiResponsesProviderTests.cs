using System.Collections.Frozen;
using System.Net;
using System.Text;
using System.Text.Json;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Domain.Tools;
using NextAZ.Harness.Providers.OpenAI;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.Execution;
using NextAZ.Harness.Runtime.Memory;
using NextAZ.Harness.Runtime.ModelRouting;
using NextAZ.Harness.Runtime.Tooling;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.IntegrationTests;
public sealed class OpenAiResponsesProviderTests
{
    [Fact] public async Task SendsResponsesContractWithoutPersistingOrLeakingSecret()
    {
        var handler = new CapturingHandler((_, _) => Json(HttpStatusCode.OK,
            "{\"id\":\"resp_1\",\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"Resposta\"}]}],\"usage\":{\"input_tokens\":7,\"output_tokens\":3}}"));
        var provider = Provider(handler);
        var result = await provider.ExecuteAsync(Request(), default);
        Assert.Equal("Resposta", result.Content);
        Assert.Equal(7, result.InputTokens);
        Assert.Equal(3, result.OutputTokens);
        Assert.Null(result.EstimatedCost);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-secret", handler.AuthorizationParameter);
        Assert.DoesNotContain("test-secret", handler.Body);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.False(body.RootElement.GetProperty("store").GetBoolean());
        Assert.False(body.RootElement.GetProperty("parallel_tool_calls").GetBoolean());
        Assert.Equal("configured-model", body.RootElement.GetProperty("model").GetString());
        Assert.True(body.RootElement.TryGetProperty("tools", out _));
    }
    [Fact] public async Task ResolvesCredentialAndModelFromTenantAgentRoute()
    {
        var handler = new CapturingHandler((_, _) => Json(HttpStatusCode.OK,
            "{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"ok\"}]}],\"usage\":{\"input_tokens\":1,\"output_tokens\":1}}"));
        var request = Request();
        var connectionId = Guid.NewGuid();
        request = request with
        {
            Route = new("openai-responses", "gpt-4o-mini", TimeSpan.FromSeconds(1), connectionId)
        };
        var options = OpenAiResponsesOptions.Create("https://fallback.invalid/v1/", null, null,
            allowGlobalFallback: false);
        var resolver = new StubConnectionResolver(request.Context.TenantId, connectionId,
            new(connectionId, "openai-responses", new("https://tenant-openai.example/v1/"),
                "tenant-secret"));

        await new OpenAiResponsesModelProvider(new HttpClient(handler), options, resolver)
            .ExecuteAsync(request, default);

        Assert.Equal("tenant-secret", handler.AuthorizationParameter);
        Assert.Equal("https://tenant-openai.example/v1/responses", handler.RequestUri);
        Assert.DoesNotContain("tenant-secret", handler.Body);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("gpt-4o-mini", body.RootElement.GetProperty("model").GetString());
    }
    [Fact] public async Task IncompleteResponseFailsClosed()
    {
        var provider = Provider(new CapturingHandler((_, _) => Json(HttpStatusCode.OK,
            "{\"status\":\"incomplete\",\"incomplete_details\":{\"reason\":\"max_output_tokens\"},\"output\":[],\"usage\":{\"input_tokens\":1,\"output_tokens\":1}}")));
        var error = await Assert.ThrowsAsync<HarnessExecutionException>(() => provider.ExecuteAsync(Request(), default));
        Assert.Equal("provider_response_incomplete", error.Code);
    }
    [Fact] public async Task CalculatesConfiguredTokenCostAsExecutionSnapshot()
    {
        var handler = new CapturingHandler((_, _) => Json(HttpStatusCode.OK,
            "{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"ok\"}]}],\"usage\":{\"input_tokens\":750000,\"output_tokens\":250000}}"));
        var options = OpenAiResponsesOptions.Create("https://api.openai.com/v1/", "test-secret",
            "configured-model", 2m, 8m, "usd");

        var result = await new OpenAiResponsesModelProvider(new HttpClient(handler), options)
            .ExecuteAsync(Request(), default);

        Assert.Equal(3.5m, result.EstimatedCost);
        Assert.Equal("USD", options.CostCurrency);
    }
    [Fact] public async Task SendsAllowlistedImageWithStrictStructuredOutput()
    {
        var handler = new CapturingHandler((_, _) => Json(HttpStatusCode.OK,
            "{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"{\\\"isReceipt\\\":false}\"}]}],\"usage\":{\"input_tokens\":10,\"output_tokens\":4}}"));
        var options = OpenAiResponsesOptions.Create("https://api.openai.com/v1/", "test-secret",
            "configured-model", allowedMediaHosts: ["nextazstorage.blob.core.windows.net"]);
        var provider = new OpenAiResponsesModelProvider(new HttpClient(handler), options);
        var schema = new StructuredOutputDefinition("receipt_classification", "Receipt classification",
            "{\"type\":\"object\",\"properties\":{\"isReceipt\":{\"type\":\"boolean\"}},\"required\":[\"isReceipt\"],\"additionalProperties\":false}");
        var request = Request() with
        {
            StructuredOutput = schema,
            MediaInputs = [new("image",
                "https://nextazstorage.blob.core.windows.net/midias/receipt.jpg?sig=test-only", "low")]
        };

        await provider.ExecuteAsync(request, default);

        using var body = JsonDocument.Parse(handler.Body!);
        var input = body.RootElement.GetProperty("input");
        var user = input.EnumerateArray().Single();
        var content = user.GetProperty("content");
        Assert.Contains(content.EnumerateArray(), part =>
            part.GetProperty("type").GetString() == "input_image" &&
            part.GetProperty("detail").GetString() == "low");
        var format = body.RootElement.GetProperty("text").GetProperty("format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.Equal("receipt_classification", format.GetProperty("name").GetString());
        Assert.True(format.GetProperty("strict").GetBoolean());
        Assert.False(format.GetProperty("schema").GetProperty("additionalProperties").GetBoolean());
    }
    [Fact] public async Task RejectsImageHostOutsideProviderAllowlist()
    {
        var provider = Provider(new CapturingHandler((_, _) => Json(HttpStatusCode.OK, "{}")));
        var request = Request() with
        {
            MediaInputs = [new("image", "https://attacker.example/receipt.jpg", "low")]
        };

        var error = await Assert.ThrowsAsync<HarnessExecutionException>(() =>
            provider.ExecuteAsync(request, default));

        Assert.Equal("media_source_forbidden", error.Code);
    }
    [Fact] public void PricingRequiresBothSides()
    {
        var error = Assert.Throws<InvalidOperationException>(() => OpenAiResponsesOptions.Create(
            "https://api.openai.com/v1/", "test-secret", "configured-model", 2m, null));

        Assert.Contains("both", error.Message, StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task TransientHttpFailureAllowsRouterFallback(HttpStatusCode status)
    {
        var provider = Provider(new CapturingHandler((_, _) => Json(status, "{}")));
        await Assert.ThrowsAsync<ModelProviderUnavailableException>(() => provider.ExecuteAsync(Request(), default));
    }
    [Fact] public async Task CancellationReachesHttpBoundary()
    {
        var provider = Provider(new CapturingHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Json(HttpStatusCode.OK, "{}");
        }));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.ExecuteAsync(Request(), cancellation.Token));
    }
    [Fact] public async Task RejectsUsageOutsideIntegerBounds()
    {
        var provider = Provider(new CapturingHandler((_, _) => Json(HttpStatusCode.OK,
            "{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"x\"}]}],\"usage\":{\"input_tokens\":999999999999,\"output_tokens\":1}}")));
        var error = await Assert.ThrowsAsync<HarnessExecutionException>(() => provider.ExecuteAsync(Request(), default));
        Assert.Equal("model_output_invalid", error.Code);
    }
    [Fact] public async Task SendsTypedToolResultBackToResponsesApi()
    {
        var handler = new CapturingHandler((_, _) => Json(HttpStatusCode.OK,
            "{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"ok\"}]}],\"usage\":{\"input_tokens\":1,\"output_tokens\":1}}"));
        var arguments = JsonSerializer.SerializeToElement(new { atendimentoId = Guid.NewGuid() });
        var output = JsonSerializer.SerializeToElement(new { imoveis = Array.Empty<object>() });
        await Provider(handler).ExecuteAsync(Request([new("call_1", new("imobiliaria.contexto-atendimento"),
            output, arguments)]), default);
        using var body = JsonDocument.Parse(handler.Body!);
        var input = body.RootElement.GetProperty("input");
        Assert.Contains(input.EnumerateArray(), item => item.TryGetProperty("type", out var type) &&
            type.GetString() == "function_call_output");
        Assert.Contains(input.EnumerateArray(), item => item.TryGetProperty("type", out var type) &&
            type.GetString() == "function_call" && item.GetProperty("name").GetString() ==
            "imobiliaria_contexto_atendimento");
    }
    [Fact] public async Task IncludesConversationAndTrustedResourceReferences()
    {
        var handler = new CapturingHandler((_, _) => Json(HttpStatusCode.OK,
            "{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"ok\"}]}],\"usage\":{\"input_tokens\":1,\"output_tokens\":1}}"));
        var atendimentoId = Guid.NewGuid().ToString();
        var request = Request() with
        {
            Messages = [new("user", "mensagem anterior"), new("assistant", "resposta anterior")],
            ResourceReferences = new Dictionary<string, string> { ["atendimentoId"] = atendimentoId }
        };
        await Provider(handler).ExecuteAsync(request, default);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(JsonValueKind.Array, body.RootElement.GetProperty("input").ValueKind);
        Assert.Contains(atendimentoId, body.RootElement.GetProperty("instructions").GetString());
    }
    [Fact] public async Task OmitsToolsWhenNoneAreAuthorized()
    {
        var handler = new CapturingHandler((_, _) => Json(HttpStatusCode.OK,
            "{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"ok\"}]}],\"usage\":{\"input_tokens\":1,\"output_tokens\":1}}"));
        await Provider(handler).ExecuteAsync(Request() with { Tools = [] }, default);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.False(body.RootElement.TryGetProperty("tools", out _));
    }
    [Fact] public async Task NetworkFailureAllowsRouterFallback()
    {
        var provider = Provider(new CapturingHandler(
            (Func<HttpRequestMessage, CancellationToken, HttpResponseMessage>)((_, _) =>
                throw new HttpRequestException("synthetic failure"))));
        await Assert.ThrowsAsync<ModelProviderUnavailableException>(() => provider.ExecuteAsync(Request(), default));
    }
    [Fact] public async Task OversizedProviderResponseFailsClosed()
    {
        var response = "{\"status\":\"completed\",\"padding\":\"" + new string('x', 1_048_576) + "\"}";
        var provider = Provider(new CapturingHandler((_, _) => Json(HttpStatusCode.OK, response)));
        var error = await Assert.ThrowsAsync<HarnessExecutionException>(() => provider.ExecuteAsync(Request(), default));
        Assert.Equal("model_output_invalid", error.Code);
    }
    private static OpenAiResponsesModelProvider Provider(HttpMessageHandler handler) => new(new HttpClient(handler),
        OpenAiResponsesOptions.Create("https://api.openai.com/v1/", "test-secret", "configured-model"));
    private static ModelExecutionRequest Request(IReadOnlyList<ToolResult>? results = null)
    {
        var tenant = Guid.NewGuid();
        var context = new HarnessContext(tenant, new AgentId("imobiliaria.atendimento"), new AgentVersion(1),
            ExecutionId.New(), "corr", "api", "imobiliaria.atendimento", DateTimeOffset.UtcNow);
        var tool = new ToolDefinition(new("imobiliaria.contexto-atendimento"), "context", "context",
            "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}", ToolRiskLevel.Read,
            true, TimeSpan.FromSeconds(1), "harness.execute");
        return new(context, new("openai-responses", "configured-model", TimeSpan.FromSeconds(1)),
            "objective", "tenant instruction", "customer input", new MemoryContext(tenant, null, []),
            [tool], results ?? [], 128);
    }
    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class CapturingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response)
        : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string? RequestUri { get; private set; }
        public CapturingHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> response)
            : this((message, token) => Task.FromResult(response(message, token))) { }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            RequestUri = request.RequestUri?.AbsoluteUri;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await response(request, cancellationToken);
        }
    }
    private sealed class StubConnectionResolver(Guid tenantId, Guid connectionId,
        ProviderConnectionCredentials credentials) : IProviderConnectionResolver
    {
        public Task<ProviderConnectionCredentials> ResolveAsync(Guid requestedTenantId,
            Guid requestedConnectionId, CancellationToken cancellationToken)
        {
            Assert.Equal(tenantId, requestedTenantId);
            Assert.Equal(connectionId, requestedConnectionId);
            return Task.FromResult(credentials);
        }
    }
}
