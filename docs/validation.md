# Evidências de validação local

## Integração Cartório — 15/09/2026

Comando: `.tools/powershell/pwsh.exe -NoProfile -File nextaz-harness-back-end/scripts/verify.ps1 -Offline`
a partir do workspace. Relatório:
`artifacts/test-results/20260915T160719719Z-6deb1c69/summary.json`.
Build com zero erros/avisos; 115 testes aprovados, 3 PostgreSQL omitidos;
resultado `partial`, exit 0. Auditoria NuGet desabilitada nesse perfil.

O navegador executou login no Auth isolado `5091`, SSO via frontend `5177`,
criação de rascunho (`201`) e execução pela API `5187` (`200`, `Completed`), com
retorno de `cartorio.atendimento`, versão 1. O runtime respondeu pelo provider
simulado em `5088`. Cookie BFF HttpOnly/Lax, logout seguido de `401` e retorno
SSO foram verificados. Não houve chamada paga, provider externo ou deploy.
Detalhes em `../nextaz-cartorio-back-end/docs/NXZ-VAL-CART-001 - Validacao local.md`.

## Runtime: proteção contra loops — 15/09/2026

Base `9c6176a` com alterações locais não commitadas. Antes da correção,
`ToolLoopRegressionTests` teve 7 falhas e 5 aprovados; depois, 12 aprovados.
A suíte inclui bloqueio de repetição com nova CallId e JSON reordenado, aninhado
ou com escapes, rejeição de nomes duplicados e isolamento do histórico por execução.
O executor é falso nesses casos para medir somente a proteção contra loops;
as suítes existentes continuam cobrindo autorização e contratos das tools.

Comando completo: `pwsh -NoProfile -File scripts/verify.ps1 -Offline`.
Restore/build aprovados, zero warnings/erros de compilação. **107 testes aprovados,
zero falhas e 3 PostgreSQL omitidos**, resultado `partial`, 110 descobertos.
Evidência: `artifacts/test-results/20260915T153927556Z-8180839f/summary.json`.
Sem migrations, chamadas pagas, auditoria NuGet, E2E real, alterações no Auth ou deploy.
Esta rodada adiciona controle determinístico; não avalia a qualidade de um modelo.

## Guias e sensores — 15/09/2026

Checkout: `nextaz-harness-back-end`, HEAD base `9c6176a`, com as alterações locais
de documentação e scripts desta entrega ainda não commitadas. O runtime não foi
alterado neste recorte. O parecer do agente de arquitetura aprovou os guias e a
fronteira com Auth; as ressalvas sobre tokens por chamada e ferramentas de escrita
foram corrigidas na documentação.

Comando executado pelo agente de desenvolvimento:

```powershell
pwsh -NoProfile -File ./scripts/verify.ps1 -Offline
```

Restore/build passaram. Resultado do perfil: **partial**, 95 testes aprovados,
zero falhas e os 3 testes PostgreSQL previstos omitidos (98 descobertos).
Distribuição: Domain 12, Application 9, Runtime 18, Infrastructure 11 aprovados +
3 omitidos, Integration 45. O relatório foi conferido pelo agente principal em
`artifacts/test-results/20260915T152227847Z-5ea250da/summary.json`.

O agente principal também executou `scripts/tests/TrxVerification.Tests.ps1`:
**9 assertions aprovadas**, cobrindo TRX válido, ausente, malformado, vazio,
falho, incompleto e política de testes omitidos. O parser confronta contadores
com resultados individuais; não usa apenas o exit code do `dotnet test`.

O perfil `-RequirePostgres -AcknowledgeDisposablePostgres`, sem conexão configurada,
foi testado negativamente: saída 1, `postgres_connection_not_configured`, sem
restore, build ou acesso a banco. Evidência em
`artifacts/test-results/20260915T152302432Z-51170274/summary.json`.

No checkout `nextaz-auth`, sem alterar seus fontes, o agente principal executou:

```powershell
dotnet test tests/Nextaz.Auth.IntegrationTests/Nextaz.Auth.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~HarnessPageAuthorizationTests --logger "trx;LogFileName=harness-auth.trx" --results-directory output/harness-verification-20260915
```

**6 aprovados, zero falhas/omitidos.** A suíte chama o PageModel/filtro com
colaboradores falsos: valida membership atual, usuário anônimo, contexto de
empresa e recusa de POST; não representa navegador ou autenticação E2E real.

Limites desta rodada: sem PostgreSQL, migrations, auditoria NuGet (modo offline),
evals de LLM, provider pago, QA visual ou integração Auth-console-runtime.
Nenhum commit, push, mudança no Hangfire ou deploy. Artefatos locais não são
evidência de produção nem substituem o gate DBA.

## Histórico — 13/09/2026

As evidências abaixo pertencem à rodada anterior; não foram reproduzidas integralmente em 15/09.

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

## OpenAI multimodal para recibos — 02/11/2026

O agente `funcionario-digital.recibo` passou a transportar a imagem como entrada multimodal
tipada, exigir Structured Output por JSON Schema e permitir seleção explícita de OpenAI,
Hugging Face ou simulação. O adapter bloqueia URLs fora da allowlist e o produto continua
responsável por validar e registrar qualquer venda.

`scripts/verify.ps1 -Offline` compilou a solution sem warnings e executou 184 testes com
sucesso; os três testes PostgreSQL foram omitidos de forma explícita porque nenhum banco
descartável foi autorizado. O resultado é `partial`: não houve chamada paga, avaliação de
qualidade do modelo, teste com imagem real, deploy nem ativação em produção.
