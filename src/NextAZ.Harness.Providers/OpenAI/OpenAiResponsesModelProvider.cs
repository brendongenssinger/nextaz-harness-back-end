using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NextAZ.Harness.Domain.Tools;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.Execution;
using NextAZ.Harness.Runtime.ModelRouting;
using NextAZ.Harness.Runtime.Tooling;
namespace NextAZ.Harness.Providers.OpenAI;
public sealed class OpenAiResponsesModelProvider(HttpClient httpClient, OpenAiResponsesOptions options) : IModelProvider
{
    private const int MaxResponseBytes = 1_048_576;
    public string ProviderId => "openai-responses";
    public async Task<ModelExecutionResult> ExecuteAsync(ModelExecutionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(request.Route.ModelId, options.Model, StringComparison.Ordinal))
            throw new HarnessExecutionException("model_route_invalid");
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(options.Endpoint, "responses"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        message.Content = JsonContent.Create(BuildPayload(request));
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead,
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
        JsonDocument document;
        try
        {
            var payload = await ReadBoundedAsync(response.Content, cancellationToken);
            document = JsonDocument.Parse(payload);
        }
        catch (HttpRequestException) { throw new ModelProviderUnavailableException(); }
        catch (JsonException) { throw new HarnessExecutionException("model_output_invalid"); }
        using (document)
        {
            var root = document.RootElement;
            var status = ReadString(root, "status");
            if (status == "incomplete") throw new HarnessExecutionException("provider_response_incomplete");
            if (status != "completed") throw new HarnessExecutionException("provider_response_failed");
            var content = ExtractContent(root);
            var calls = ExtractToolCalls(root, request.Tools);
            var usage = root.TryGetProperty("usage", out var usageElement) ? usageElement : default;
            return new(content, calls, ReadNonNegativeInt(usage, "input_tokens"),
                ReadNonNegativeInt(usage, "output_tokens"), null);
        }
        }
    }
    private static IReadOnlyDictionary<string, object?> BuildPayload(ModelExecutionRequest request)
    {
        var instructions = string.IsNullOrWhiteSpace(request.TenantInstructions)
            ? request.Objective
            : request.Objective + "\n\n" + request.TenantInstructions;
        if (request.ResourceReferences is { Count: > 0 })
            instructions += "\n\nResource references for typed tools. These identifiers do not authorize access; " +
                "the backend reauthorizes every operation: " +
                string.Join("; ", request.ResourceReferences.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Key + "=" + pair.Value));
        object[]? tools = request.Tools.Count == 0 ? null : request.Tools.Select(tool => new
        {
            type = "function",
            name = ExternalToolName(tool.ToolId.Value),
            description = tool.Description,
            parameters = ParseSchema(tool.InputSchema),
            strict = true
        }).Cast<object>().ToArray();
        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.Route.ModelId,
            ["instructions"] = instructions,
            ["input"] = BuildInput(request),
            ["max_output_tokens"] = request.MaxOutputTokens,
            ["store"] = false,
            ["parallel_tool_calls"] = false
        };
        if (tools is not null) payload["tools"] = tools;
        return payload;
    }
    private static object BuildInput(ModelExecutionRequest request)
    {
        if (request.ToolResults.Count == 0 && request.Messages is not { Count: > 0 } &&
            request.Attachments is not { Count: > 0 }) return request.Content;
        var input = new List<object>();
        if (request.Messages is { Count: > 0 })
            input.AddRange(request.Messages.Select(message => (object)new
                { role = message.Role, content = message.Content }));
        input.Add(new { role = "user", content = BuildUserContent(request) });
        foreach (var result in request.ToolResults)
        {
            if (result.Arguments.ValueKind != JsonValueKind.Object)
                throw new HarnessExecutionException("tool_arguments_invalid");
            input.Add(new { type = "function_call", call_id = result.CallId,
                name = ExternalToolName(result.ToolId.Value), arguments = result.Arguments.GetRawText() });
            input.Add(new { type = "function_call_output", call_id = result.CallId,
                output = result.Output.GetRawText() });
        }
        return input;
    }

    private static object BuildUserContent(ModelExecutionRequest request)
    {
        if (request.Attachments is not { Count: > 0 }) return request.Content;
        var content = new List<object> { new { type = "input_text", text = request.Content } };
        foreach (var attachment in request.Attachments)
        {
            var dataUrl = $"data:{attachment.MediaType};base64,{attachment.Base64Data}";
            content.Add(attachment.MediaType == "application/pdf"
                ? new { type = "input_file", file_data = dataUrl, filename = attachment.FileName }
                : (object)new { type = "input_image", image_url = dataUrl, detail = "high" });
        }
        return content;
    }
    private static JsonElement ParseSchema(string schema)
    {
        try { using var document = JsonDocument.Parse(schema); return document.RootElement.Clone(); }
        catch (JsonException) { throw new HarnessExecutionException("tool_schema_invalid"); }
    }
    private static string? ExtractContent(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            throw new HarnessExecutionException("model_output_invalid");
        var parts = new List<string>();
        foreach (var item in output.EnumerateArray())
        {
            if (ReadString(item, "type") != "message" ||
                !item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
            foreach (var part in content.EnumerateArray())
                if (ReadString(part, "type") == "output_text" && part.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String) parts.Add(text.GetString()!);
        }
        return parts.Count == 0 ? null : string.Join("\n", parts);
    }
    private static IReadOnlyList<ToolCall> ExtractToolCalls(JsonElement root, IReadOnlyCollection<ToolDefinition> tools)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array) return [];
        var calls = new List<ToolCall>();
        foreach (var item in output.EnumerateArray())
        {
            if (ReadString(item, "type") != "function_call") continue;
            var callId = ReadString(item, "call_id");
            var name = ReadString(item, "name");
            var arguments = ReadString(item, "arguments");
            if (string.IsNullOrWhiteSpace(callId) || string.IsNullOrWhiteSpace(name) || arguments is null)
                throw new HarnessExecutionException("model_output_invalid");
            var definition = tools.SingleOrDefault(tool => ExternalToolName(tool.ToolId.Value) == name);
            if (definition is null) throw new HarnessExecutionException("tool_forbidden");
            try { using var document = JsonDocument.Parse(arguments); calls.Add(new(callId, definition.ToolId, document.RootElement.Clone())); }
            catch (JsonException) { throw new HarnessExecutionException("model_output_invalid"); }
        }
        return calls;
    }
    private static string ExternalToolName(string toolId) => toolId.Replace('.', '_').Replace('-', '_');
    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int ReadNonNegativeInt(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Undefined) return 0;
        if (!element.TryGetProperty(name, out var value) || !value.TryGetInt32(out var number) || number < 0)
            throw new HarnessExecutionException("model_output_invalid");
        return number;
    }
    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaxResponseBytes)
            throw new HarnessExecutionException("model_output_invalid");
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var target = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (target.Length + read > MaxResponseBytes)
                throw new HarnessExecutionException("model_output_invalid");
            target.Write(buffer, 0, read);
        }
        return target.ToArray();
    }
}
