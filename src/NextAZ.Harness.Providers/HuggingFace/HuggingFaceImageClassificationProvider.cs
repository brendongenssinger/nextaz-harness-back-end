using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.Execution;
using NextAZ.Harness.Runtime.ModelRouting;

namespace NextAZ.Harness.Providers.HuggingFace;

public sealed class HuggingFaceImageClassificationProvider(
    HttpClient inferenceClient,
    HttpClient mediaClient,
    HuggingFaceImageClassificationOptions options) : IModelProvider
{
    private const int MaxResponseBytes = 256 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    public string ProviderId => "huggingface-image-classification";

    public async Task<ModelExecutionResult> ExecuteAsync(
        ModelExecutionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(request.Route.ModelId, options.Model, StringComparison.Ordinal))
            throw new HarnessExecutionException("model_route_invalid");

        var input = ReadInput(request.Content);
        var mediaUri = ValidateMediaUri(input.MediaUrl);
        var media = await DownloadMediaAsync(mediaUri, cancellationToken);
        var classifications = await ClassifyAsync(media, cancellationToken);
        var best = classifications.OrderByDescending(item => item.Score).FirstOrDefault();
        if (best is null)
            throw new HarnessExecutionException("model_output_invalid");

        var isReceipt = best.Score >= options.MinimumScore && options.ReceiptLabels.Contains(best.Label);
        var output = JsonSerializer.Serialize(new
        {
            isReceipt,
            label = best.Label,
            score = best.Score,
            threshold = options.MinimumScore,
            requiresDetailedExtraction = isReceipt
        }, JsonOptions);
        return new(output, [], 0, 0, options.EstimatedCostPerRequest);
    }

    private static ReceiptMediaInput ReadInput(string content)
    {
        try
        {
            var input = JsonSerializer.Deserialize<ReceiptMediaInput>(content, JsonOptions);
            if (input is null || string.IsNullOrWhiteSpace(input.MediaUrl) || input.MediaUrl.Length > 4096)
                throw new HarnessExecutionException("invalid_request");
            return input;
        }
        catch (JsonException)
        {
            throw new HarnessExecutionException("invalid_request");
        }
    }

    private Uri ValidateMediaUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) || !options.AllowedMediaHosts.Contains(uri.Host))
            throw new HarnessExecutionException("media_source_forbidden");
        return uri;
    }

    private async Task<ReceiptMedia> DownloadMediaAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        HttpResponseMessage response;
        try
        {
            response = await mediaClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new ModelProviderUnavailableException();
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
                (int)response.StatusCode >= 500)
                throw new ModelProviderUnavailableException();
            if (!response.IsSuccessStatusCode)
                throw new HarnessExecutionException("media_unavailable");

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is null || !AllowedContentTypes.Contains(contentType))
                throw new HarnessExecutionException("media_type_unsupported");
            var bytes = await ReadBoundedAsync(response.Content, options.MaximumMediaBytes,
                "media_too_large", cancellationToken);
            return new(bytes, contentType);
        }
    }

    private async Task<IReadOnlyList<Classification>> ClassifyAsync(
        ReceiptMedia media,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = new ByteArrayContent(media.Bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(media.ContentType);

        HttpResponseMessage response;
        try
        {
            response = await inferenceClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new ModelProviderUnavailableException();
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
                (int)response.StatusCode >= 500)
                throw new ModelProviderUnavailableException();
            if (!response.IsSuccessStatusCode)
                throw new HarnessExecutionException("provider_request_rejected");

            try
            {
                var bytes = await ReadBoundedAsync(response.Content, MaxResponseBytes,
                    "model_output_invalid", cancellationToken);
                var items = JsonSerializer.Deserialize<Classification[]>(bytes, JsonOptions);
                if (items is null || items.Length == 0 || items.Length > 100 ||
                    items.Any(item => string.IsNullOrWhiteSpace(item.Label) || item.Label.Length > 100 ||
                        item.Score is < 0 or > 1))
                    throw new HarnessExecutionException("model_output_invalid");
                return items;
            }
            catch (JsonException)
            {
                throw new HarnessExecutionException("model_output_invalid");
            }
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int maximumBytes,
        string errorCode, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > 0 && content.Headers.ContentLength > maximumBytes)
            throw new HarnessExecutionException(errorCode);
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var target = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (target.Length + read > maximumBytes)
                throw new HarnessExecutionException(errorCode);
            target.Write(buffer, 0, read);
        }
        return target.ToArray();
    }

    private sealed record ReceiptMediaInput(string MediaUrl);
    private sealed record ReceiptMedia(byte[] Bytes, string ContentType);
    private sealed record Classification(string Label, decimal Score);
}
