namespace NextAZ.Harness.Providers.OpenAI;
public sealed record OpenAiResponsesOptions(Uri Endpoint, string ApiKey, string Model)
{
    public static OpenAiResponsesOptions Create(string? endpoint, string? apiKey, string? model)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Configure an HTTPS OpenAI Responses endpoint.");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Configure the OpenAI API key outside source control.");
        if (string.IsNullOrWhiteSpace(model) || model.Length > 200)
            throw new InvalidOperationException("Configure an explicit OpenAI model.");
        return new(uri, apiKey, model);
    }
}
