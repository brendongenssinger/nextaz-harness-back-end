using System.Text.Json;
using MediatR;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Application.Executions;
using NextAZ.Harness.Contracts.Executions;
using NextAZ.Harness.Contracts.Receipts;
using NextAZ.Harness.Runtime.ModelRouting;

namespace NextAZ.Harness.Application.Receipts;

public sealed class ClassifyReceiptHandler(ICurrentTenant tenant, ISender sender)
    : IRequestHandler<ClassifyReceipt, ReceiptClassificationResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ReceiptClassificationResponse> Handle(
        ClassifyReceipt command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        ReceiptClassificationResponse Reject(string code) => new(
            Guid.NewGuid(), "Failed", null, new(code, "Receipt classification request rejected."));

        if (tenant.TenantId == Guid.Empty || request.TenantId != tenant.TenantId ||
            !tenant.Permissions.Contains("receipts.classify"))
            return Reject("tenant_forbidden");
        if (request.RequestId == Guid.Empty || request.MediaUrl is not { Length: > 0 and <= 4096 })
            return Reject("invalid_request");

        var content = JsonSerializer.Serialize(new { mediaUrl = request.MediaUrl.Trim() }, JsonOptions);
        var execution = await sender.Send(new ExecuteHarnessRequest(new HarnessRequest(
            tenant.TenantId,
            "api",
            "funcionario-digital.recibo",
            content,
            request.RequestId.ToString("N")),
            MediaInputs: [new ModelMediaInput("image", request.MediaUrl.Trim(), "low")]), cancellationToken);

        if (execution.Status != "Completed" || string.IsNullOrWhiteSpace(execution.Content))
            return new(execution.ExecutionId, execution.Status, null, execution.Error,
                execution.ProviderId, execution.ModelId, execution.EstimatedCost);

        try
        {
            var classification = JsonSerializer.Deserialize<ReceiptClassification>(execution.Content, JsonOptions);
            if (!ValidClassification(classification))
                return InvalidOutput(execution);
            return new(execution.ExecutionId, execution.Status, classification, null,
                execution.ProviderId, execution.ModelId, execution.EstimatedCost);
        }
        catch (JsonException)
        {
            return InvalidOutput(execution);
        }
    }

    private static bool ValidClassification(ReceiptClassification? classification) =>
        classification is not null && classification.Label is { Length: > 0 and <= 100 } &&
        classification.Score is >= 0 and <= 1 && classification.Threshold is > 0 and <= 1 &&
        classification.RequiresDetailedExtraction == classification.IsReceipt &&
        (!string.Equals(classification.Label, "recibo", StringComparison.OrdinalIgnoreCase) ||
            classification.IsReceipt) &&
        (!string.Equals(classification.Label, "nao_recibo", StringComparison.OrdinalIgnoreCase) ||
            !classification.IsReceipt);

    private static ReceiptClassificationResponse InvalidOutput(HarnessResponse execution) => new(
        execution.ExecutionId, "Failed", null,
        new("model_output_invalid", "The model returned an invalid receipt classification."),
        execution.ProviderId, execution.ModelId, execution.EstimatedCost);
}
