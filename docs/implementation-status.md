# Estado da implementação

## Cartório — integração local em 15/09/2026

Endpoint vertical `POST/GET /api/v1/cartorio/atendimentos`, políticas M2M exclusivas,
metadata allowlist (`source`, `conversationId`, `solicitacaoId`) e agente v1 com
tenant/canal configurados no servidor. A conta humana do Cartório não recebe
audience do Harness. O frontend chama somente seu backend/BFF.

E2E real local comprovou portal Auth → BFF → criação de rascunho → token M2M →
Harness → sugestão exibida no frontend. Este perfil usa provider `simulation` e
armazenamento em memória. Não implementa ferramentas Engegraph, envio de mensagens,
atos cartorários, configuração dinâmica pelo console ou avaliação de LLM real.

Verificação `-Offline`: 115 aprovados, 3 testes PostgreSQL omitidos, zero falhas,
resultado `partial`. Sem alteração de schema ou validação nova da persistência.

## Core e vertical imobiliária

| Área | Estado |
|---|---|
| Solution e dependências | Implementado em .NET 8, com separação Domain, Contracts, Application, Runtime, Providers, Infrastructure, Observability e API |
| Execução | CQRS/MediatR, fingerprint imutável, lease/reclaim, fencing, estados monotônicos e resposta persistida |
| Auth | JWT por audience; vertical imobiliária exclusivamente pelo machine client tenant-scoped |
| Agente imobiliário | `imobiliaria.atendimento` fixo, allowlist explícita de tenants/canais e limites de execução |
| Contexto e ações | Adapters de tools tipadas integrados ao backend imobiliário; o registry atual disponibiliza apenas tools `Read`, sem liberar as escritas ao agente |
| Visitas | Intenção pendente em uma rodada e confirmação/cancelamento somente após novo inbound; argumentos congelados no domínio |
| Provider | OpenAI Responses configurável e simulação restrita a Development/Testing |
| Recibos | `funcionario-digital.recibo` v2 aceita OpenAI multimodal com JSON Schema estrito; provider e modelo são configuráveis, Hugging Face permanece como opção de rollback e o Harness não confirma pagamentos |
| PostgreSQL | Schema `harness`, migrations, execução idempotente e auditoria terminal atômica implementados |
| Observabilidade | Logs estruturados, métricas, ActivitySource e health com conectividade PostgreSQL |
| Testes | Unidade, runtime, HTTP/JWT, contrato do provider, contrato das tools e integração PostgreSQL isolada |

## Console administrativo conectado — leitura

O runtime expõe um snapshot tenant-scoped do catálogo, configuração efetiva,
rotas de modelo e limites em `GET /api/v1/admin/tenants/{tenantId}/overview`.
O endpoint exige `HarnessRead` e recusa divergência entre o tenant assinado no
token e o tenant da rota. O Auth possui cliente BFF que obtém autorização M2M e
consome esse snapshot sem entregar segredo ou bearer token ao navegador.

Este recorte não adiciona escrita: criar draft persistente, testar provider,
publicar versão imutável, ativar, desativar e fazer rollback continuam pendentes.

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

## Guias e sensores — 15/09/2026

O [índice de trabalho](../AGENTS.md) e o mapa de [harness engineering](harness-engineering.md)
separam runtime, verificações de desenvolvimento e console existente no Auth.
O console é uma prévia: não grava nem ativa configurações. Esta organização não
adiciona integração administrativa, RAG, orçamento agregado de tokens/custo ou
permissões de escrita. Os limites atuais de tokens são de saída por chamada.

## Proteção contra loops — 15/09/2026

`ToolCallHistory` normaliza argumentos JSON por execução antes de detectar uma
chamada repetida. Alterações de espaços, ordem de propriedades (inclusive aninhadas)
e escapes de strings não geram uma chamada nova. Campos duplicados são rejeitados.
Arrays mantêm ordem; tipos/valores permanecem distintos. Não há inferência de
equivalência numérica/de domínio (por exemplo, `1` e `1.0` mantêm representações
distintas). O estado é local à execução, não cache compartilhado ou deduplicação
distribuída. Chaves idempotentes do produto não foram alteradas.

Teste antes/depois reproduziu sete falhas, corrigidas pelos 12 casos novos.
A rodada completa local tem 107 aprovados e 3 testes PostgreSQL omitidos. Orçamento
agregado, contexto adaptativo, evals reais e conexão do console seguem pendentes.
