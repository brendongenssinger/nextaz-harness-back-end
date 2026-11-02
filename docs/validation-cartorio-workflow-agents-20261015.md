# Validação: catálogo e execução de agentes por workflow do Cartório

Data: 2026-10-15

## Alcance

- Catálogo filtrado por tenant, allowlist `cartorio.atendimento`, vertical, configuração ativa e rota
  de modelo.
- Seleção por versão exata, canal autorizado e identidade M2M do Cartório.
- Origem `system|operator` validada pelo Harness e incluída no fingerprint junto do contexto do
  workflow.
- Reconciliação limitada por tenant, produto, vertical, operação, agente e versão.
- Endpoint legado do Cartório preservado.
- Feature desabilitada e tenant fora da allowlist retornam `404`.

## Comandos focados

```powershell
dotnet test tests/NextAZ.Harness.Application.Tests/NextAZ.Harness.Application.Tests.csproj --no-restore --filter FullyQualifiedName~CartorioWorkflowAgentSelectionTests
dotnet test tests/NextAZ.Harness.IntegrationTests/NextAZ.Harness.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CartorioWorkflowAgentsHttpTests
```

Resultados antes da verificação ampla: 5/5 testes de Application e 12/12 testes HTTP aprovados.

Os testes cobrem configuração revogada, rota de modelo ausente, versão alterada, canal proibido,
origem inválida, fingerprint divergente, isolamento entre tenants, bloqueio do endpoint legado e
bloqueio explícito de agente de outra vertical mesmo quando a operação armazenada é forjada com o
nome da operação do Cartório.

## Verificação ampla

```powershell
pwsh -NoProfile -File .\scripts\verify.ps1
```

Resultado: `PARTIAL`, exit code `0`, build Release sem warnings, 161 testes descobertos, 158
aprovados, nenhum reprovado e 3 omitidos. Os testes omitidos são os testes PostgreSQL de
persistência, lease/fencing e rollback que exigem `HARNESS_POSTGRES_TEST`. Esta entrega não altera
schema nem persistência.

Resumo verificável:
`artifacts/test-results/20261015T030926441Z-b739a6bd/summary.json`.

`git diff --check` não encontrou erro de whitespace; os avisos exibidos referem-se à política de
conversão LF/CRLF do checkout.

## Limites

O contexto de origem participa da prevenção de replay, mas não está persistido em colunas
consultáveis; não existe relatório de auditoria por ator/workflow nesta entrega. Nenhum provider pago,
WhatsApp, SOAP, deploy, produção ou alteração de schema foi validado.
