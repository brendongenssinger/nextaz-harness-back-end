namespace NextAZ.Harness.Contracts.Cartorio;

public sealed record CartorioWorkflowAgentCatalogResponse(
    IReadOnlyList<CartorioWorkflowAgentCatalogItem> Items);

public sealed record CartorioWorkflowAgentCatalogItem(string Id, string Name,
    IReadOnlyList<string> Channels, IReadOnlyList<CartorioWorkflowAgentVersion> Versions);

public sealed record CartorioWorkflowAgentVersion(int Version, string Status);

public sealed record CartorioWorkflowAgentExecutionRequest(string Content, string CorrelationId,
    string Channel, Guid WorkflowId, int WorkflowVersion, Guid RunId, string StepId,
    Guid ConversationId, Guid? SolicitacaoId, CartorioWorkflowAgentOriginator Originator);

public sealed record CartorioWorkflowAgentOriginator(string Kind, string? ActorId = null);
