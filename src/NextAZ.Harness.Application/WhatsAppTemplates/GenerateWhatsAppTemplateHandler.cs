using System.Text.Json;
using System.Text.RegularExpressions;
using MediatR;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Application.Executions;
using NextAZ.Harness.Contracts.Executions;
using NextAZ.Harness.Contracts.WhatsAppTemplates;

namespace NextAZ.Harness.Application.WhatsAppTemplates;

public sealed class GenerateWhatsAppTemplateHandler(ICurrentTenant tenant, ISender sender)
    : IRequestHandler<GenerateWhatsAppTemplate, WhatsAppTemplateDraftResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WhatsAppTemplateDraftResponse> Handle(
        GenerateWhatsAppTemplate command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        WhatsAppTemplateDraftResponse Reject(string code) => new(
            Guid.NewGuid(), "Failed", null, new(code, "WhatsApp template draft request rejected."));

        if (tenant.TenantId == Guid.Empty || request.TenantId != tenant.TenantId ||
            !tenant.Permissions.Contains("whatsapp.templates.generate"))
            return Reject("tenant_forbidden");
        if (!ValidRequest(request)) return Reject("invalid_request");

        var content = JsonSerializer.Serialize(new
        {
            objective = request.Objective.Trim(),
            audience = request.Audience.Trim(),
            language = request.Language.Trim(),
            category = request.Category.Trim(),
            tone = string.IsNullOrWhiteSpace(request.Tone) ? "claro e objetivo" : request.Tone.Trim()
        }, JsonOptions);
        var execution = await sender.Send(new ExecuteHarnessRequest(new HarnessRequest(
            tenant.TenantId,
            "api",
            "whatsapp.template.criacao",
            content,
            request.RequestId.ToString("N"))), cancellationToken);

        if (execution.Status != "Completed" || string.IsNullOrWhiteSpace(execution.Content))
            return new(execution.ExecutionId, execution.Status, null, execution.Error,
                execution.ProviderId, execution.ModelId);

        try
        {
            var draft = JsonSerializer.Deserialize<WhatsAppTemplateDraft>(execution.Content, JsonOptions);
            if (draft is null || !ValidDraft(draft) ||
                !string.Equals(draft.Language, request.Language, StringComparison.Ordinal) ||
                !string.Equals(draft.Category, request.Category, StringComparison.Ordinal))
                return new(execution.ExecutionId, "Failed", null,
                    new("model_output_invalid", "The model returned an invalid template draft."),
                    execution.ProviderId, execution.ModelId);
            return new(execution.ExecutionId, execution.Status, draft, null,
                execution.ProviderId, execution.ModelId);
        }
        catch (JsonException)
        {
            return new(execution.ExecutionId, "Failed", null,
                new("model_output_invalid", "The model returned an invalid template draft."),
                execution.ProviderId, execution.ModelId);
        }
    }

    private static bool ValidRequest(GenerateWhatsAppTemplateRequest request) =>
        request.RequestId != Guid.Empty && request.Objective is { Length: > 0 and <= 2000 } &&
        request.Audience is { Length: > 0 and <= 500 } &&
        Regex.IsMatch(request.Language ?? string.Empty, "^[a-z]{2}_[A-Z]{2}$") &&
        request.Category is "MARKETING" or "UTILITY" or "AUTHENTICATION" &&
        (request.Tone is null || request.Tone.Length <= 200);

    private static bool ValidDraft(WhatsAppTemplateDraft draft)
    {
        if (!Regex.IsMatch(draft.Name ?? string.Empty, "^[a-z][a-z0-9_]{2,511}$") ||
            !Regex.IsMatch(draft.Language ?? string.Empty, "^[a-z]{2}_[A-Z]{2}$") ||
            draft.Category is not ("MARKETING" or "UTILITY" or "AUTHENTICATION") ||
            draft.BodyText is not { Length: >= 10 and <= 4096 } ||
            draft.HeaderText?.Length > 60 || draft.FooterText?.Length > 60)
            return false;
        return VariablesValid(draft.HeaderText, draft.HeaderVariableExamples, 1) &&
               VariablesValid(draft.BodyText, draft.BodyVariableExamples, int.MaxValue) &&
               !Regex.IsMatch(draft.FooterText ?? string.Empty, @"\{\{\d+\}\}");
    }

    private static bool VariablesValid(string? text, IReadOnlyList<string>? examples, int maximum)
    {
        if (examples is null) return false;
        var indexes = Regex.Matches(text ?? string.Empty, @"\{\{(\d+)\}\}")
            .Select(match => int.Parse(match.Groups[1].Value)).Distinct().Order().ToArray();
        return indexes.Length <= maximum && indexes.SequenceEqual(Enumerable.Range(1, indexes.Length)) &&
               examples.Count == indexes.Length && examples.All(example => !string.IsNullOrWhiteSpace(example));
    }
}
