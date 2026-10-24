# NextAZ Harness

Core de execução, orquestração e governança de IA da NextAZ em **.NET 8**.

> O modelo interpreta e propõe; o Harness controla; o backend do produto autoriza, valida, executa e audita.

## Estado implementado

- API JWT multi-tenant com policies distintas para o Harness administrativo e para o atendimento imobiliário.
- Endpoint vertical `POST /api/v1/imobiliaria/atendimentos` fixo no agente `imobiliaria.atendimento` e consulta por execução restrita ao tenant e ao agente.
- Acesso vertical exclusivamente por `client_credentials` tenant-scoped do cliente `nextaz-imobiliaria-harness`. Usuários/BFF não recebem audience do Harness e esse cliente não acessa `/api/v1/executions`.
- Metadata imobiliária estritamente allowlisted: `source=nextaz-imobiliaria`, `atendimentoId` obrigatório e `contatoId` opcional. Esses IDs selecionam recursos; nunca concedem autorização.
- Tools tipadas para consultar catálogo/contexto, registrar recomendação, preparar/confirmar/cancelar intenção de visita e encaminhar ao corretor.
- Escritas usam `commandId` GUID determinístico por tenant, mensagem, tool e argumentos canônicos. O backend imobiliário revalida tenant, atendimento, imóvel, estado, confirmação posterior e idempotência.
- Adapter real da OpenAI Responses API, habilitado somente por configuração externa, com `store=false`, modelo explícito, limites de resposta, cancelamento e fallback apenas para indisponibilidade transitória.
- Provider simulado determinístico permitido somente em Development/Testing. Nenhuma chamada paga é executada pelos testes.
- Persistência PostgreSQL no schema exclusivo `harness`, com fingerprint imutável, lease com fencing e replay idempotente por tenant/operação/correlação.
- Estado terminal e evento de auditoria são gravados atomicamente na mesma transação PostgreSQL.
- Loop do agente limitado por steps, tool calls, tempo e tokens. Tool outputs são tratados como dados não confiáveis.
- Histórico de mensagens opcional, conteúdo de resposta, usage, provider/model, timeline e erro terminal persistidos.

O agente `cartorio.atendimento` continua sendo somente uma fixture de simulação. A vertical operacional implementada nesta entrega é `imobiliaria.atendimento`.

## Executar os testes

Pré-requisitos: SDK .NET 8 ou superior. Todos os projetos compilam para `net8.0`.

```powershell
cd C:\Users\brend\source\nextaz-harness
dotnet restore NextAZ.Harness.sln --configfile NuGet.Config
dotnet build NextAZ.Harness.sln --no-restore
dotnet test NextAZ.Harness.sln --no-restore
```

O perfil Development usa simulação e tenants fictícios. A API local usa `http://localhost:5088`.

Para a integração local com Auth, PostgreSQL e backend Imobiliária já iniciados:

```powershell
.\scripts\run-imobiliaria-local.ps1
```

Com os três serviços ativos, valide o fluxo M2M, a tool de contexto e a leitura persistida:

```powershell
.\scripts\test-imobiliaria-e2e.ps1 -AtendimentoId <atendimento-sintetico-guid>
```

Use `-ApplyMigrations` somente quando quiser aplicar as migrations no banco local isolado configurado em `.local`.

- `GET /health/live`: processo disponível.
- `GET /health/ready`: informa modo, persistência, provider e mensageria; em PostgreSQL também verifica conectividade.
- `POST /api/v1/imobiliaria/atendimentos`: executa o atendimento especializado.
- `GET /api/v1/imobiliaria/atendimentos/{executionId}`: consulta somente uma execução do mesmo tenant e agente.
- `POST /api/v1/funcionario-digital/receipts`: propõe a leitura estruturada de um recibo para validação pelo Funcionário Digital.
- `GET /api/v1/funcionario-digital/receipts/{executionId}`: consulta somente uma execução de recibo do mesmo tenant e agente.
- `GET /api/v1/admin/tenants/{tenantId}/overview`: lista agentes publicados e vínculos operacionais do tenant.
- `PUT /api/v1/admin/tenants/{tenantId}/routes`: cria ou atualiza, com revisão otimista e auditoria, o vínculo `canal + operação -> agente + versão`.
- `POST /api/v1/executions`: execução administrativa do Harness, inacessível aos tokens do produto Imobiliária.

O roteamento operacional é resolvido por `tenant + canal + operação`. Para recibos,
a chave é `tenant + api + funcionario-digital.recibo`. Um vínculo persistido ativo
fixa também a versão publicada do agente; se ela não estiver mais disponível, a
execução falha fechada. Um vínculo persistido desativado bloqueia a rota. A configuração
estática permanece apenas como fallback de migração enquanto não existir vínculo para
aquela chave.

## Contrato de autenticação imobiliária

O JWT usa audience `nextaz-harness-api` e exatamente um `tenant_id` GUID.

