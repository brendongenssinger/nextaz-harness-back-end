# Estado da implementação

## Core e vertical imobiliária

| Área | Estado |
|---|---|
| Solution e dependências | Implementado em .NET 8, com separação Domain, Contracts, Application, Runtime, Providers, Infrastructure, Observability e API |
| Execução | CQRS/MediatR, fingerprint imutável, lease/reclaim, fencing, estados monotônicos e resposta persistida |
| Auth | JWT por audience; vertical imobiliária exclusivamente pelo machine client tenant-scoped |
| Agente imobiliário | `imobiliaria.atendimento` fixo, allowlist explícita de tenants/canais e limites de execução |
| Contexto e ações | Seis tools tipadas integradas ao backend imobiliário; IDs retornados pelo modelo são revalidados |
| Visitas | Intenção pendente em uma rodada e confirmação/cancelamento somente após novo inbound; argumentos congelados no domínio |
| Provider | OpenAI Responses configurável e simulação restrita a Development/Testing |
| PostgreSQL | Schema `harness`, migrations, execução idempotente e auditoria terminal atômica implementados |
| Observabilidade | Logs estruturados, métricas, ActivitySource e health com conectividade PostgreSQL |
| Testes | Unidade, runtime, HTTP/JWT, contrato do provider, contrato das tools e integração PostgreSQL isolada |

## Decisões preservadas

- O modelo não autoriza nem executa regras de domínio. O backend imobiliário revalida tenant, atendimento, imóvel, disponibilidade, intenção, confirmação e idempotência.
- Metadata do request é proveniência e seleção de recurso. Somente `source`, `atendimentoId` e `contatoId` são aceitos; `tenant_id` vem do JWT.
- O cliente `nextaz-imobiliaria-harness` chama sozinho o endpoint vertical com scope `imobiliaria.atendimento`. O cliente `nextaz-harness` chama apenas as tools internas do produto com scope `imobiliaria.tools`.
- A mesma correlação aceita somente o mesmo fingerprint de conteúdo, metadata, recursos, canal, agente e configuração. Payload divergente retorna conflito.
- Lease expirado permite reclaim; a finalização exige o token corrente e grava estado terminal e auditoria na mesma transação.
- Command IDs são GUIDs determinísticos derivados de tenant, correlação, tool e argumentos canônicos. O modelo não escolhe a chave idempotente.
- Produção exige PostgreSQL, provider real e integração imobiliária; configuração simulada falha no startup.
- O schema `harness` separa ownership, enquanto todas as chaves e consultas mantêm o tenant efetivo.

## Pendências explícitas

1. Evals de qualidade e segurança do agente imobiliário com casos reais anonimizados.
2. Exportação OpenTelemetry e reconciliação de custo por tentativa.
3. Memória/RAG, se houver uma fonte imobiliária canônica que preserve filtros de tenant.
4. Aplicação controlada de migrations e configuração dos segredos no ambiente de destino.
5. Gate independente de DBA antes de promover as migrations.

`harness.outbox_events` funciona nesta fase como auditoria transacional terminal. Não existe broker externo, dispatcher, retry de publicação ou confirmação de entrega. O catálogo/configuração de agentes continua carregado no startup; administração dinâmica, versionamento persistido e console operacional seguem como evolução.
