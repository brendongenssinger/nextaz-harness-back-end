using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.Execution;
using NextAZ.Harness.Runtime.ModelRouting;

namespace NextAZ.Harness.Providers.AgentFramework;

public sealed class ModelProviderAgentFrameworkChatClientFactory(IModelProvider innerProvider)
    : IAgentFrameworkChatClientFactory
{
    public IChatClient Create(ModelExecutionRequest request) => new ModelProviderChatClient(innerProvider, request);

    private sealed class ModelProviderChatClient(IModelProvider innerProvider, ModelExecutionRequest originalRequest)
        : IChatClient
    {
        private bool _called;

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_called) throw new HarnessExecutionException("model_output_invalid");
            _called = true;

            var materialized = messages.ToArray();
            var currentUser = materialized.LastOrDefault(message => message.Role == ChatRole.User)?.Text;
            if (!string.Equals(currentUser, originalRequest.Content, StringComparison.Ordinal))
                throw new HarnessExecutionException("model_input_invalid");

            ValidateAdvertisedTools(options?.Tools, originalRequest);
            var route = originalRequest.Route with { ProviderId = innerProvider.ProviderId };
            var delegated = originalRequest with { Route = route };
            var result = await innerProvider.ExecuteAsync(delegated, cancellationToken);
            if (result.ToolCalls is null || result.InputTokens < 0 || result.OutputTokens < 0)
                throw new HarnessExecutionException("model_output_invalid");

            var contents = new List<AIContent>();
            if (result.Content is not null) contents.Add(new TextContent(result.Content));
            foreach (var call in result.ToolCalls)
            {
                var definition = originalRequest.Tools.SingleOrDefault(tool => tool.ToolId == call.ToolId);
                if (definition is null) throw new HarnessExecutionException("tool_forbidden");
                Dictionary<string, object?>? arguments;
                try
                {
                    arguments = call.Arguments.Deserialize<Dictionary<string, object?>>();
                }
                catch (System.Text.Json.JsonException)
                {
                    throw new HarnessExecutionException("model_output_invalid");
                }
                if (arguments is null) throw new HarnessExecutionException("model_output_invalid");
                contents.Add(new FunctionCallContent(call.CallId,
                    MicrosoftAgentFrameworkModelProvider.ExternalToolName(definition.ToolId.Value), arguments));
            }

            return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents))
            {
                ModelId = originalRequest.Route.ModelId,
                Usage = new UsageDetails
                {
                    InputTokenCount = result.InputTokens,
                    OutputTokenCount = result.OutputTokens,
                    TotalTokenCount = (long)result.InputTokens + result.OutputTokens
                },
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    [MicrosoftAgentFrameworkModelProvider.EstimatedCostKey] = result.EstimatedCost
                }
            };
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var update in response.ToChatResponseUpdates()) yield return update;
        }

        public void Dispose() { }

        private static void ValidateAdvertisedTools(IList<AITool>? advertised,
            ModelExecutionRequest request)
        {
            var expected = request.Tools.Select(tool =>
                MicrosoftAgentFrameworkModelProvider.ExternalToolName(tool.ToolId.Value))
                .ToHashSet(StringComparer.Ordinal);
            var actual = (advertised ?? []).Select(tool => tool.Name).ToArray();
            if (actual.Length != expected.Count || actual.Any(name => !expected.Contains(name)))
                throw new HarnessExecutionException("tool_forbidden");
        }
    }
}
