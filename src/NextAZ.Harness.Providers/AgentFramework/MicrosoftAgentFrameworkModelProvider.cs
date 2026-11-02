using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.Execution;
using NextAZ.Harness.Runtime.ModelRouting;
using NextAZ.Harness.Runtime.Tooling;

namespace NextAZ.Harness.Providers.AgentFramework;

public sealed class MicrosoftAgentFrameworkModelProvider(IAgentFrameworkChatClientFactory chatClients,
    AgentFrameworkTenantScope tenantScope)
    : IModelProvider
{
    internal const string EstimatedCostKey = "nextaz.estimated_cost";
    public string ProviderId => "microsoft-agent-framework";

    public async Task<ModelExecutionResult> ExecuteAsync(ModelExecutionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!tenantScope.Allows(request.Context.TenantId, request.Context.AgentId.Value) ||
            request.Context.TenantId != request.Memory.TenantId || request.MaxOutputTokens <= 0)
            throw new HarnessExecutionException("model_route_invalid");

        var tools = BuildToolDeclarations(request);
        using var chatClient = chatClients.Create(request);
        var agent = chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Id = request.Context.AgentId.Value + ".v" + request.Context.AgentVersion.Value,
            Name = request.Context.AgentId.Value.Replace('.', '_').Replace('-', '_'),
            Description = "NextAZ tenant-scoped governed agent.",
            UseProvidedChatClientAsIs = true,
            AllowConcurrentInvocation = false,
            ChatOptions = new ChatOptions
            {
                Instructions = BuildInstructions(request),
                MaxOutputTokens = request.MaxOutputTokens,
                ModelId = request.Route.ModelId,
                AllowMultipleToolCalls = false,
                Tools = tools
            }
        });

        var response = await agent.RunAsync(BuildMessages(request), session: null,
            options: null, cancellationToken);
        var toolCalls = ExtractToolCalls(response, request);
        var content = string.IsNullOrWhiteSpace(response.Text) ? null : response.Text;
        var inputTokens = ReadTokenCount(response.Usage?.InputTokenCount);
        var outputTokens = ReadTokenCount(response.Usage?.OutputTokenCount);
        decimal? estimatedCost = null;
        if (response.AdditionalProperties?.TryGetValue(EstimatedCostKey, out var cost) == true)
            estimatedCost = cost switch
            {
                decimal value => value,
                null => null,
                _ => throw new HarnessExecutionException("model_output_invalid")
            };
        return new(content, toolCalls, inputTokens, outputTokens, estimatedCost);
    }

    internal static string ExternalToolName(string toolId) => toolId.Replace('.', '_').Replace('-', '_');

    private static IList<AITool> BuildToolDeclarations(ModelExecutionRequest request)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<AITool>(request.Tools.Count);
        foreach (var tool in request.Tools)
        {
            var name = ExternalToolName(tool.ToolId.Value);
            if (!names.Add(name)) throw new HarnessExecutionException("tool_schema_invalid");
            try
            {
                using var schema = JsonDocument.Parse(tool.InputSchema);
                if (schema.RootElement.ValueKind != JsonValueKind.Object)
                    throw new HarnessExecutionException("tool_schema_invalid");
                result.Add(AIFunctionFactory.CreateDeclaration(name, tool.Description,
                    schema.RootElement.Clone(), returnJsonSchema: null));
            }
            catch (JsonException)
            {
                throw new HarnessExecutionException("tool_schema_invalid");
            }
        }
        return result;
    }

    private static string BuildInstructions(ModelExecutionRequest request) =>
        string.IsNullOrWhiteSpace(request.TenantInstructions)
            ? request.Objective
            : request.Objective + "\n\n" + request.TenantInstructions;

    private static IReadOnlyList<ChatMessage> BuildMessages(ModelExecutionRequest request)
    {
        var messages = new List<ChatMessage>();
        if (request.Messages is { Count: > 0 })
        {
            foreach (var message in request.Messages)
            {
                var role = message.Role switch
                {
                    "assistant" => ChatRole.Assistant,
                    "user" => ChatRole.User,
                    _ => throw new HarnessExecutionException("model_input_invalid")
                };
                messages.Add(new ChatMessage(role, message.Content));
            }
        }
        messages.Add(new ChatMessage(ChatRole.User, request.Content));
        foreach (var result in request.ToolResults)
        {
            if (result.Arguments.ValueKind != JsonValueKind.Object)
                throw new HarnessExecutionException("tool_arguments_invalid");
            Dictionary<string, object?>? arguments;
            try
            {
                arguments = result.Arguments.Deserialize<Dictionary<string, object?>>();
            }
            catch (JsonException)
            {
                throw new HarnessExecutionException("tool_arguments_invalid");
            }
            if (arguments is null) throw new HarnessExecutionException("tool_arguments_invalid");
            messages.Add(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent(result.CallId, ExternalToolName(result.ToolId.Value), arguments)]));
            messages.Add(new ChatMessage(ChatRole.Tool,
                [new FunctionResultContent(result.CallId, result.Output.Clone())]));
        }
        return messages;
    }

    private static IReadOnlyList<ToolCall> ExtractToolCalls(AgentResponse response,
        ModelExecutionRequest request)
    {
        var byExternalName = request.Tools.ToDictionary(
            tool => ExternalToolName(tool.ToolId.Value), StringComparer.Ordinal);
        var calls = new List<ToolCall>();
        if (response.Messages is null) throw new HarnessExecutionException("model_output_invalid");
        foreach (var call in response.Messages.SelectMany(message => message.Contents ?? [])
                     .OfType<FunctionCallContent>())
        {
            if (!byExternalName.TryGetValue(call.Name, out var definition))
                throw new HarnessExecutionException("tool_forbidden");
            if (string.IsNullOrWhiteSpace(call.CallId) || call.Arguments is null)
                throw new HarnessExecutionException("model_output_invalid");
            JsonElement arguments;
            try
            {
                arguments = JsonSerializer.SerializeToElement(call.Arguments);
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                throw new HarnessExecutionException("model_output_invalid");
            }
            if (arguments.ValueKind != JsonValueKind.Object)
                throw new HarnessExecutionException("model_output_invalid");
            calls.Add(new ToolCall(call.CallId, definition.ToolId, arguments));
        }
        return calls;
    }

    private static int ReadTokenCount(long? value) => value switch
    {
        null => 0,
        < 0 or > int.MaxValue => throw new HarnessExecutionException("model_output_invalid"),
        _ => (int)value.Value
    };
}
