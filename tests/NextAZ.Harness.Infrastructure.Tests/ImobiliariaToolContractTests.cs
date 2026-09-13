using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Infrastructure.Imobiliaria;
using NextAZ.Harness.Runtime.Tooling;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Infrastructure.Tests;
public sealed class ImobiliariaToolContractTests
{
    [Fact]
    public async Task ContextSchemaUsesRequiredNullableOptionsForStrictProvider()
    {
        var handler = new RecordingHandler();
        var client = new ImobiliariaApiClient(new HttpClient(handler), new FixedTokenProvider(),
            ImobiliariaIntegrationOptions.Create("https://imobiliaria.test/", "https://auth.test/connect/token", "nextaz-harness", "external-secret", "imobiliaria.tools"));
        var tool = new ObterContextoAtendimentoTool(client);
        using var schema = JsonDocument.Parse(tool.Definition.InputSchema);
        var required = schema.RootElement.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Equal(schema.RootElement.GetProperty("properties").EnumerateObject().Select(x => x.Name), required);
        foreach (var name in new[] { "busca", "pagina" })
            Assert.Contains("null", schema.RootElement.GetProperty("properties").GetProperty(name).GetProperty("type").EnumerateArray().Select(x => x.GetString()));
        var args = Json(new { atendimentoId = Guid.NewGuid(), busca = (string?)null, pagina = (int?)null });
        Assert.True(tool.ValidateArguments(args));
        var context = new HarnessContext(Guid.NewGuid(), new AgentId("imobiliaria.atendimento"), new AgentVersion(1), ExecutionId.New(), "nullable", "web", "imobiliaria.atendimento", DateTimeOffset.UtcNow);
        await tool.ExecuteAsync(context, args, default);
        Assert.Equal("", handler.LastUri!.Query);
    }
    [Fact]
    public async Task ContextToolSupportsBoundedSearchPaginationWithoutQueryInjection()
    {
        var tenantId = Guid.NewGuid(); var atendimentoId = Guid.NewGuid();
        var handler = new RecordingHandler();
        var client = new ImobiliariaApiClient(new HttpClient(handler), new FixedTokenProvider(),
            ImobiliariaIntegrationOptions.Create("https://imobiliaria.test/", "https://auth.test/connect/token", "nextaz-harness", "external-secret", "imobiliaria.tools"));
        var tool = new ObterContextoAtendimentoTool(client);
        var args = Json(new { atendimentoId, busca = "Centro & Jardim", pagina = 2 });
        Assert.True(tool.ValidateArguments(args));
        Assert.False(tool.ValidateArguments(Json(new { atendimentoId, pagina = "2" })));
        Assert.False(tool.ValidateArguments(Json(new { atendimentoId, pagina = 0 })));
        Assert.False(tool.ValidateArguments(Json(new { atendimentoId, busca = new string('x', 121) })));
        Assert.False(tool.ValidateArguments(Json(new { atendimentoId, tenantId })));
        var context = new HarnessContext(tenantId, new AgentId("imobiliaria.atendimento"), new AgentVersion(1), ExecutionId.New(), "search", "web", "imobiliaria.atendimento", DateTimeOffset.UtcNow);
        await tool.ExecuteAsync(context, args, default);
        Assert.Equal("?busca=Centro%20%26%20Jardim&pagina=2", handler.LastUri!.Query);
    }
    [Fact] public async Task TypedToolsUseTenantScopedBackendContractsAndGuidCommands()
    {
        var tenantId = Guid.NewGuid();
        var atendimentoId = Guid.NewGuid();
        var imovelId = Guid.NewGuid();
        var approvalId = Guid.NewGuid();
        var token = new FixedTokenProvider();
        var handler = new RecordingHandler();
        var client = new ImobiliariaApiClient(new HttpClient(handler), token,
            ImobiliariaIntegrationOptions.Create("https://imobiliaria.test/", "https://auth.test/connect/token",
                "nextaz-harness", "external-secret", "imobiliaria.tools"));
        var context = new HarnessContext(tenantId, new AgentId("imobiliaria.atendimento"), new AgentVersion(1),
            ExecutionId.New(), "message-123", "whatsapp", "imobiliaria.atendimento", DateTimeOffset.UtcNow);
        var calls = new (IToolHandler Tool, JsonElement Arguments, HttpMethod Method, string Path)[]
        {
            (new ObterContextoAtendimentoTool(client), Json(new { atendimentoId }), HttpMethod.Get,
                $"/api/internal/harness/atendimentos/{atendimentoId}/contexto"),
            (new RegistrarRecomendacaoTool(client), Json(new { atendimentoId, imovelId }), HttpMethod.Post,
                $"/api/internal/harness/atendimentos/{atendimentoId}/recomendacoes"),
            (new CriarIntencaoVisitaTool(client), Json(new { atendimentoId, imovelId, cliente = "Cliente",
                telefone = "+5562999999999", dataHora = "2026-09-15T14:00:00-03:00", observacao = (string?)null }),
                HttpMethod.Post, $"/api/internal/harness/atendimentos/{atendimentoId}/visitas/intencoes"),
            (new ConfirmarIntencaoVisitaTool(client), Json(new { atendimentoId, approvalId }), HttpMethod.Post,
                $"/api/internal/harness/atendimentos/{atendimentoId}/visitas/intencoes/{approvalId}/confirmar"),
            (new CancelarIntencaoVisitaTool(client), Json(new { atendimentoId, approvalId, reason = "Cliente recusou" }),
                HttpMethod.Post, $"/api/internal/harness/atendimentos/{atendimentoId}/visitas/intencoes/{approvalId}/cancelar"),
            (new EncaminharCorretorTool(client), Json(new { atendimentoId, imovelId = (Guid?)null,
                regiao = "Centro", tipoImovel = "Apartamento", motivo = "Cliente pediu atendimento humano" }),
                HttpMethod.Post, $"/api/internal/harness/atendimentos/{atendimentoId}/encaminhamentos")
        };
        foreach (var call in calls)
        {
            Assert.True(call.Tool.ValidateArguments(call.Arguments));
            await call.Tool.ExecuteAsync(context, call.Arguments, default);
            var request = handler.Requests[^1];
            Assert.Equal(call.Method, request.Method);
            Assert.Equal(call.Path, request.Path);
            Assert.Equal("Bearer", request.Authorization?.Scheme);
            Assert.Equal("service-token", request.Authorization?.Parameter);
            if (call.Method == HttpMethod.Post)
            {
                using var body = JsonDocument.Parse(request.Body!);
                Assert.True(Guid.TryParse(body.RootElement.GetProperty("commandId").GetString(), out var commandId));
                Assert.NotEqual(Guid.Empty, commandId);
                Assert.False(body.RootElement.TryGetProperty("tenantId", out _));
                Assert.False(body.RootElement.TryGetProperty("confirmacaoExplicita", out _));
            }
        }
        Assert.All(token.Tenants, value => Assert.Equal(tenantId, value));
    }
    [Fact] public async Task CommandIdIsStableAcrossJsonPropertyOrder()
    {
        var tenantId = Guid.NewGuid();
        var atendimentoId = Guid.NewGuid();
        var imovelId = Guid.NewGuid();
        var context = new HarnessContext(tenantId, new AgentId("imobiliaria.atendimento"), new AgentVersion(1),
            ExecutionId.New(), "same-message", "api", "imobiliaria.atendimento", DateTimeOffset.UtcNow);
        var first = JsonSerializer.SerializeToElement(new Dictionary<string, object>
            { ["atendimentoId"] = atendimentoId, ["imovelId"] = imovelId });
        var second = JsonSerializer.SerializeToElement(new Dictionary<string, object>
            { ["imovelId"] = imovelId, ["atendimentoId"] = atendimentoId });
        var handler = new RecordingHandler();
        var client = new ImobiliariaApiClient(new HttpClient(handler), new FixedTokenProvider(),
            ImobiliariaIntegrationOptions.Create("https://imobiliaria.test/", "https://auth.test/connect/token",
                "nextaz-harness", "external-secret", "imobiliaria.tools"));
        var tool = new RegistrarRecomendacaoTool(client);
        await tool.ExecuteAsync(context, first, default);
        await tool.ExecuteAsync(context, second, default);
        using var firstBody = JsonDocument.Parse(handler.Requests[0].Body!);
        using var secondBody = JsonDocument.Parse(handler.Requests[1].Body!);
        Assert.Equal(firstBody.RootElement.GetProperty("commandId").GetString(),
            secondBody.RootElement.GetProperty("commandId").GetString());
    }
    [Fact] public async Task BackendToolResponseIsBoundedBeforeJsonParsing()
    {
        var handler = new RecordingHandler { ResponseBody = $"{{\"value\":\"{new string('x', 32_768)}\"}}" };
        var client = new ImobiliariaApiClient(new HttpClient(handler), new FixedTokenProvider(),
            ImobiliariaIntegrationOptions.Create("https://imobiliaria.test/", "https://auth.test/connect/token",
                "nextaz-harness", "external-secret", "imobiliaria.tools"));
        var error = await Assert.ThrowsAsync<NextAZ.Harness.Runtime.Execution.HarnessExecutionException>(() =>
            client.GetAsync(Guid.NewGuid(), "api/internal/harness/atendimentos/test/contexto", default));
        Assert.Equal("tool_output_too_large", error.Code);
    }
    private static JsonElement Json<T>(T value) => JsonSerializer.SerializeToElement(value);
    private sealed class FixedTokenProvider : IImobiliariaAccessTokenProvider
    {
        public List<Guid> Tenants { get; } = [];
        public Task<string> GetAsync(Guid tenantId, CancellationToken cancellationToken)
        { Tenants.Add(tenantId); return Task.FromResult("service-token"); }
    }
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }
        public List<CapturedRequest> Requests { get; } = [];
        public string ResponseBody { get; init; } =
            "{\"resourceId\":\"00000000-0000-0000-0000-000000000001\",\"alreadyProcessed\":false}";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            Requests.Add(new(request.Method, request.RequestUri!.AbsolutePath,
                request.Headers.Authorization,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new(HttpStatusCode.OK)
                { Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json") };
        }
    }
    private sealed record CapturedRequest(HttpMethod Method, string Path,
        AuthenticationHeaderValue? Authorization, string? Body);
}
