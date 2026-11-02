using MediatR;
using NextAZ.Harness.Contracts.WhatsAppTemplates;

namespace NextAZ.Harness.Application.WhatsAppTemplates;

public sealed record GenerateWhatsAppTemplate(GenerateWhatsAppTemplateRequest Request)
    : IRequest<WhatsAppTemplateDraftResponse>;
