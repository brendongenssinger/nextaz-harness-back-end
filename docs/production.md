# Preparação de produção — Harness

Estado em 02/11/2026: **candidato de release validado localmente e em PostgreSQL 15
descartável**. Commit, imagem, migration e ativação continuam etapas separadas. O
frontend `/admin/harness` no Auth continua independente desta publicação.

## Limites da release

- `HarnessComposition` exige, em Production: PostgreSQL, adapter OpenAI habilitado,
  simulação desligada e pelo menos um agente operacional. A Imobiliária só exige a
  própria integração quando essa vertical estiver habilitada.
- `GenerateEmployeeDraftHandler` ainda força `SimulationOnly: true`. O piloto de
  rascunhos não vira produção por trocar envs; demanda evolução e testes próprios.
- A migration de roteamento por tenant exige gate DBA e aplicação controlada antes
  da ativação do container.
  Não usar o script local `run-imobiliaria-local.ps1` para produção: ele força
  Development/simulação e pode aplicar migrations quando solicitado.

## 1. Montar o arquivo privado

Na raiz do checkout, copiar `.env.example` para `.env.production` apenas se este
arquivo privado ainda não existir. Ambos os formatos usam linhas `KEY=valor`.
Não colocar aspas externas, comentários inline ou multilinhas; não executar com
`source`, dot-source ou `Invoke-Expression`.

Preencher diretamente no ambiente seguro:

| Valor | Origem/decisão |
|---|---|
| Hostname do Harness | DNS e proxy HTTPS da API; confirmar antes de publicar |
| `Authentication__Authority` | Issuer público HTTPS exato do Auth |
| `ConnectionStrings__Harness` | Banco e usuário exclusivos; acesso mínimo, TLS verificado e credencial privada |
| OpenAI API key/model | Secret da conta aprovada e modelo multimodal habilitado; configurar `AllowedMediaHosts` e nunca inventar nem reaproveitar chave de outro cliente |
| Rotas de modelos | Cadastrar conexões por tenant; o token fica cifrado no PostgreSQL e a chave de dados é envolvida por uma chave RSA do Key Vault. Publicar a cadeia por agente e versão. Não existe provider global para recibos. |
| Tenant IDs | GUIDs reais de empresas autorizadas; nunca IDs de fixtures |
| API/token endpoint Imobiliária | URLs HTTPS alcançáveis a partir do container |
| `Integration__ClientSecret` | Secret do cliente **Harness → backend Imobiliária**, não o secret da direção inversa |

O pool sugerido de 5 é **por processo**, um ponto inicial conservador para revisão,
não garantia de capacidade. Somar réplicas e os pools dos outros serviços; validar
o orçamento de conexões antes de aumentar. A aplicação não deve usar superusuário.
Credenciais de migration pertencem a uma etapa separada e não ao container da API.

O exemplo deixa as flags de Imobiliária desativadas de propósito. Só alterá-las
quando essa vertical fizer parte do escopo aprovado. Cartório e employee drafts
não são ativados pelo template.

Verificação local sem rede/banco:

```powershell
pwsh -NoProfile -File scripts/test-production-env.ps1 -EnvFile .env.production
pwsh -NoProfile -File scripts/tests/ProductionEnv.Tests.ps1
```

A primeira verificação valida formato e requisitos estáticos, sem imprimir secrets.
Saída zero não comprova credenciais, existência do modelo, grants, DNS, schema,
conectividade ou segurança do ambiente. Não suba/cole o arquivo privado no chat.

## 2. Testar e preparar uma imagem identificável

Executar `scripts/verify.ps1`; o perfil local omite PostgreSQL e não aprova release.
Para testes reais, usar `-RequirePostgres -AcknowledgeDisposablePostgres` somente
com `HARNESS_POSTGRES_TEST` apontando para um banco descartável autorizado.
Nunca colocar essa variável no container de produção.

Depois de revisar/commitar seletivamente a release e aprovar o gate de persistência:

```sh
# Na raiz do checkout da release aprovada. Docker Linux necessário.
docker build --pull -t nextaz-harness:RELEASE_APROVADA .
```

O Dockerfile restaura com lock, publica somente a API, executa como UID não-root e
não copia `.env`, certificados privados ou configurações Development/Production.
Não aplica migrations. As bases .NET 8 devem ser revisadas/atualizadas; registrar
os digests resolvidos e promover a imagem por digest, sem usar `latest` para rollback.
O build Docker não foi executado nesta sessão: CLI Docker indisponível.

