using MediatR;
using NextAZ.Harness.Contracts.Imobiliaria;

namespace NextAZ.Harness.Application.Imobiliaria;

public sealed record InterpretarBuscaAssistida(BuscaAssistidaRequest Request)
    : IRequest<BuscaAssistidaExecutionResult>;

public sealed record BuscaAssistidaExecutionResult(BuscaAssistidaResponse? Response, string? ErrorCode)
{
    public static BuscaAssistidaExecutionResult Completed(BuscaAssistidaResponse response) => new(response, null);
    public static BuscaAssistidaExecutionResult Failed(string errorCode) => new(null, errorCode);
}
