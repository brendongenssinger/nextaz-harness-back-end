using System.Text.Json;
using NextAZ.Harness.Domain.Tools;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.Execution;
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
        if (request.Context.AgentId.Value == "whatsapp.template.criacao")
            return Task.FromResult(new ModelExecutionResult(WhatsAppTemplateDraft(request.Content),
                Array.Empty<ToolCall>(), 0, 0, 0));
        if (request.Context.AgentId.Value == "funcionario-digital.recibo")
            return Task.FromResult(new ModelExecutionResult(JsonSerializer.Serialize(new
            {
                isReceipt = false,
                label = "simulation-not-receipt",
                score = 1m,
                threshold = 0.90m,
                requiresDetailedExtraction = false
            }), Array.Empty<ToolCall>(), 0, 0, 0));
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

    private static string WhatsAppTemplateDraft(string content)
    {
        try
        {
            using var input = JsonDocument.Parse(content);
            var language = input.RootElement.GetProperty("language").GetString();
            var category = input.RootElement.GetProperty("category").GetString();
            if (string.IsNullOrWhiteSpace(language) || string.IsNullOrWhiteSpace(category))
                throw new HarnessExecutionException("model_input_invalid");
            return JsonSerializer.Serialize(new
            {
                name = "mensagem_de_exemplo",
                language,
                category,
                bodyText = "Olá! Esta é uma mensagem de exemplo para revisão.",
                headerText = (string?)null,
                footerText = "Revise antes de enviar para aprovação.",
                headerVariableExamples = Array.Empty<string>(),
                bodyVariableExamples = Array.Empty<string>()
            });
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new HarnessExecutionException("model_input_invalid");
        }
    }
}
