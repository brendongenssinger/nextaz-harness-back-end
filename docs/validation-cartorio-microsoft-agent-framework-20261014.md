# Validação: Microsoft Agent Framework no Cartório

Data: 2026-10-14

## Alcance validado

- `Microsoft.Agents.AI` 1.24.0 executado por um `ChatClientAgent` real com `IChatClient` falso e determinístico.
- Ativação apenas para `cartorio.atendimento` e para tenants explicitamente allowlisted.
- Tenant do JWT diferente do tenant do corpo recebe `403` antes da execução.
- Memória de outro tenant é rejeitada antes da criação do client do SDK.
- Uma instância de `IChatClient` é criada por request, sem estado compartilhado entre tenants.
- Tools são declarações de schema; o SDK retorna propostas e o runtime do Harness continua responsável pela execução autorizada.
- Tool fora da allowlist falha com `tool_forbidden`.
- Limite de tools, timeout, cancelamento do chamador e fallback explícito continuam sob o `BoundedAgentRuntime`.
- Histórico, instruções do tenant, resultados de tools, referências, usage e custo atravessam o adapter sem perda.
- Saída malformada do provider falha como `model_output_invalid`.

## Comandos focados

```powershell
dotnet test tests/NextAZ.Harness.Runtime.Tests/NextAZ.Harness.Runtime.Tests.csproj --no-restore --filter FullyQualifiedName~MicrosoftAgentFrameworkModelProviderTests
dotnet test tests/NextAZ.Harness.IntegrationTests/NextAZ.Harness.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CartorioHttpTests
```

Resultado focado: 13/13 testes de runtime e 10/10 testes HTTP do Cartório aprovados.

## Verificação ampla

Comando:

```powershell
pwsh -NoProfile -File .\scripts\verify.ps1
```

Resultado: `PARTIAL`, exit code `0`, build Release sem warnings, 144 testes descobertos, 141 aprovados, nenhum reprovado e 3 omitidos. Os três omitidos são testes PostgreSQL de persistência, lease/fencing e rollback que exigem `HARNESS_POSTGRES_TEST`; este incremento não altera schema nem persistência.

Resumo verificável: `artifacts/test-results/20261015T025324692Z-1fab9ef4/summary.json`.

A auditoria independente de pacotes foi executada com:

```powershell
dotnet list NextAZ.Harness.sln package --vulnerable --include-transitive --format json
```

O comando consultou NuGet.org e não reportou pacotes diretos ou transitivos com advisory conhecido nos 13 projetos. Esse resultado representa a base de advisories disponível em 2026-10-14, não uma garantia absoluta de ausência de vulnerabilidades futuras.

## Limites da evidência

Nenhuma chamada a provider pago, integração WhatsApp, SOAP, deploy, alteração de schema ou execução em produção fez parte desta validação. O provider determinístico comprova o caminho do SDK e suas fronteiras; homologação com modelo externo continua separada e exige credenciais/configuração operacionais.
