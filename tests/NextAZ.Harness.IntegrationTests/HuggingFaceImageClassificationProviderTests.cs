using System.Collections.Frozen;
using System.Net;
using System.Text;
using System.Text.Json;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Providers.HuggingFace;
using NextAZ.Harness.Runtime.Execution;
using NextAZ.Harness.Runtime.Memory;
using NextAZ.Harness.Runtime.ModelRouting;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;

namespace NextAZ.Harness.IntegrationTests;

public sealed class HuggingFaceImageClassificationProviderTests
{
    [Fact]
    public async Task ClassifiesAllowedMediaWithPrivateModelAndConfiguredThreshold()
    {
        var media = new CapturingHandler(_ => new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3])
            {
                Headers = { ContentType = new("image/jpeg") }
            }
        });
        var inference = new CapturingHandler(_ => Json(HttpStatusCode.OK,
            "[{\"label\":\"recibo\",\"score\":0.97},{\"label\":\"outro\",\"score\":0.03}]"));

        var result = await Provider(inference, media).ExecuteAsync(Request(), default);

        using var output = JsonDocument.Parse(result.Content!);
        Assert.True(output.RootElement.GetProperty("isReceipt").GetBoolean());
        Assert.True(output.RootElement.GetProperty("requiresDetailedExtraction").GetBoolean());
        Assert.Equal("recibo", output.RootElement.GetProperty("label").GetString());
        Assert.Equal(0.02m, result.EstimatedCost);
        Assert.Equal("Bearer", inference.AuthorizationScheme);
        Assert.Equal("hf-test-secret", inference.AuthorizationParameter);
        Assert.Equal("https://nextazstorage.blob.core.windows.net/midias/receipt.jpg?sig=redacted",
            media.RequestUri?.ToString());
        Assert.Equal("image/jpeg", inference.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, inference.Body);
    }

    [Fact]
    public async Task KeepsLowConfidenceClassificationOutOfDetailedExtraction()
    {
        var media = ImageHandler();
        var inference = new CapturingHandler(_ => Json(HttpStatusCode.OK,
            "[{\"label\":\"recibo\",\"score\":0.89}]"));

        var result = await Provider(inference, media).ExecuteAsync(Request(), default);

        using var output = JsonDocument.Parse(result.Content!);
        Assert.False(output.RootElement.GetProperty("isReceipt").GetBoolean());
        Assert.False(output.RootElement.GetProperty("requiresDetailedExtraction").GetBoolean());
    }

    [Fact]
    public async Task RejectsMediaOutsideExplicitHostAllowlistBeforeNetworkCall()
    {
        var media = ImageHandler();
        var provider = Provider(new CapturingHandler(_ => Json(HttpStatusCode.OK, "[]")), media);

        var error = await Assert.ThrowsAsync<HarnessExecutionException>(() => provider.ExecuteAsync(
            Request("https://attacker.invalid/receipt.jpg"), default));

        Assert.Equal("media_source_forbidden", error.Code);
        Assert.Null(media.RequestUri);
    }

    [Fact]
    public void RequiresExactReceiptLabelsFromPrivateModel()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Options(receiptLabels: []));
        Assert.Contains("receipt label", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HuggingFaceImageClassificationProvider Provider(
        HttpMessageHandler inference,
        HttpMessageHandler media) => new(new HttpClient(inference), new HttpClient(media), Options());

    private static HuggingFaceImageClassificationOptions Options(string[]? receiptLabels = null) =>
        HuggingFaceImageClassificationOptions.Create(
            "https://router.huggingface.co/hf-inference/models/brendonmascarenhas/train-identify-recibos",
            "hf-test-secret",
            "brendonmascarenhas/train-identify-recibos",
            receiptLabels ?? ["recibo"],
            0.90m,
            ["nextazstorage.blob.core.windows.net"],
            1024 * 1024,
            0.02m);

    private static ModelExecutionRequest Request(string? mediaUrl = null)
    {
        var tenant = Guid.NewGuid();
        var context = new HarnessContext(tenant, new AgentId("funcionario-digital.recibo"), new AgentVersion(1),
            ExecutionId.New(), "corr", "api", "funcionario-digital.recibo", DateTimeOffset.UtcNow);
        var content = JsonSerializer.Serialize(new
        {
            mediaUrl = mediaUrl ??
                "https://nextazstorage.blob.core.windows.net/midias/receipt.jpg?sig=redacted"
        });
        return new(context, new("huggingface-image-classification",
                "brendonmascarenhas/train-identify-recibos", TimeSpan.FromSeconds(5)),
            "classify", string.Empty, content, new MemoryContext(tenant, null, []), [], [], 64);
    }

    private static CapturingHandler ImageHandler() => new(_ => new(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent([1])
        {
            Headers = { ContentType = new("image/png") }
        }
    });

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> response)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public byte[]? Body { get; private set; }
        public string? ContentType { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            Body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return response(request);
        }
    }
}
