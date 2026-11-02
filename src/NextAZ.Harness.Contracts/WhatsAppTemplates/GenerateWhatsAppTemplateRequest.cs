namespace NextAZ.Harness.Contracts.WhatsAppTemplates;

public sealed record GenerateWhatsAppTemplateRequest(
    Guid TenantId,
    Guid RequestId,
    string Objective,
    string Audience,
    string Language,
    string Category,
    string? Tone = null);
