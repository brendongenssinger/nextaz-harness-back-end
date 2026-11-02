# ADR: seleção de agentes do Harness por workflows do Cartório

- Status: aceito para implementação local
- Data: 2026-10-15
- Escopo: catálogo, execução selecionada e reconciliação do workflow do Cartório

## Contexto

O editor de workflows do Cartório precisa listar apenas agentes que o tenant pode usar e fixar a
versão escolhida na publicação. A execução deve continuar atravessando as mesmas políticas, limites,
lease, fencing, ferramentas e runtime do Harness. O Cartório não pode descobrir prompts, tools ou
agentes de outro produto, nem escolher o tenant pelo corpo HTTP.

## Decisão

O Harness expõe uma API específica do produto, protegida pela identidade M2M exata do backend do
Cartório:

- `GET /api/v1/cartorio/workflows/agents`
- `POST /api/v1/cartorio/workflows/agents/{agentId}/versions/{version}/executions`
- `GET /api/v1/cartorio/workflows/agents/{agentId}/versions/{version}/executions/{executionId}`

O tenant vem exclusivamente do JWT validado. O catálogo cruza a allowlist da feature, a vertical
`cartorio`, o registro do agente, a configuração habilitada do tenant e a existência de rota de
modelo. No recorte atual, a allowlist de produto contém somente `cartorio.atendimento`. A resposta
expõe nome, canais autorizados, versão atual e status; objetivo, instruções, tools, políticas, modelo
e provider permanecem internos.

A execução revalida allowlist, vertical, versão exata, configuração do tenant e canal. Em seguida,
ela cria a operação fixa `cartorio.workflow.agent.execute` e delega ao `ExecuteHarnessRequestHandler`.
Não existe caminho direto do controller ao provider ou às tools.

O backend do Cartório atesta `originator.kind=system|operator`. Para `system`, o Harness substitui o
ator por `nextaz-cartorio-workflow`; para `operator`, exige um identificador opaco e estável. Esses
campos servem para vincular idempotência ao contexto confiável, não para conceder autorização. O
cidadão é referenciado apenas por `conversationId`; CPF não faz parte do contrato.

O fingerprint inclui agente/versão, workflow/versão, run, step, ator, conversa e solicitação. Reusar
a correlação com contexto diferente retorna conflito. O contexto confiável é validado em memória e
incluído no hash, mas o schema atual não o persiste em campos consultáveis. Portanto, esta entrega
não oferece consulta de auditoria por ator, workflow ou step. Uma futura auditoria pesquisável exige
decisão de persistência e migration próprias.

A reconciliação lê somente execuções do mesmo tenant, agente allowlisted, vertical `cartorio`, versão
e operação fixa. Ela permite consultar uma execução anterior depois que a configuração do tenant for
desabilitada, desde que a definição allowlisted do agente continue registrada. Isso resolve resultado
desconhecido sem repetir o POST. Se a feature inteira ou a definição for removida, a consulta retorna
`404`.

## Ativação

```json
{
  "Harness": {
    "Cartorio": {
      "Enabled": true,
      "TenantIds": ["11111111-1111-1111-1111-111111111111"],
      "Channels": ["web"],
      "WorkflowAgents": {
        "Enabled": true,
        "TenantIds": ["11111111-1111-1111-1111-111111111111"]
      }
    }
  }
}
```

Os IDs são sintéticos. `WorkflowAgents:TenantIds` deve ser subconjunto de `Cartorio:TenantIds` ou o
processo falha no startup. A feature fica desabilitada quando a chave não é configurada.

## Consequências e limites

- O endpoint legado `/api/v1/cartorio/atendimentos` permanece compatível.
- O POST não deve ser repetido automaticamente após timeout de rede; o cliente consulta o GET com o
  `executionId` conhecido. A mesma correlação só é idempotente quando todo o contexto coincide.
- Revogar agente/configuração o remove do catálogo e bloqueia novas execuções. A seleção publicada
  com versão antiga falha com `409 agent_version_conflict` quando a versão atual muda.
- Não há catálogo histórico de versões. Só a versão atual e executável é anunciada.
- Não há schema novo, sessão de WhatsApp, SOAP, mídia, catálogo configurável multiagente ou chamada a
  provider pago nesta entrega.