| Origem | Claims exigidas |
|---|---|
| Worker/outbox do backend | `sub`, `client_id` e `azp` iguais a `nextaz-imobiliaria-harness`, `product=nextaz-imobiliaria`, scope `imobiliaria.atendimento`, role `Service` |

O worker adquire o token por `client_credentials` para o tenant em processamento e pode mantê-lo somente em cache até perto da expiração. Tokens humanos e tokens de serviço não pertencem ao banco nem ao outbox.

Para recibos, o JWT também usa audience `nextaz-harness-api`, exatamente um
`tenant_id`, `product=funcionario-digital`, scope `funcionario-digital.receipts`
e role `Service`. `sub`, `client_id` e `azp` devem ser iguais a um cliente M2M
presente em `Authentication:FuncionarioDigitalReceiptClientIds`. O Harness só
propõe a estrutura; o Funcionário Digital valida o emissor, os totais e a
evidência literal de pagamento antes de aplicar qualquer efeito.

## Configuração operacional

Segredos e valores de ambiente ficam fora do repositório:

```text
Authentication__Authority
Authentication__Audience=nextaz-harness-api
Authentication__FuncionarioDigitalReceiptClientIds__0=<client-id-m2m-do-tenant>
ConnectionStrings__Harness
Harness__Persistence__Provider=PostgreSql
Harness__KeyVault__Enabled=true
Harness__KeyVault__VaultUri=https://nextaz-kv-prod-70bec273.vault.azure.net/
Harness__Providers__OpenAI__Enabled=true
Harness__Providers__OpenAI__Endpoint=https://api.openai.com/v1/
Harness__Providers__OpenAI__ApiKey
Harness__Imobiliaria__Enabled=true
Harness__Imobiliaria__TenantIds__0=<tenant-guid>
Harness__Imobiliaria__Integration__Enabled=true
Harness__Imobiliaria__Integration__ApiBaseUrl
Harness__Imobiliaria__Integration__TokenEndpoint
Harness__Imobiliaria__Integration__ClientId=nextaz-harness
Harness__Imobiliaria__Integration__ClientSecret
Harness__Imobiliaria__Integration__Scope=imobiliaria.tools
Harness__FuncionarioDigital__Receipts__Enabled=true
Harness__FuncionarioDigital__Receipts__TenantIds__0=<tenant-guid>
Harness__FuncionarioDigital__Receipts__Instructions=
```

### Modelo OpenAI centralizado no Azure Key Vault

O runtime .NET consulta diretamente o Azure Key Vault durante a inicialização e
carrega somente o segredo `Harness--Providers--OpenAI--Model`. Esse nome é mapeado
para `Harness:Providers:OpenAI:Model` e prevalece sobre valores locais. O modelo
não deve ser repetido no `.env.production`.

```powershell
# Primeiro cadastre o modelo explicitamente aprovado; não há valor padrão.
az keyvault secret set --vault-name nextaz-kv-prod-70bec273 `
  --name Harness--Providers--OpenAI--Model --value '<modelo-aprovado>'
```

Na VPS, o container usa `DefaultAzureCredential`. Como a HostGator não possui
Managed Identity, informe ao container as credenciais de um service principal
dedicado por `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` e `AZURE_CLIENT_SECRET`. Conceda
somente `Key Vault Secrets User`, preferencialmente no escopo desse segredo. Não
grave essas credenciais no Git, na imagem Docker ou em logs.

Com OpenAI habilitado, Production recusa iniciar sem `Harness:KeyVault:Enabled`.
Falha de autenticação, cofre inacessível ou segredo ausente também interrompe o
startup sem revelar o valor. O custo adicional estimado permanece **R$ 0/mês**
no volume atual de operações do cofre.

Os canais aceitos pelo agente são `web`, `api`, `whatsapp`, `instagram`, `facebook` e `telegram`. A allowlist de tenants é obrigatória. O startup em Production recusa simulação, persistência volátil, provider ausente ou integração imobiliária ausente.

As migrations devem ser aplicadas de forma controlada antes de iniciar a aplicação. O histórico do EF fica em `harness.__ef_migrations_history`.

## Limites operacionais atuais

- `harness.outbox_events` é o registro transacional de auditoria terminal desta entrega. Não representa publicação ou confirmação de entrega em broker externo.
- Uma execução `Running` pode ser retomada após a expiração do lease. O token de fencing impede que o worker anterior sobrescreva o resultado do novo owner.
- Memória/RAG continua desabilitada; o contexto imobiliário vem exclusivamente das tools tipadas do backend do produto.
- Custo permanece `null` quando o provider não entrega um valor confiável. Não há tabela local de preços inventada.
- Não há evals de qualidade do modelo, exportador OpenTelemetry, deploy ou validação com chamada paga nesta entrega.

A implementação do provider segue o contrato oficial da [OpenAI Responses API](https://developers.openai.com/api/reference/cli/resources/responses/methods/create). O roadmap e as evidências ficam em [docs/implementation-status.md](docs/implementation-status.md) e [docs/validation.md](docs/validation.md).

Não houve commit, push ou deploy.
