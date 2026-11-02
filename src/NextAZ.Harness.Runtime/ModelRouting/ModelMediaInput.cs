namespace NextAZ.Harness.Runtime.ModelRouting;

public sealed record ModelMediaInput
{
    public string Type { get; }
    public string Url { get; }
    public string Detail { get; }

    public ModelMediaInput(string type, string url, string detail = "low")
    {
        if (!string.Equals(type, "image", StringComparison.Ordinal))
            throw new ArgumentException("Only image media is currently supported.", nameof(type));
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        if (url.Length > 4096)
            throw new ArgumentException("Media URL is too long.", nameof(url));
        if (detail is not ("low" or "high" or "auto"))
            throw new ArgumentException("Image detail must be low, high or auto.", nameof(detail));

        Type = type;
        Url = url;
        Detail = detail;
    }
}
