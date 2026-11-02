# Contrato HTTP: agentes do workflow do Cartório

Todos os endpoints exigem o JWT M2M do backend do Cartório com `product=nextaz-cartorio`, role
`Service`, scope `cartorio.atendimento` e os valores esperados de `sub`, `client_id` e `azp`. O tenant
é lido do claim único `tenant_id` e nunca é aceito no body.

## Catálogo

`GET /api/v1/cartorio/workflows/agents`

```json
{
  "items": [
    {
      "id": "cartorio.atendimento",
      "name": "Atendimento do cartorio",
      "channels": ["web"],
      "versions": [{ "version": 1, "status": "active" }]
    }
  ]
}
```

Um catálogo vazio é válido. Agente revogado, desabilitado, fora da allowlist, sem rota de modelo ou de
outra vertical não aparece. Feature/tenant fora da allowlist retorna `404`.

## Execução

`POST /api/v1/cartorio/workflows/agents/{agentId}/versions/{version}/executions`

```json
{
  "content": "Mensagem para o agente",
  "correlationId": "workflow-run-step-attempt",
  "channel": "web",
  "workflowId": "11111111-1111-1111-1111-111111111111",
  "workflowVersion": 1,
  "runId": "22222222-2222-2222-2222-222222222222",
  "stepId": "agent-step-1",
  "conversationId": "33333333-3333-3333-3333-333333333333",
  "solicitacaoId": null,
  "originator": { "kind": "system", "actorId": null }
}
```

`originator.kind` aceita somente `system` ou `operator`. `system` proíbe `actorId`; `operator` exige
um identificador opaco com até 128 caracteres. `stepId` aceita identificador estável com até 120
caracteres. Tenant, prompt, tools, modelo, provider e permissões não são aceitos no body.

Sucesso terminal retorna `201`; execução ainda ativa retorna `202`. Respostas usam `HarnessResponse`.
Falhas relevantes:

- `400 invalid_request`: corpo ou origem inválidos;
- `403 agent_disabled` ou `agent_channel_forbidden`: configuração revogada ou canal proibido;
- `404 agent_not_found` ou `tenant_agent_not_configured`: agente não pertence ao produto/tenant;
- `409 agent_version_conflict`: versão publicada não é mais a atual;
- `409 execution_conflict`: correlação reutilizada com conteúdo ou contexto diferente;
- `503 model_route_unavailable`/`provider_unavailable`: execução indisponível;
- `504 execution_timed_out`: prazo do Harness excedido.

## Reconciliação

`GET /api/v1/cartorio/workflows/agents/{agentId}/versions/{version}/executions/{executionId}`

Retorna `200` com `HarnessResponse` somente quando tenant, agente allowlisted, vertical, versão e
operação de workflow coincidem. Qualquer divergência retorna `404`, inclusive uma execução conhecida
do mesmo tenant pertencente a outro produto ou criada pelo endpoint legado.

O consumidor não deve repetir automaticamente um POST cujo resultado seja desconhecido. Quando
possuir `executionId`, deve consultar este GET. Sem `executionId`, a recuperação por correlação não faz
parte deste contrato.