## 3. Sequência de publicação

1. Confirmar vertical, hostname, destino e release; revisar alterações pendentes.
2. Confirmar Auth/grants e comunicação nos dois sentidos. Para Imobiliária:
   `nextaz-imobiliaria-harness` + `imobiliaria.atendimento` chama o Harness;
   `nextaz-harness` + `imobiliaria.tools` chama o backend do produto. Secrets distintos.
3. Validar migration/backup/reversibilidade e aplicar schema em etapa autorizada.
4. Preparar diretório exclusivo `/opt/nextaz/nextaz-harness`, arquivo privado com
   permissões restritas e acesso à rede externa já existente `nextaz-network`.
5. Transferir imagem aprovada (registry ou arquivo) e o Compose, sem embutir secrets.
6. Somente após os gates, definir as variáveis **de implantação** abaixo no shell
   do destino; elas não são as envs da aplicação:

```sh
export HARNESS_IMAGE='REGISTRY/nextaz-harness@sha256:DIGEST_APROVADO'
export HARNESS_ENV_FILE='/opt/nextaz/nextaz-harness/.env.production'
export HARNESS_LOCAL_PORT='5088'
# Usar -q: imprimir compose config completo poderia expor secrets.
docker compose -f compose.production.yml config -q
docker compose -f compose.production.yml up -d --no-deps harness
```

Não usar `down`, `--remove-orphans`, prune global ou scripts que reiniciem Auth,
ERP, Worker/Hangfire ou containers de clientes. `constrotudo_api` fica fora do escopo.

O Compose publica apenas em loopback e desabilita descoberta automática pelo
Traefik. Configurar uma rota TLS explícita depois de confirmar o hostname; não
abrir a porta diretamente na internet. Compose não tem healthcheck embutido:
processo `running` não significa aplicação pronta.

## 4. Aceite e rollback

- Conferir `/health/live` e `/health/ready`: modo operacional, PostgreSQL, rotas
  publicadas para todos os agentes habilitados, conexão cifrada decodificável e
  adapter do provider registrado no processo. O readiness não executa chamada paga
  ao provider e não comprova entrega ponta a ponta das integrações.
- Conferir descoberta do Auth, emissor/audience, token M2M tenant-scoped, negativas
  entre tenants e um fluxo autenticado com leitura persistida.
- Chamadas ao modelo são pagas; combinar smoke test e limites antes de executá-las.
- Não liberar tráfego se algum gate falhar. Registrar imagem/digest/configuração
  anterior para rollback; mudar `HARNESS_IMAGE` e recriar **somente harness**.
  Compatibilidade do schema deve ser avaliada separadamente; não reverter banco
  automaticamente nem apagar execuções.

## Referências e custo

### Evidências locais desta preparação

- `scripts/tests/ProductionEnv.Tests.ps1`: 20 verificações aprovadas, sem rede.
- `test-production-env.ps1 -EnvFile .env.example`: saída 1 esperada, sem expor valores.
- `verify.ps1 -Offline -RequirePostgres -AcknowledgeDisposablePostgres`: restore e
  build aprovados; 196 testes passaram, sem falhas nem omissões, contra PostgreSQL 15.
  Relatório `artifacts/test-results/20261102T170757568Z-a58d570a/summary.json`,
  resultado `passed`. O banco e a role descartáveis foram removidos após a suíte.
- Compose analisado como YAML e checado para serviço único, loopback, filesystem
  read-only e `env_file` raw. Isso não substitui `docker compose config` ou build.
- Docker/Compose, banco real, migrations, infraestrutura e smoke pago **não executados**.

### Fontes

`env_file.format: raw` requer Docker Compose 2.30+ e preserva caracteres como `$`
sem interpolação: [documentação Docker](https://docs.docker.com/reference/compose-file/services/#format).
O container usa o padrão de publicação .NET documentado pela
[Microsoft](https://learn.microsoft.com/en-us/dotnet/core/docker/build-container).

Preparação local: R$0 de serviços novos. Custo adicional de hospedagem pode ser R$0
se houver capacidade na VPS já contratada; capacidade não foi medida. Consumo do
provider, banco e tráfego depende do ambiente/modelo e não foi estimado nem contratado.
