namespace NextAZ.Harness.Contracts.WhatsAppTemplates;

public sealed record WhatsAppTemplateDraft(
    string Name,
    string Language,
    string Category,
    string BodyText,
    string? HeaderText,
    string? FooterText,
    IReadOnlyList<string> HeaderVariableExamples,
    IReadOnlyList<string> BodyVariableExamples);
