using System.Text.Json;
using MediatR;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Contracts.Imobiliaria;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.Execution;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;

namespace NextAZ.Harness.Application.Imobiliaria;

public sealed class InterpretarBuscaAssistidaHandler(ICurrentTenant currentTenant,
    IAgentRegistry agents, ITenantAgentConfigurationRepository configurations,
    IMemoryProvider memory, IModelRouter router, IAgentRuntime runtime, TimeProvider timeProvider)
    : IRequestHandler<InterpretarBuscaAssistida, BuscaAssistidaExecutionResult>
{
    private static readonly AgentId AgentId = new("imobiliaria.vitrine.busca-assistida");
    private const string Operation = "imobiliaria.vitrine.busca-assistida";

    public async Task<BuscaAssistidaExecutionResult> Handle(InterpretarBuscaAssistida command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (currentTenant.TenantId == Guid.Empty || currentTenant.TenantId != request.TenantId)
            return BuscaAssistidaExecutionResult.Failed("tenant_forbidden");
        if (!currentTenant.Permissions.Contains("harness.execute"))
            return BuscaAssistidaExecutionResult.Failed("execution_forbidden");
        if (!IsValid(request)) return BuscaAssistidaExecutionResult.Failed("invalid_request");

        var agent = await agents.GetAsync(AgentId, cancellationToken);
        var configuration = agent is null ? null :
            await configurations.GetAsync(currentTenant.TenantId, AgentId, cancellationToken);
        if (agent is null || configuration is null)
            return BuscaAssistidaExecutionResult.Failed("busca_assistida_unavailable");

        var executionId = ExecutionId.New();
        var context = new HarnessContext(currentTenant.TenantId, AgentId, agent.Version, executionId,
            request.CorrelationId.ToString("D"), "web", Operation, timeProvider.GetUtcNow());
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(configuration.Limits.Timeout);
            var memoryContext = await memory.LoadAsync(context, configuration, deadline.Token);
            var routes = (await router.ResolveAsync(currentTenant.TenantId, agent.AgentId, agent.Version,
                configuration.ModelPolicy, deadline.Token)).Select(route => route with
                {
                    Timeout = route.Timeout <= configuration.Limits.Timeout
                        ? route.Timeout
                        : configuration.Limits.Timeout
                }).ToArray();
            var result = await runtime.ExecuteAsync(new(context, agent, configuration,
                currentTenant.Permissions, BuildContent(request), memoryContext, routes, [], _ => { }),
                deadline.Token).WaitAsync(deadline.Token);
            if (!BuscaAssistidaOutputParser.TryParse(result.Content, out var filters, out var unknownFields))
                return BuscaAssistidaExecutionResult.Failed("model_output_invalid");
            return BuscaAssistidaExecutionResult.Completed(new(executionId.Value, request.CorrelationId,
                filters, unknownFields));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return BuscaAssistidaExecutionResult.Failed("execution_timed_out");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HarnessExecutionException exception)
        {
            return BuscaAssistidaExecutionResult.Failed(exception.Code);
        }
        catch (Exception)
        {
            // Provider failures must not expose prompts, credentials or customer data through the HTTP boundary.
            return BuscaAssistidaExecutionResult.Failed("execution_failed");
        }
    }

    private static bool IsValid(BuscaAssistidaRequest request)
    {
        if (request.TenantId == Guid.Empty || request.CorrelationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Texto) || request.Texto.Length > 500 ||
            request.AdditionalProperties is { Count: > 0 })
            return false;
        return request.FiltrosAtuais is null || ValidateFilters(request.FiltrosAtuais);
    }

    private static bool ValidateFilters(BuscaAssistidaFiltros filters)
    {
        if (filters.AdditionalProperties is { Count: > 0 } ||
            filters.Finalidade is not (null or "buy" or "rent") ||
            filters.Tipo is not (null or "house" or "apartment" or "land" or "commercial") ||
            (filters.Regiao is { } regiao && (string.IsNullOrWhiteSpace(regiao) || regiao.Length > 160 ||
                regiao.Any(char.IsControl))) ||
            filters.PrecoMin < 0 || filters.PrecoMax < 0 || filters.QuartosMin < 0 || filters.VagasMin < 0 ||
            filters.AreaMin < 0 || filters.AreaMax < 0 ||
            (filters.PrecoMin.HasValue && filters.PrecoMax.HasValue && filters.PrecoMin > filters.PrecoMax) ||
            (filters.AreaMin.HasValue && filters.AreaMax.HasValue && filters.AreaMin > filters.AreaMax))
            return false;
        return true;
    }

    private static string BuildContent(BuscaAssistidaRequest request) => JsonSerializer.Serialize(new
    {
        texto = request.Texto!.Trim(),
        filtrosAtuais = request.FiltrosAtuais is null ? null : new
        {
            finalidade = request.FiltrosAtuais.Finalidade,
            regiao = request.FiltrosAtuais.Regiao,
            tipo = request.FiltrosAtuais.Tipo,
            precoMin = request.FiltrosAtuais.PrecoMin,
            precoMax = request.FiltrosAtuais.PrecoMax,
            quartosMin = request.FiltrosAtuais.QuartosMin,
            vagasMin = request.FiltrosAtuais.VagasMin,
            areaMin = request.FiltrosAtuais.AreaMin,
            areaMax = request.FiltrosAtuais.AreaMax
        }
    });
}
