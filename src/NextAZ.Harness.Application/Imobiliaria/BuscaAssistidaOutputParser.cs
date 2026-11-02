using System.Text.Json;
using NextAZ.Harness.Contracts.Imobiliaria;

namespace NextAZ.Harness.Application.Imobiliaria;

internal static class BuscaAssistidaOutputParser
{
    private static readonly HashSet<string> RootProperties =
        ["filtrosInterpretados", "camposNaoEntendidos"];
    private static readonly HashSet<string> FilterProperties =
    [
        "finalidade", "regiao", "tipo", "precoMin", "precoMax", "quartosMin", "vagasMin",
        "areaMin", "areaMax"
    ];
    private static readonly HashSet<string> AllowedFinalidades = ["buy", "rent"];
    private static readonly HashSet<string> AllowedTipos = ["house", "apartment", "land", "commercial"];
    private static readonly HashSet<string> AllowedUnknownFields =
    [
        "finalidade", "regiao", "tipo", "precoMin", "precoMax", "quartosMin", "vagasMin",
        "areaMin", "areaMax", "quintal", "piscina", "varanda", "churrasqueira", "elevador",
        "aceita_pet", "outros_detalhes"
    ];

    public static bool TryParse(string content, out BuscaAssistidaFiltros filters,
        out IReadOnlyList<string> unknownFields)
    {
        filters = new();
        unknownFields = [];
        try
        {
            using var document = JsonDocument.Parse(content, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !HasOnlyUniqueProperties(root, RootProperties) ||
                !root.TryGetProperty("filtrosInterpretados", out var filterElement) ||
                !root.TryGetProperty("camposNaoEntendidos", out var unknownElement) ||
                !TryParseFilters(filterElement, out filters) ||
                !TryParseUnknownFields(unknownElement, out unknownFields))
                return false;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseFilters(JsonElement element, out BuscaAssistidaFiltros filters)
    {
        filters = new();
        if (element.ValueKind != JsonValueKind.Object || !HasExactlyUniqueProperties(element, FilterProperties))
            return false;

        if (!TryOptionalEnum(element, "finalidade", AllowedFinalidades, out var finalidade) ||
            !TryOptionalSafeString(element, "regiao", 160, out var regiao) ||
            !TryOptionalEnum(element, "tipo", AllowedTipos, out var tipo) ||
            !TryOptionalDecimal(element, "precoMin", out var precoMin) ||
            !TryOptionalDecimal(element, "precoMax", out var precoMax) ||
            !TryOptionalInt(element, "quartosMin", out var quartosMin) ||
            !TryOptionalInt(element, "vagasMin", out var vagasMin) ||
            !TryOptionalDecimal(element, "areaMin", out var areaMin) ||
            !TryOptionalDecimal(element, "areaMax", out var areaMax) ||
            (precoMin.HasValue && precoMax.HasValue && precoMin > precoMax) ||
            (areaMin.HasValue && areaMax.HasValue && areaMin > areaMax))
            return false;

        filters = new()
        {
            Finalidade = finalidade,
            Regiao = regiao,
            Tipo = tipo,
            PrecoMin = precoMin,
            PrecoMax = precoMax,
            QuartosMin = quartosMin,
            VagasMin = vagasMin,
            AreaMin = areaMin,
            AreaMax = areaMax
        };
        return true;
    }

    private static bool TryParseUnknownFields(JsonElement element, out IReadOnlyList<string> values)
    {
        values = [];
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() > 10)
            return false;
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } value ||
                !AllowedUnknownFields.Contains(value) || !seen.Add(value))
                return false;
            result.Add(value);
        }
        values = result;
        return true;
    }

    private static bool HasOnlyUniqueProperties(JsonElement element, IReadOnlySet<string> allowed)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!allowed.Contains(property.Name) || !seen.Add(property.Name)) return false;
        return true;
    }

    private static bool HasExactlyUniqueProperties(JsonElement element, IReadOnlySet<string> expected)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!expected.Contains(property.Name) || !seen.Add(property.Name)) return false;
        return seen.SetEquals(expected);
    }

    private static bool TryOptionalEnum(JsonElement element, string name, IReadOnlySet<string> allowed,
        out string? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null) return true;
        if (property.ValueKind != JsonValueKind.String || property.GetString() is not { } parsed ||
            !allowed.Contains(parsed)) return false;
        value = parsed;
        return true;
    }

    private static bool TryOptionalSafeString(JsonElement element, string name, int maxLength, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null) return true;
        if (property.ValueKind != JsonValueKind.String || property.GetString() is not { } parsed) return false;
        parsed = parsed.Trim();
        if (parsed.Length is 0 || parsed.Length > maxLength || IsUnsafeFreeText(parsed)) return false;
        value = parsed;
        return true;
    }

    private static bool IsUnsafeFreeText(string value)
    {
        if (value.Any(char.IsControl) || value.Contains("://", StringComparison.Ordinal) ||
            value.Contains("www.", StringComparison.OrdinalIgnoreCase) ||
            value.IndexOfAny(['<', '>', '{', '}', ';']) >= 0)
            return true;
        var normalized = " " + value.ToLowerInvariant() + " ";
        return normalized.Contains(" select ", StringComparison.Ordinal) ||
            normalized.Contains(" union ", StringComparison.Ordinal) ||
            normalized.Contains(" insert ", StringComparison.Ordinal) ||
            normalized.Contains(" update ", StringComparison.Ordinal) ||
            normalized.Contains(" delete ", StringComparison.Ordinal) ||
            normalized.Contains(" drop ", StringComparison.Ordinal) ||
            normalized.Contains("--", StringComparison.Ordinal);
    }

    private static bool TryOptionalDecimal(JsonElement element, string name, out decimal? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null) return true;
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetDecimal(out var parsed) || parsed < 0)
            return false;
        value = parsed;
        return true;
    }

    private static bool TryOptionalInt(JsonElement element, string name, out int? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null) return true;
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var parsed) || parsed < 0)
            return false;
        value = parsed;
        return true;
    }
}
