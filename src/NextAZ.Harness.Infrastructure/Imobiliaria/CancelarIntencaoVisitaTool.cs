using System.Text.Json;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Domain.Tools;
using NextAZ.Harness.Runtime.Tooling;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Infrastructure.Imobiliaria;
public sealed class CancelarIntencaoVisitaTool(ImobiliariaApiClient client) : IBackendAuthorizedWriteToolHandler
{
    public ToolDefinition Definition { get; } = new(new("imobiliaria.cancelar-intencao-visita"),
        "Cancelar intencao de visita", "Cancela de forma idempotente uma intencao pendente do atendimento.",
        "{\"type\":\"object\",\"properties\":{\"atendimentoId\":{\"type\":\"string\",\"format\":\"uuid\"},\"approvalId\":{\"type\":\"string\",\"format\":\"uuid\"},\"reason\":{\"type\":\"string\"}},\"required\":[\"atendimentoId\",\"approvalId\",\"reason\"],\"additionalProperties\":false}",
        ToolRiskLevel.WriteLowRisk, true, TimeSpan.FromSeconds(5), "harness.execute");
    public bool ValidateArguments(JsonElement arguments) => arguments.EnumerateObject().Count() == 3 &&
        ImobiliariaToolArguments.GuidProperty(arguments, "atendimentoId") &&
        ImobiliariaToolArguments.GuidProperty(arguments, "approvalId") &&
        ImobiliariaToolArguments.StringProperty(arguments, "reason", 1000);
    public Task<JsonElement> ExecuteAsync(HarnessContext context, JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var atendimentoId = ImobiliariaToolArguments.GetGuid(arguments, "atendimentoId");
        var approvalId = ImobiliariaToolArguments.GetGuid(arguments, "approvalId");
        return client.PostAsync(context.TenantId,
            $"api/internal/harness/atendimentos/{atendimentoId}/visitas/intencoes/{approvalId}/cancelar", new
            {
                commandId = ImobiliariaToolArguments.CommandId(context, Definition.ToolId.Value, arguments),
                reason = arguments.GetProperty("reason").GetString()
            }, cancellationToken);
    }
}
