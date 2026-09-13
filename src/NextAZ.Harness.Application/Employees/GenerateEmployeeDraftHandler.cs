using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Application.Executions;
using NextAZ.Harness.Contracts.Employees;
using NextAZ.Harness.Contracts.Executions;
namespace NextAZ.Harness.Application.Employees;
public sealed class GenerateEmployeeDraftHandler(ICurrentTenant tenant,
    IEmployeeDraftProfileRepository profiles, ISender sender) : IRequestHandler<GenerateEmployeeDraft, HarnessResponse>
{
    public async Task<HarnessResponse> Handle(GenerateEmployeeDraft command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        HarnessResponse Reject(string code) => new(Guid.NewGuid(), request.RequestId.ToString("N"),
            null, null, "Failed", null, new(code, "Draft request rejected."));
        if (tenant.TenantId == Guid.Empty || request.TenantId != tenant.TenantId ||
            !tenant.Permissions.Contains("harness.employee-drafts"))
            return Reject("tenant_forbidden");
        if (!Valid(request)) return Reject("invalid_request");
        var profile = await profiles.GetAsync(tenant.TenantId, request.ProductId, request.EmployeeId, cancellationToken);
        if (profile is null) return Reject("employee_profile_not_found");
        if (profile.TenantId != tenant.TenantId || profile.EmployeeId != request.EmployeeId ||
            profile.ProductId != request.ProductId || profile.Configuration.TenantId != tenant.TenantId)
            return Reject("tenant_forbidden");
        if (!profile.Agent.Enabled || !profile.Configuration.Enabled) return Reject("agent_disabled");
        // Include source, employee and payload in the correlation scope. This local pilot performs no writes/sends.
        var correlation = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request)))).ToLowerInvariant();
        var messages = request.Messages.Select(m =>
            new Domain.Conversations.ConversationMessage(m.Role, m.Content)).ToArray();
        var instruction = request.Operation == "draft.opening"
            ? "Prepare a short opening message for human review. Do not execute actions."
            : "Prepare a reply to the conversation for human review. Do not execute actions.";
        var input = new HarnessRequest(tenant.TenantId, request.Channel, request.Operation, instruction, correlation);
        var resolved = new ResolvedExecutionProfile(profile.Agent, profile.Configuration, messages,
            DisableTools: true, SimulationOnly: true);
        return await sender.Send(new ExecuteHarnessRequest(input, resolved), cancellationToken);
    }
    private static bool Valid(EmployeeDraftRequest r) =>
        r.ProductId == "funcionario-digital" && r.EmployeeId != Guid.Empty && r.ConversationId != Guid.Empty &&
        r.RequestId != Guid.Empty && r.SourceMessageId != Guid.Empty &&
        r.Channel is "api" or "whatsapp" or "instagram" &&
        r.Operation is "draft.reply" or "draft.opening" &&
        r.Messages is { Count: > 0 and <= 100 } &&
        r.Messages.All(m => m is not null && m.Role is "user" or "assistant" &&
            !string.IsNullOrWhiteSpace(m.Content) && m.Content.Length <= 16000) &&
        r.Messages.Sum(m => (long)m.Content.Length) <= 32000;
}
