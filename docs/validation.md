# Validação local — 13/09/2026

## Suíte determinística

Comando:

```powershell
dotnet test NextAZ.Harness.sln --no-restore --configuration Debug
```

Resultado com `HARNESS_POSTGRES_TEST` configurado para o banco local isolado: **95 aprovados, zero falhas e zero ignorados**.

| Projeto | Testes aprovados |
|---|---:|
| Domain.Tests | 12 |
| Application.Tests | 9 |
| Runtime.Tests | 18 |
| Infrastructure.Tests | 12 |
| IntegrationTests | 44 |

A cobertura inclui JWT assinado, issuer/audience/lifetime, tenant duplicado, vertical M2M-only, bloqueio humano e do endpoint genérico, metadata allowlisted, isolamento de tenant/agente/recurso, fingerprint e conflito por correlação, limites do runtime, contratos das tools e adapter Responses com HTTP fake.

Após revisão da aprovação de visita, a confirmação foi removida da allowlist do agente.
Preparar intenção continua permitido, e o texto orienta revisão humana no produto.
A suíte HTTP foi repetida: **45/45 aprovados**, incluindo a ausência de permissão de
confirmação na composição real do agente. As outras suítes mantêm a evidência da rodada
anterior; não foram repetidas por essa mudança de política.

O adapter Responses foi testado para sucesso, output incompleto, 408/429/5xx, falha de rede, cancelamento, usage fora de faixa, payload acima de 1 MiB, tool call/output, histórico de conversa, nomes externos de funções e ausência de segredo no body. A resposta HTTP das tools imobiliárias também é limitada a 32 KiB antes do parse JSON. Nenhuma chamada paga foi feita.

## Auth real local

Foi adquirido um token real por `client_credentials` no NextAZ Auth local para o cliente `nextaz-imobiliaria-harness`, com tenant allowlisted. O token executou o endpoint vertical do Harness em modo `Simulation` explícito:

```text
ExecutionStatus=Completed
AgentId=imobiliaria.atendimento
HasContent=true
CorrelationMatched=true
```

O segredo e o access token não foram impressos ou persistidos.

## E2E das tools com o backend imobiliário

O script abaixo obtém um token M2M tenant-scoped sem imprimi-lo, envia uma execução ao Harness em `Simulation`, exige que a consulta tipada de contexto seja concluída e lê a execução persistida com a mesma identidade:

```powershell
.\scripts\test-imobiliaria-e2e.ps1 -AtendimentoId 173c471e-9c40-4ba1-a85f-733e2beea4d7
```

O orquestrador executou esse E2E após reabrir a API imobiliária: **Completed**, provider
`simulation`, `TypedContextValidated=true`, `PersistedReadValidated=true` e
`CorrelationMatched=true`. A primeira execução excedeu o limite de cinco segundos
da ferramenta durante inicialização; uma nova execução com outra correlação passou
após aquecimento das APIs. Isso valida a consulta tipada e a persistência; não
comprova envio por canais, provider pago nem todas as ferramentas de mutação.

## PostgreSQL isolado

As migrations `InitialHarnessPersistence`, `HardenOutboxIntegrity`, `AddExecutionFencing` e `ClarifyTerminalAudit` foram aplicadas no banco PostgreSQL local isolado. Três testes reais validaram fingerprint imutável, replay conflitante, crash/reclaim do lease, stale writer bloqueado, terminal monotônico e rollback conjunto quando a auditoria não pode ser inserida.

Sem `HARNESS_POSTGRES_TEST`, esses testes aparecem explicitamente como ignorados com a instrução de configuração; eles nunca retornam silenciosamente como aprovados.

Ainda é obrigatório obter o veredito independente do DBA antes de promover o schema. Produção, provider pago, broker externo e deploy não foram acessados.
