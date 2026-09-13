namespace NextAZ.Harness.Contracts.Employees;
public sealed record EmployeeDraftRequest(Guid TenantId, string ProductId, Guid EmployeeId,
    Guid ConversationId, Guid RequestId, Guid? SourceMessageId, string Channel, string Operation,
    IReadOnlyList<ConversationMessage> Messages);
