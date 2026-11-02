# NextAZ Harness

Core de execução, orquestração e governança de IA da NextAZ em **.NET 8**.

> O modelo interpreta e propõe; o Harness controla; o backend do produto autoriza, valida, executa e audita.

Para trabalhar no projeto, comece por [AGENTS.md](AGENTS.md) e pelo mapa de
[harness engineering](docs/harness-engineering.md). O frontend existente está
no `nextaz-auth`, em `/admin/harness`; hoje é uma prévia sem persistência ou
ativação de agentes, não uma integração operacional com esta API.

## Estado implementado

- API JWT multi-tenant com policies distintas para o Harness administrativo e para o atendimento imobiliário.
- Snapshot administrativo tenant-scoped em `GET /api/v1/admin/tenants/{tenantId}/overview`, consumido pelo BFF do Auth com token M2M; este endpoint é somente leitura e não publica configurações.
- Endpoint vertical `POST /api/v1/imobiliaria/atendimentos` fixo no agente `imobiliaria.atendimento` e consulta por execução restrita ao tenant e ao agente.
- Acesso vertical exclusivamente por `client_credentials` tenant-scoped do cliente `nextaz-imobiliaria-harness`. Usuários/BFF não recebem audience do Harness e esse cliente não acessa `/api/v1/executions`.
- Metadata imobiliária estritamente allowlisted: `source=nextaz-imobiliaria`, `atendimentoId` obrigatório e `contatoId` opcional. Esses IDs selecionam recursos; nunca concedem autorização.
- Adapters de tools tipadas para consultar catálogo/contexto, registrar recomendação, preparar/confirmar/cancelar intenção de visita e encaminhar ao corretor. O registry atual disponibiliza somente tools de leitura (`Read`); adapters de escrita registrados não significam ações liberadas ao agente.
- Escritas usam `commandId` GUID determinístico por tenant, mensagem, tool e argumentos canônicos. O backend imobiliário revalida tenant, atendimento, imóvel, estado, confirmação posterior e idempotência.
- Adapter real da OpenAI Responses API, habilitado somente por configuração externa, com `store=false`, modelo explícito, limites de resposta, cancelamento, imagens HTTPS em hosts allowlisted, Structured Outputs e fallback apenas para indisponibilidade transitória.
- Provider simulado determinístico permitido somente em Development/Testing. Nenhuma chamada paga é executada pelos testes.
- Persistência PostgreSQL no schema exclusivo `harness`, com fingerprint imutável, lease com fencing e replay idempotente por tenant/operação/correlação.
- Estado terminal e evento de auditoria são gravados atomicamente na mesma transação PostgreSQL.
- Loop do agente limitado por steps, tool calls, tempo e tokens de saída por chamada. Não há orçamento agregado de tokens/custo por execução. Tool outputs são tratados como dados não confiáveis.
- Proteção contra tools repetidas por execução: normaliza ordem dos campos, espaços e escapes de strings antes de comparar; rejeita campos JSON duplicados sem invocar a ferramenta. Não substitui a idempotência persistida do produto.
- Histórico de mensagens opcional, conteúdo de resposta, usage, provider/model, timeline e erro terminal persistidos.

O Cartório possui a vertical dedicada `POST/GET /api/v1/cartorio/atendimentos`,
autorizada por `nextaz-cartorio-harness`, scope `cartorio.atendimento`, produto
`nextaz-cartorio` e tenant explícito no token. O agente `cartorio.atendimento` v1
foi integrado ao backend e frontend do produto. A configuração local atual usa
provider simulado, sem tools cartorárias ou chamadas a modelos externos.
Inicie esse perfil por `../nextaz-cartorio-back-end/scripts/start-local-harness.ps1`:
Auth isolado em `5091`, runtime em `5088` e persistência em memória declarada no health.

## Executar os testes

Pré-requisitos: SDK .NET 8 ou superior. Todos os projetos compilam para `net8.0`.

```powershell
cd C:\Users\brend\source\nextaz-harness-back-end
.\scripts\verify.ps1
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
- `POST /api/v1/executions`: execução administrativa do Harness, inacessível aos tokens do produto Imobiliária.

## Contrato de autenticação imobiliária

O JWT usa audience `nextaz-harness-api` e exatamente um `tenant_id` GUID.

| Origem | Claims exigidas |
|---|---|
| Worker/outbox do backend | `sub`, `client_id` e `azp` iguais a `nextaz-imobiliaria-harness`, `product=nextaz-imobiliaria`, scope `imobiliaria.atendimento`, role `Service` |

O worker adquire o token por `client_credentials` para o tenant em processamento e pode mantê-lo somente em cache até perto da expiração. Tokens humanos e tokens de serviço não pertencem ao banco nem ao outbox.

## Configuração operacional

O [guia de preparação de produção](docs/production.md) reúne `.env.example`,
`scripts/test-production-env.ps1`, Dockerfile e Compose. O template é intencionalmente
incompleto: não ativa verticais sem decisão explícita e não é aprovação de deploy.

Segredos e valores de ambiente ficam fora do repositório:

```text
Authentication__Authority
Authentication__Audience=nextaz-harness-api
ConnectionStrings__Harness
Harness__Persistence__Provider=PostgreSql
Harness__Providers__OpenAI__Enabled=true
Harness__Providers__OpenAI__Endpoint=https://api.openai.com/v1/
Harness__ProviderEncryption__KeyVaultKeyId=https://<vault>.vault.azure.net/keys/nextaz-harness-provider-kek/<version>
Harness__Imobiliaria__Enabled=true
Harness__Imobiliaria__TenantIds__0=<tenant-guid>
Harness__Imobiliaria__Integration__Enabled=true
Harness__Imobiliaria__Integration__ApiBaseUrl
Harness__Imobiliaria__Integration__TokenEndpoint
Harness__Imobiliaria__Integration__ClientId=nextaz-harness
Harness__Imobiliaria__Integration__ClientSecret
Harness__Imobiliaria__Integration__Scope=imobiliaria.tools
```

O token e o modelo são cadastrados por tenant e versão do agente. O token fica cifrado no PostgreSQL por uma chave de dados exclusiva, e essa chave é envolvida por uma chave RSA do Key Vault. Os canais aceitos pelo agente são `web`, `api`, `whatsapp`, `instagram`, `facebook` e `telegram`. A allowlist de tenants é obrigatória. O startup em Production recusa simulação, persistência volátil, provider ausente ou integração imobiliária ausente.

As migrations devem ser aplicadas de forma controlada antes de iniciar a aplicação. O histórico do EF fica em `harness.__ef_migrations_history`.

## Limites operacionais atuais

- `harness.outbox_events` é o registro transacional de auditoria terminal desta entrega. Não representa publicação ou confirmação de entrega em broker externo.
- Uma execução `Running` pode ser retomada após a expiração do lease. O token de fencing impede que o worker anterior sobrescreva o resultado do novo owner.
- Memória/RAG continua desabilitada; o contexto imobiliário vem exclusivamente das tools tipadas do backend do produto.
- Custo permanece `null` quando o provider não entrega um valor confiável. Não há tabela local de preços inventada.
- Não há evals de qualidade do modelo, exportador OpenTelemetry, deploy ou validação com chamada paga nesta entrega.

A implementação do provider segue o contrato oficial da [OpenAI Responses API](https://developers.openai.com/api/reference/cli/resources/responses/methods/create). O roadmap e as evidências ficam em [docs/implementation-status.md](docs/implementation-status.md) e [docs/validation.md](docs/validation.md).

Não houve commit, push ou deploy.
