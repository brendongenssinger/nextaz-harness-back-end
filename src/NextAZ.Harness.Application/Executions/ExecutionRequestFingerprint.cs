using System.Security.Cryptography;
using System.Text;
using NextAZ.Harness.Contracts.Executions;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Conversations;
using NextAZ.Harness.Runtime.ModelRouting;
namespace NextAZ.Harness.Application.Executions;
internal static class ExecutionRequestFingerprint
{
    public static string Compute(HarnessRequest request, AgentDefinition agent,
        TenantAgentConfiguration configuration, IReadOnlyList<ConversationMessage>? messages,
        IReadOnlyDictionary<string, string>? resourceReferences,
        TrustedExecutionOrigin? trustedOrigin = null,
        IReadOnlyList<ModelMediaInput>? mediaInputs = null)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string name, string? value)
        {
            var bytes = Encoding.UTF8.GetBytes(name + "\0" + (value ?? "<null>") + "\0");
            hash.AppendData(bytes);
        }
        Add("format", "harness-request-v1");
        Add("tenant", request.TenantId.ToString("D"));
        Add("channel", request.Channel);
        Add("operation", request.Operation);
        Add("content", request.Content);
        Add("agent-id", agent.AgentId.Value);
        Add("agent-version", agent.Version.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add("configuration-version", configuration.ConfigurationVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add("knowledge-base", configuration.KnowledgeBaseId?.ToString("D"));
        Add("model-policy", configuration.ModelPolicy.Name);
        foreach (var pair in (request.Metadata ?? new Dictionary<string, string>())
            .OrderBy(x => x.Key, StringComparer.Ordinal))
            Add("metadata:" + pair.Key, pair.Value);
        foreach (var pair in (resourceReferences ?? new Dictionary<string, string>())
            .OrderBy(x => x.Key, StringComparer.Ordinal))
            Add("resource:" + pair.Key, pair.Value);
        if (mediaInputs is not null)
            for (var index = 0; index < mediaInputs.Count; index++)
            {
                Add($"media:{index}:type", mediaInputs[index].Type);
                Add($"media:{index}:url", mediaInputs[index].Url);
                Add($"media:{index}:detail", mediaInputs[index].Detail);
            }
        if (trustedOrigin is not null)
        {
            Add("origin:source", trustedOrigin.Source);
            Add("origin:actor-type", trustedOrigin.ActorType);
            Add("origin:actor-id", trustedOrigin.ActorId);
            Add("origin:workflow-id", trustedOrigin.WorkflowId.ToString("D"));
            Add("origin:workflow-version", trustedOrigin.WorkflowVersion.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            Add("origin:run-id", trustedOrigin.RunId.ToString("D"));
            Add("origin:step-id", trustedOrigin.StepId);
            Add("origin:conversation-id", trustedOrigin.ConversationId.ToString("D"));
            Add("origin:solicitacao-id", trustedOrigin.SolicitacaoId?.ToString("D"));
        }
        if (messages is not null)
            for (var index = 0; index < messages.Count; index++)
            {
                Add($"message:{index}:role", messages[index].Role);
                Add($"message:{index}:content", messages[index].Content);
            }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
