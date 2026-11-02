using NextAZ.Harness.Contracts.Executions;

namespace NextAZ.Harness.Contracts.WhatsAppTemplates;

public sealed record WhatsAppTemplateDraftResponse(
    Guid ExecutionId,
    string Status,
    WhatsAppTemplateDraft? Draft,
    HarnessError? Error,
    string? ProviderId = null,
    string? ModelId = null);
