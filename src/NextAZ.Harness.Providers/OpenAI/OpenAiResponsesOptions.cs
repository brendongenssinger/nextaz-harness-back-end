using System.Collections.Frozen;

namespace NextAZ.Harness.Providers.OpenAI;

public sealed record OpenAiResponsesOptions(
    Uri Endpoint,
    string ApiKey,
    string Model,
    decimal? InputPricePerMillionTokens,
    decimal? OutputPricePerMillionTokens,
    string CostCurrency,
    IReadOnlySet<string> AllowedMediaHosts,
    bool AllowGlobalFallback)
{
    public static OpenAiResponsesOptions Create(
        string? endpoint,
        string? apiKey,
        string? model,
        decimal? inputPricePerMillionTokens = null,
        decimal? outputPricePerMillionTokens = null,
        string? costCurrency = "USD",
        IEnumerable<string>? allowedMediaHosts = null,
        bool allowGlobalFallback = true)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Configure an HTTPS OpenAI Responses endpoint.");
        if (allowGlobalFallback && string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Configure the OpenAI API key outside source control.");
        if (allowGlobalFallback && (string.IsNullOrWhiteSpace(model) || model.Length > 200))
            throw new InvalidOperationException("Configure an explicit OpenAI model.");
        if (inputPricePerMillionTokens.HasValue != outputPricePerMillionTokens.HasValue ||
            inputPricePerMillionTokens is < 0 or > 100000 ||
            outputPricePerMillionTokens is < 0 or > 100000)
            throw new InvalidOperationException(
                "Configure both non-negative OpenAI token prices or leave both unset.");
        var currency = string.IsNullOrWhiteSpace(costCurrency)
            ? "USD"
            : costCurrency.Trim().ToUpperInvariant();
        if (currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
            throw new InvalidOperationException("Configure an ISO 4217 cost currency.");
        var mediaHosts = (allowedMediaHosts ?? [])
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .Select(host => host.Trim().ToLowerInvariant())
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        if (mediaHosts.Any(host => host.Length > 253 ||
                !Uri.CheckHostName(host).Equals(UriHostNameType.Dns)))
            throw new InvalidOperationException("Configure valid DNS names for OpenAI media hosts.");
        return new(uri, apiKey ?? string.Empty, model ?? string.Empty, inputPricePerMillionTokens,
            outputPricePerMillionTokens, currency, mediaHosts, allowGlobalFallback);
    }

    public decimal? EstimateCost(int inputTokens, int outputTokens)
    {
        if (!InputPricePerMillionTokens.HasValue || !OutputPricePerMillionTokens.HasValue)
            return null;

        return decimal.Round(
            inputTokens * InputPricePerMillionTokens.Value / 1_000_000m +
            outputTokens * OutputPricePerMillionTokens.Value / 1_000_000m,
            8,
            MidpointRounding.AwayFromZero);
    }
}
