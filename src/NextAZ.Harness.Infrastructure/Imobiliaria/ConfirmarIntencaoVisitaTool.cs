using System.Text.Json;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Domain.Tools;
using NextAZ.Harness.Runtime.Tooling;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Infrastructure.Imobiliaria;
public sealed class ConfirmarIntencaoVisitaTool(ImobiliariaApiClient client) : IBackendAuthorizedWriteToolHandler
{
    public ToolDefinition Definition { get; } = new(new("imobiliaria.confirmar-intencao-visita"),
        "Confirmar visita pendente", "Operacao reservada a confirmacao humana no produto; indisponivel para o agente imobiliario e rejeitada pelo backend para credenciais de maquina.",
        "{\"type\":\"object\",\"properties\":{\"atendimentoId\":{\"type\":\"string\",\"format\":\"uuid\"},\"approvalId\":{\"type\":\"string\",\"format\":\"uuid\"}},\"required\":[\"atendimentoId\",\"approvalId\"],\"additionalProperties\":false}",
        ToolRiskLevel.WriteLowRisk, true, TimeSpan.FromSeconds(5), "harness.execute");
    public bool ValidateArguments(JsonElement arguments) => arguments.EnumerateObject().Count() == 2 &&
        ImobiliariaToolArguments.GuidProperty(arguments, "atendimentoId") &&
        ImobiliariaToolArguments.GuidProperty(arguments, "approvalId");
    public Task<JsonElement> ExecuteAsync(HarnessContext context, JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var atendimentoId = ImobiliariaToolArguments.GetGuid(arguments, "atendimentoId");
        var approvalId = ImobiliariaToolArguments.GetGuid(arguments, "approvalId");
        return client.PostAsync(context.TenantId,
            $"api/internal/harness/atendimentos/{atendimentoId}/visitas/intencoes/{approvalId}/confirmar",
            new { commandId = ImobiliariaToolArguments.CommandId(context, Definition.ToolId.Value, arguments) },
            cancellationToken);
    }
}
