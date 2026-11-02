# Harness engineering na NextAZ

## Objetivo e origem

Aplicar a orientação enviada por Brendon em 15/09/2026, na transcrição
“Harness Engineering: A Nova Corrida do Mercado de IA”: contexto direcionado,
ferramentas controladas, estado, limites, guias antes da ação e sensores depois.
As alegações de benchmark do vídeo não foram verificadas e não são metas ou
evidências deste projeto. A arquitetura continua nas [fontes documentadas](documentation.md).

Há dois recortes complementares:

- **Runtime de produto:** recebe a solicitação e controla modelo, contexto,
  ferramentas, permissões, estado e resultado. Vive neste repositório.
- **Ambiente de desenvolvimento:** orienta humanos/agentes e verifica mudanças.
  O índice `AGENTS.md`, os testes e `scripts/verify.ps1` apoiam esse trabalho;
  não substituem o runtime nem comprovam a qualidade de uma resposta de IA.

Spec Kit pode organizar especificação, plano e tarefas de cada evolução. Não foi
inicializado neste repositório nesta entrega; o piloto `005-harness-conversas` do
AtendIA não deve ser apresentado como a especificação deste runtime compartilhado.

## Onde cada responsabilidade fica

| Componente | Responsabilidade | Limite atual verificado no código |
|---|---|---|
| `nextaz-auth`, `/admin/harness` | Console administrativo e autorização da página | Prévia para SuperAdmin; catálogo demonstrativo e rascunhos na página, sem salvar, ativar ou consultar agentes do runtime |
| `NextAZ.Harness.Application` | Orquestrar execução, resolver perfil e persistir resultado | Não assumir regras comerciais dos produtos |
| `NextAZ.Harness.Runtime` | Resolver agente/modelo, aplicar policies e executar loop/tools | Execução limitada; RAG/memória externa não habilitados |
| `NextAZ.Harness.Providers` | Adaptar o provider às portas próprias | OpenAI e simulação; seletores no Auth não implementam outros conectores |
| `NextAZ.Harness.Infrastructure` | Catálogo/configuração, adapters de tools e persistência | Configuração de agentes carregada no startup; publicação dinâmica ainda pendente |
| Backend de cada produto | Regras determinísticas, autorização dos recursos e efeitos de negócio | Uma sugestão do modelo não confirma pagamento nem concede permissão |

O frontend não será duplicado. A integração futura deverá passar pelo servidor
do Auth, preservando autorização e proteção das credenciais. O console não deve
usar o token de serviço da vertical imobiliária como atalho administrativo.

Pontos de entrada no checkout vizinho `nextaz-auth`:

- `src/Nextaz.Auth.Server/Pages/Admin/Harness.cshtml` — interface e aviso de prévia.
- `src/Nextaz.Auth.Server/Pages/Admin/Harness.cshtml.cs` — membership atual de
  SuperAdmin consultado no servidor; escritas recusadas nesta fase.
- `src/Nextaz.Auth.Server/wwwroot/js/harness-admin.js` — propostas locais por empresa/agente.
- `tests/Nextaz.Auth.IntegrationTests/HarnessPageAuthorizationTests.cs` — negativas
  de acesso e ausência de escrita. Esses testes não são QA visual ou E2E com o runtime.

## Guias: antes de alterar

Comece pelo índice [AGENTS.md](../AGENTS.md), identifique o dono da regra e leia o
contrato correspondente. Registre o comportamento desejado, o que não muda e
um critério observável de aceitação. Evite adicionar documentos inteiros ao prompt
quando basta apontar a fonte e carregar o trecho necessário.

Ao criar uma capacidade, defina AgentId estável, contexto mínimo, tools permitidas,
limites e casos de falha antes de escolher provider/modelo. Na API, use contratos
tipados e validação/autorização na entrada; não espalhe conferências em handlers.
Uma nova configuração com tools existentes é diferente de implementar uma tool nova.

