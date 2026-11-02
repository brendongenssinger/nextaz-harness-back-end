using MediatR;
using NextAZ.Harness.Contracts.Receipts;

namespace NextAZ.Harness.Application.Receipts;

public sealed record ClassifyReceipt(ClassifyReceiptRequest Request)
    : IRequest<ReceiptClassificationResponse>;
