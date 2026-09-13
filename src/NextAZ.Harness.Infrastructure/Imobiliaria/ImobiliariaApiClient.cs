using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NextAZ.Harness.Runtime.Execution;
namespace NextAZ.Harness.Infrastructure.Imobiliaria;
public sealed class ImobiliariaApiClient(HttpClient httpClient, IImobiliariaAccessTokenProvider tokens,
    ImobiliariaIntegrationOptions options)
{
    private const int MaxToolResponseBytes = 32_768;
    public async Task<JsonElement> GetAsync(Guid tenantId, string relativePath, CancellationToken cancellationToken) =>
        await SendAsync(tenantId, HttpMethod.Get, relativePath, null, cancellationToken);
    public async Task<JsonElement> PostAsync(Guid tenantId, string relativePath, object body,
        CancellationToken cancellationToken) =>
        await SendAsync(tenantId, HttpMethod.Post, relativePath, JsonContent.Create(body), cancellationToken);
    private async Task<JsonElement> SendAsync(Guid tenantId, HttpMethod method, string relativePath,
        HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(options.ApiBaseUrl, relativePath)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            await tokens.GetAsync(tenantId, cancellationToken));
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            throw new HarnessExecutionException("imobiliaria_backend_unavailable");
        if (!response.IsSuccessStatusCode)
            throw new HarnessExecutionException("imobiliaria_tool_rejected");
        if (response.Content.Headers.ContentLength is > MaxToolResponseBytes)
            throw new HarnessExecutionException("tool_output_too_large");
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var bounded = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (bounded.Length + read > MaxToolResponseBytes)
                throw new HarnessExecutionException("tool_output_too_large");
            await bounded.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        bounded.Position = 0;
        try { using var document = await JsonDocument.ParseAsync(bounded, cancellationToken: cancellationToken);
            return document.RootElement.Clone(); }
        catch (JsonException) { throw new HarnessExecutionException("imobiliaria_tool_output_invalid"); }
    }
}
