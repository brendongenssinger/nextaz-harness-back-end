using System.Text.Json;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Domain.Tools;
using NextAZ.Harness.Runtime.Tooling;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Infrastructure.Imobiliaria;
public sealed class EncaminharCorretorTool(ImobiliariaApiClient client) : IBackendAuthorizedWriteToolHandler
{
    public ToolDefinition Definition { get; } = new(new("imobiliaria.encaminhar-corretor"),
        "Encaminhar para corretor", "Registra encaminhamento idempotente e pausa a IA no atendimento.",
        "{\"type\":\"object\",\"properties\":{\"atendimentoId\":{\"type\":\"string\",\"format\":\"uuid\"},\"imovelId\":{\"type\":[\"string\",\"null\"],\"format\":\"uuid\"},\"regiao\":{\"type\":[\"string\",\"null\"]},\"tipoImovel\":{\"type\":[\"string\",\"null\"]},\"motivo\":{\"type\":\"string\"}},\"required\":[\"atendimentoId\",\"imovelId\",\"regiao\",\"tipoImovel\",\"motivo\"],\"additionalProperties\":false}",
        ToolRiskLevel.WriteLowRisk, true, TimeSpan.FromSeconds(5), "harness.execute");
    public bool ValidateArguments(JsonElement arguments) => arguments.EnumerateObject().Count() == 5 &&
        ImobiliariaToolArguments.GuidProperty(arguments, "atendimentoId") &&
        ImobiliariaToolArguments.OptionalGuid(arguments, "imovelId") &&
        ImobiliariaToolArguments.StringProperty(arguments, "regiao", 200, false) &&
        ImobiliariaToolArguments.StringProperty(arguments, "tipoImovel", 120, false) &&
        ImobiliariaToolArguments.StringProperty(arguments, "motivo", 1000);
    public Task<JsonElement> ExecuteAsync(HarnessContext context, JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var atendimentoId = ImobiliariaToolArguments.GetGuid(arguments, "atendimentoId");
        return client.PostAsync(context.TenantId,
            $"api/internal/harness/atendimentos/{atendimentoId}/encaminhamentos", new
            {
                commandId = ImobiliariaToolArguments.CommandId(context, Definition.ToolId.Value, arguments),
                imovelId = arguments.GetProperty("imovelId").ValueKind == JsonValueKind.Null
                    ? null : arguments.GetProperty("imovelId").GetString(),
                regiao = arguments.GetProperty("regiao").ValueKind == JsonValueKind.Null
                    ? null : arguments.GetProperty("regiao").GetString(),
                tipoImovel = arguments.GetProperty("tipoImovel").ValueKind == JsonValueKind.Null
                    ? null : arguments.GetProperty("tipoImovel").GetString(),
                motivo = arguments.GetProperty("motivo").GetString()
            }, cancellationToken);
    }
}
