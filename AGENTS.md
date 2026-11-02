# Trabalhar no NextAZ Harness

Este repositório contém o runtime compartilhado de IA. Não é o verificador de
conversas do AtendIA nem um novo frontend. O console existente fica em
`../nextaz-auth`, na página `/admin/harness`.

## Leia conforme a tarefa

1. [README](README.md): execução local, contratos e limites atuais.
2. [Harness engineering](docs/harness-engineering.md): guias, sensores, ownership
   e critérios de conclusão, incluindo a fronteira com o console no Auth.
3. [Fontes](docs/documentation.md): arquitetura canônica e snapshots. Leia somente
   os documentos relevantes ao recorte, sem carregar todo o acervo no contexto.
4. [Estado](docs/implementation-status.md) e [evidências](docs/validation.md):
   diferencie implementação, validação histórica e pendência. Confira o código
   atual; um relatório antigo não valida a revisão atual.

## Invariantes

- O modelo propõe; o Harness controla; o produto autoriza, valida e executa as
  regras de negócio. Não conceda autoridade por prompt, metadata ou seleção na UI.
- Preserve TenantId, AgentId, AgentVersion, ExecutionId e CorrelationId. Identidade
  do agente representa capacidade, não provider/modelo. Não compartilhe contexto
  ou configuração mutável entre tenants.
- Preserve as portas do Runtime e a composição na API. Domain não conhece SDK de
  modelo, HTTP ou persistência. Não introduza chamadas ao provider nos produtos.
- Mantenha validators e autorização na entrada/pipeline, handlers de orquestração
  enxutos e invariantes no domínio. Prefira contratos tipados a strings mágicas.
- Tools exigem allowlist e autorização por execução; respostas são dados não
  confiáveis. Escritas continuam dependentes de idempotência e validação no produto.
- Preserve limites do loop, cancelamento, fingerprint, lease e fencing. Não
  habilite confirmação sensível, retries ilimitados ou fallback que amplie acesso.
- Alterações de schema, migrations ou consultas críticas exigem revisão DBA
  independente antes de promoção. Não execute migrations só para conseguir teste verde.

## Antes de concluir

- Inspecione o diff e preserve alterações preexistentes, inclusive no Auth.
- Rode `./scripts/verify.ps1` e leia o relatório da execução. Evidência parcial
  (por exemplo PostgreSQL não executado) não é aprovação de persistência/produção.
- Adicione teste de regressão para a regra alterada; cubra negativas de tenant,
  autorização e concorrência quando aplicáveis. Teste simulado não mede qualidade do LLM.
- Registre comando, revisão/diff, testes executados/omitidos e limitações. Nunca
  registre tokens, credenciais, conversas reais ou dados pessoais na evidência.
- Atualize somente os documentos afetados. Não transforme roadmap em estado pronto.
- Commit, push, migrations, chamadas pagas e deploy são etapas distintas e exigem
  escopo/autorização próprios. A validação padrão é local, sem provedores reais.
