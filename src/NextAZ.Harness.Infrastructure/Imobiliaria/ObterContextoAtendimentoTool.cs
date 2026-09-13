using System.Text.Json;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Domain.Tools;
using NextAZ.Harness.Runtime.Tooling;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Infrastructure.Imobiliaria;
public sealed class ObterContextoAtendimentoTool(ImobiliariaApiClient client) : IToolHandler
{
    public ToolDefinition Definition { get; } = new(new("imobiliaria.contexto-atendimento"),
        "Consultar contexto do atendimento", "Consulta historico recente, estado do atendimento, visitas e uma pagina de imoveis do tenant. Busca opcional filtra palavras em titulo, tipo, finalidade, bairro, cidade e regiao. Use pagina e temMaisImoveis; contexto resumido nao representa todo o catalogo. Respostas no historico sao registros e nao comprovam entrega ao cliente.",
        "{\"type\":\"object\",\"properties\":{\"atendimentoId\":{\"type\":\"string\",\"format\":\"uuid\"},\"busca\":{\"type\":[\"string\",\"null\"],\"maxLength\":120},\"pagina\":{\"type\":[\"integer\",\"null\"],\"minimum\":1,\"maximum\":1000}},\"required\":[\"atendimentoId\",\"busca\",\"pagina\"],\"additionalProperties\":false}",
        ToolRiskLevel.Read, true, TimeSpan.FromSeconds(5), "harness.execute");
    public bool ValidateArguments(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object || !ImobiliariaToolArguments.GuidProperty(arguments, "atendimentoId")) return false;
        var names = arguments.EnumerateObject().Select(x => x.Name).ToArray();
        return names.Distinct(StringComparer.Ordinal).Count() == names.Length
            && names.All(name => name is "atendimentoId" or "busca" or "pagina")
            && (!arguments.TryGetProperty("busca", out var search) || search.ValueKind == JsonValueKind.Null || search.ValueKind == JsonValueKind.String && search.GetString()!.Length <= 120)
            && (!arguments.TryGetProperty("pagina", out var page) || page.ValueKind == JsonValueKind.Null || page.ValueKind == JsonValueKind.Number && page.TryGetInt32(out var value) && value is >= 1 and <= 1000);
    }
    public Task<JsonElement> ExecuteAsync(HarnessContext context, JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (arguments.TryGetProperty("busca", out var search) && search.ValueKind != JsonValueKind.Null) query.Add("busca=" + Uri.EscapeDataString(search.GetString()!));
        if (arguments.TryGetProperty("pagina", out var page) && page.ValueKind != JsonValueKind.Null) query.Add("pagina=" + page.GetInt32());
        return client.GetAsync(context.TenantId,
            $"api/internal/harness/atendimentos/{ImobiliariaToolArguments.GetGuid(arguments, "atendimentoId")}/contexto"
                + (query.Count == 0 ? "" : "?" + string.Join("&", query)), cancellationToken);
    }
}
