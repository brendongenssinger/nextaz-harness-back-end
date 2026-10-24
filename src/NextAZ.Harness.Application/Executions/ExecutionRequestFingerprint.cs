using System.Security.Cryptography;
using System.Text;
using NextAZ.Harness.Contracts.Executions;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Conversations;
namespace NextAZ.Harness.Application.Executions;
internal static class ExecutionRequestFingerprint
{
    public static string Compute(HarnessRequest request, AgentDefinition agent,
        TenantAgentConfiguration configuration, IReadOnlyList<ConversationMessage>? messages,
        IReadOnlyDictionary<string, string>? resourceReferences)
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
        Add("attachment-count", request.Attachments?.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (request.Attachments is not null)
            for (var index = 0; index < request.Attachments.Count; index++)
            {
                var attachment = request.Attachments[index];
                Add($"attachment:{index}:media-type", attachment.MediaType);
                Add($"attachment:{index}:file-name", attachment.FileName);
                try
                {
                    Add($"attachment:{index}:sha256", Convert.ToHexString(
                        SHA256.HashData(Convert.FromBase64String(attachment.Base64Data))).ToLowerInvariant());
                }
                catch (FormatException)
                {
                    Add($"attachment:{index}:sha256", "invalid");
                }
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