## Sensores: depois de alterar

| Camada | Evidência exigida |
|---|---|
| Compilação e testes locais | Restore/build bem-sucedidos e TRX novo para cada projeto; nenhum projeto ausente, execução vazia ou teste obrigatório omitido |
| Segurança determinística | Testes negativos de JWT/tenant, tool não autorizada, limites, replay e indisponibilidade, conforme a alteração |
| Persistência | PostgreSQL descartável explicitamente selecionado; testes reais de lease/fencing e auditoria. Perfil sem banco não aprova esta camada |
| Console Auth | Autorização server-side; para mudanças visuais, teste de navegador e isolamento dos rascunhos por empresa |
| Qualidade do agente | Dataset anonimizado, critérios de avaliação e comparação versionada. Ainda pendente; simulação não substitui eval de modelo |
| Integração/publicação | Fluxo autenticado ponta a ponta no ambiente alvo, aprovação e rollback; não inferir de um build local |

O ponto de entrada local é `./scripts/verify.ps1`. As evidências ficam sob
`artifacts/`, fora do Git. Falhas devem retornar ao código/contrato que as causou;
não remova assertions, aumente permissões ou oculte testes para deixar verde.
O modo offline não audita vulnerabilidades. Leia sempre os limites do relatório.

Use PowerShell 7 e SDK .NET compatível com `global.json`:

```powershell
pwsh -NoProfile -File ./scripts/verify.ps1
# Sem auditoria NuGet nesta execução (não equivale a validação de segurança):
pwsh -NoProfile -File ./scripts/verify.ps1 -Offline
# Testes locais do próprio leitor/policy de TRX, sem banco ou chamadas de modelo:
pwsh -NoProfile -File ./scripts/tests/TrxVerification.Tests.ps1
```

Cada execução cria `artifacts/test-results/<runId>/summary.json` e seus próprios
TRX. O perfil local remove temporariamente a variável `HARNESS_POSTGRES_TEST`,
restaurando-a ao terminar; assim não aplica migrations por herdar uma conexão.
Somente os três testes PostgreSQL explicitamente declarados podem ser omitidos.
O resultado é `partial`, mesmo com saída zero: atende ao perfil local, mas não
valida persistência. Teste omitido inesperado, TRX ausente/vazio/inválido ou falha
de comando reprovam a verificação.

O perfil com banco exige `-RequirePostgres -AcknowledgeDisposablePostgres` e
`HARNESS_POSTGRES_TEST` configurado fora dos fontes. **Os testes aplicam migrations**:
use somente um banco descartável explicitamente autorizado. Não apontar para
produção ou um banco compartilhado. Sem os pré-requisitos, falha antes do restore.
Esse perfil não foi executado nesta entrega. Mesmo um resultado `passed` não
aprova deploy, segurança do ambiente, qualidade do LLM ou gates DBA pendentes.

## Próxima entrega funcional: console conectado

**Planejada, não implementada nesta entrega.** Reutilizar a tela no Auth e fazer
um recorte completo, com contrato e testes, antes de habilitar “Aplicar no Harness”:

1. Consultar catálogo/configuração autorizados no runtime, sem confundir exemplos
   da UI com agentes operacionais.
2. Persistir rascunho/versionamento por tenant e agente no Harness; usar controle
   de concorrência para recusar uma edição baseada em versão ultrapassada.
3. Separar salvar de publicar/ativar, com validação de tools, provider, limites,
   grants, auditoria e versão imutável usada por cada execução.
4. Validar leitura após recarregar, negativas entre tenants, atualização concorrente,
   falha de integração, replay e rollback de configuração antes da liberação.

Esse recorte exige contrato administrativo e revisão de persistência/autorização.
Não criar endpoints, migrations, grants ou chamadas pagas implicitamente durante
uma melhoria dos guias/sensores. Hangfire e produção permanecem fora desta entrega.
