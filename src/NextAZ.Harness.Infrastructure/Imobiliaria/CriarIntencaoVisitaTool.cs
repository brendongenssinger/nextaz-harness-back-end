using System.Text.Json;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Domain.Tools;
using NextAZ.Harness.Runtime.Tooling;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Infrastructure.Imobiliaria;
public sealed class CriarIntencaoVisitaTool(ImobiliariaApiClient client) : IBackendAuthorizedWriteToolHandler
{
    public ToolDefinition Definition { get; } = new(new("imobiliaria.criar-intencao-visita"),
        "Preparar visita", "Cria uma intencao pendente para revisao e confirmacao humana pelo corretor no produto. Nao confirma nem agenda a visita.",
        "{\"type\":\"object\",\"properties\":{\"atendimentoId\":{\"type\":\"string\",\"format\":\"uuid\"},\"imovelId\":{\"type\":\"string\",\"format\":\"uuid\"},\"cliente\":{\"type\":\"string\"},\"telefone\":{\"type\":\"string\"},\"dataHora\":{\"type\":\"string\",\"format\":\"date-time\"},\"observacao\":{\"type\":[\"string\",\"null\"]}},\"required\":[\"atendimentoId\",\"imovelId\",\"cliente\",\"telefone\",\"dataHora\",\"observacao\"],\"additionalProperties\":false}",
        ToolRiskLevel.WriteLowRisk, true, TimeSpan.FromSeconds(5), "harness.execute");
    public bool ValidateArguments(JsonElement arguments) => arguments.EnumerateObject().Count() == 6 &&
        ImobiliariaToolArguments.GuidProperty(arguments, "atendimentoId") &&
        ImobiliariaToolArguments.GuidProperty(arguments, "imovelId") &&
        ImobiliariaToolArguments.StringProperty(arguments, "cliente", 200) &&
        ImobiliariaToolArguments.StringProperty(arguments, "telefone", 64) &&
        ImobiliariaToolArguments.DateProperty(arguments, "dataHora") &&
        ImobiliariaToolArguments.StringProperty(arguments, "observacao", 2000, false);
    public Task<JsonElement> ExecuteAsync(HarnessContext context, JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var atendimentoId = ImobiliariaToolArguments.GetGuid(arguments, "atendimentoId");
        return client.PostAsync(context.TenantId,
            $"api/internal/harness/atendimentos/{atendimentoId}/visitas/intencoes", new
            {
                commandId = ImobiliariaToolArguments.CommandId(context, Definition.ToolId.Value, arguments),
                imovelId = ImobiliariaToolArguments.GetGuid(arguments, "imovelId"),
                cliente = arguments.GetProperty("cliente").GetString(),
                telefone = arguments.GetProperty("telefone").GetString(),
                dataHora = arguments.GetProperty("dataHora").GetString(),
                observacao = arguments.GetProperty("observacao").ValueKind == JsonValueKind.Null
                    ? null : arguments.GetProperty("observacao").GetString()
            }, cancellationToken);
    }
}
