namespace NextAZ.Harness.Providers.HuggingFace;

public sealed record HuggingFaceImageClassificationOptions(
    Uri Endpoint,
    string ApiKey,
    string Model,
    IReadOnlySet<string> ReceiptLabels,
    decimal MinimumScore,
    IReadOnlySet<string> AllowedMediaHosts,
    int MaximumMediaBytes,
    decimal? EstimatedCostPerRequest)
{
    public static HuggingFaceImageClassificationOptions Create(
        string? endpoint,
        string? apiKey,
        string? model,
        IEnumerable<string>? receiptLabels,
        decimal? minimumScore,
        IEnumerable<string>? allowedMediaHosts,
        int? maximumMediaBytes,
        decimal? estimatedCostPerRequest)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
            endpointUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(endpointUri.UserInfo) ||
            !string.IsNullOrEmpty(endpointUri.Query) || !string.IsNullOrEmpty(endpointUri.Fragment))
            throw new InvalidOperationException("Configure a trusted HTTPS Hugging Face inference endpoint.");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Configure the Hugging Face API key outside source control.");
        if (string.IsNullOrWhiteSpace(model) || model.Length > 200)
            throw new InvalidOperationException("Configure the exact Hugging Face model identifier.");

        var labels = (receiptLabels ?? []).Select(label => label.Trim())
            .Where(label => label.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (labels.Count == 0 || labels.Any(label => label.Length > 100))
            throw new InvalidOperationException("Configure at least one exact receipt label from the trained model.");

        var score = minimumScore ?? 0.90m;
        if (score is <= 0 or > 1)
            throw new InvalidOperationException("Hugging Face receipt minimum score must be greater than zero and at most one.");

        var hosts = (allowedMediaHosts ?? []).Select(host => host.Trim().TrimEnd('.'))
            .Where(host => host.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (hosts.Count == 0 || hosts.Any(host => host.Length > 253 || host.Contains('/') || host.Contains(':')))
            throw new InvalidOperationException("Configure explicit media host allowlists for receipt classification.");

        var mediaLimit = maximumMediaBytes ?? 10 * 1024 * 1024;
        if (mediaLimit is < 1024 or > 20 * 1024 * 1024)
            throw new InvalidOperationException("Receipt media size limit must be between 1 KiB and 20 MiB.");
        if (estimatedCostPerRequest < 0)
            throw new InvalidOperationException("Estimated Hugging Face request cost cannot be negative.");

        return new(endpointUri, apiKey.Trim(), model.Trim(), labels, score, hosts, mediaLimit,
            estimatedCostPerRequest);
    }
}
