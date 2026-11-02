namespace NextAZ.Harness.Application.Executions;

public sealed record TrustedExecutionOrigin(string Source, string ActorType, string ActorId,
    Guid WorkflowId, int WorkflowVersion, Guid RunId, string StepId, Guid ConversationId,
    Guid? SolicitacaoId);
