using System.Text.Json;
using NextAZ.Harness.Domain.Tools;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.ModelRouting;
using NextAZ.Harness.Runtime.Tooling;
namespace NextAZ.Harness.Providers.Simulation;
// Deliberately deterministic; makes no network calls and performs no language understanding.
public sealed class SimulationModelProvider : IModelProvider
{
    public string ProviderId => "simulation";
    public Task<ModelExecutionResult> ExecuteAsync(ModelExecutionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Route.ModelId != "deterministic-v1") throw new ModelProviderUnavailableException();
        if (request.Context.Operation == "funcionario-digital.recibo")
            return Task.FromResult(new ModelExecutionResult(
                "{\"isSale\":true,\"confidence\":0.99,\"issuerName\":\"EMPRESA TESTE\",\"issuerTaxId\":null," +
                "\"saleNumber\":\"SIM-1\",\"customerName\":\"Cliente Teste\",\"customerPhone\":null," +
                "\"customerTaxId\":null,\"customerEmail\":null,\"soldAt\":null,\"total\":10.0," +
                "\"discount\":0,\"surcharge\":0,\"currency\":\"BRL\",\"paymentMethod\":\"PIX\"," +
                "\"items\":[{\"productId\":\"SIM-1\",\"productName\":\"Produto teste\",\"quantity\":1," +
                "\"unitPrice\":10.0,\"total\":10.0}],\"extractedText\":\"VALOR PAGO R$ 10,00\"}",
                Array.Empty<ToolCall>(), 0, 0, 0));
        var imobiliariaContext = new ToolId("imobiliaria.contexto-atendimento");
        if (request.ToolResults.Count == 0 && request.Tools.Any(t => t.ToolId == imobiliariaContext) &&
            request.ResourceReferences?.TryGetValue("atendimentoId", out var atendimentoId) == true)
            return Task.FromResult(new ModelExecutionResult(null,
                [new ToolCall("imobiliaria-context-1", imobiliariaContext,
                    JsonSerializer.SerializeToElement(new { atendimentoId, busca = (string?)null, pagina = (int?)null }))], 0, 0, 0));
        var tool = new ToolId("harness.execution-info");
        if (request.ToolResults.Count == 0 && request.Tools.Any(t => t.ToolId == tool))
            return Task.FromResult(new ModelExecutionResult(null,
                [new ToolCall("diagnostic-1", tool, JsonSerializer.SerializeToElement(new { }))], 0, 0, 0));
        return Task.FromResult(new ModelExecutionResult(
            request.ToolResults.Any(result => result.ToolId == imobiliariaContext)
                ? "[SIMULACAO] Fluxo imobiliario e consulta tipada de contexto validados. Nenhum modelo externo foi acionado."
                : "[SIMULACAO] Fluxo do Harness validado. Nenhum modelo externo ou servico de negocio foi acionado.",
            Array.Empty<ToolCall>(), 0, 0, 0));
    }
}
