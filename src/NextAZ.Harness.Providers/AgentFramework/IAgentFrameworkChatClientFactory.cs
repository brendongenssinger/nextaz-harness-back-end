using Microsoft.Extensions.AI;
using NextAZ.Harness.Runtime.ModelRouting;

namespace NextAZ.Harness.Providers.AgentFramework;

public interface IAgentFrameworkChatClientFactory
{
    IChatClient Create(ModelExecutionRequest request);
}
