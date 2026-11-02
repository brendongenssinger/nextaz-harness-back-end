using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextAZ.Harness.Contracts.Imobiliaria;

public sealed class BuscaAssistidaRequest
{
    public Guid TenantId { get; init; }
    public Guid CorrelationId { get; init; }
    public string? Texto { get; init; }
    public BuscaAssistidaFiltros? FiltrosAtuais { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed class BuscaAssistidaFiltros
{
    public string? Finalidade { get; init; }
    public string? Regiao { get; init; }
    public string? Tipo { get; init; }
    public decimal? PrecoMin { get; init; }
    public decimal? PrecoMax { get; init; }
    public int? QuartosMin { get; init; }
    public int? VagasMin { get; init; }
    public decimal? AreaMin { get; init; }
    public decimal? AreaMax { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record BuscaAssistidaResponse(Guid ExecutionId, Guid CorrelationId,
    BuscaAssistidaFiltros FiltrosInterpretados, IReadOnlyList<string> CamposNaoEntendidos);
