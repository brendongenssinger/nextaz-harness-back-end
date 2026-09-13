using System.Text.Json;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Domain.Tools;
using NextAZ.Harness.Runtime.Tooling;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Infrastructure.Imobiliaria;
public sealed class RegistrarRecomendacaoTool(ImobiliariaApiClient client) : IBackendAuthorizedWriteToolHandler
{
    public ToolDefinition Definition { get; } = new(new("imobiliaria.registrar-recomendacao"),
        "Registrar recomendacao", "Registra de forma idempotente um imovel recomendado no atendimento atual.",
        "{\"type\":\"object\",\"properties\":{\"atendimentoId\":{\"type\":\"string\",\"format\":\"uuid\"},\"imovelId\":{\"type\":\"string\",\"format\":\"uuid\"}},\"required\":[\"atendimentoId\",\"imovelId\"],\"additionalProperties\":false}",
        ToolRiskLevel.WriteLowRisk, true, TimeSpan.FromSeconds(5), "harness.execute");
    public bool ValidateArguments(JsonElement arguments) => arguments.EnumerateObject().Count() == 2 &&
        ImobiliariaToolArguments.GuidProperty(arguments, "atendimentoId") &&
        ImobiliariaToolArguments.GuidProperty(arguments, "imovelId");
    public Task<JsonElement> ExecuteAsync(HarnessContext context, JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var atendimentoId = ImobiliariaToolArguments.GetGuid(arguments, "atendimentoId");
        return client.PostAsync(context.TenantId,
            $"api/internal/harness/atendimentos/{atendimentoId}/recomendacoes", new
            {
                commandId = ImobiliariaToolArguments.CommandId(context, Definition.ToolId.Value, arguments),
                imovelId = ImobiliariaToolArguments.GetGuid(arguments, "imovelId")
            }, cancellationToken);
    }
}
