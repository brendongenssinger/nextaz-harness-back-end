# ADR: Microsoft Agent Framework opt-in para o Cartório

- Status: aceito para implementação local e homologação com provider falso
- Data: 2026-10-14
- Escopo: `cartorio.atendimento`

## Contexto

O Harness já possui o `ExecuteHarnessRequestHandler` e o `BoundedAgentRuntime` como fronteiras de segurança e execução. Eles resolvem o agente e sua versão por tenant, calculam o fingerprint, controlam lease/fencing, limitam passos e ferramentas, revalidam a autorização de cada tool e persistem o resultado terminal. Substituir esse fluxo pelo loop interno de outro SDK duplicaria regras e poderia permitir que uma tool fosse executada fora do `ToolExecutor` do Harness.

O Cartório precisa adotar o Microsoft Agent Framework (MAF) de forma gradual, sem migrar os demais produtos e sem habilitar automaticamente todos os tenants do Cartório.

## Decisão

Adotar `Microsoft.Agents.AI` 1.24.0 como adapter de provider dentro da fronteira existente do runtime:

1. O `BoundedAgentRuntime` continua sendo o loop externo e a autoridade sobre timeout, máximo de passos, máximo de tools e fallback.
2. `MicrosoftAgentFrameworkModelProvider` cria um `ChatClientAgent` novo para cada chamada. Não há sessão, histórico ou instância compartilhada entre tenants.
3. `UseProvidedChatClientAsIs=true` impede que o SDK acrescente execução automática de funções.
4. Tools são fornecidas ao SDK como `AIFunctionDeclaration`, contendo somente nome, descrição e JSON Schema. A resposta do modelo pode propor `FunctionCallContent`, mas não executá-lo.
5. A proposta volta como `ModelExecutionResult`; somente então o `BoundedAgentRuntime` aplica contadores e o `ToolExecutor` revalida tenant, agente, configuração, permissão, risco, argumentos, recurso e timeout.
6. O bridge `ModelProviderAgentFrameworkChatClientFactory` preserva o contexto confiável do Harness ao delegar a chamada do `IChatClient` ao provider já configurado. Tenant, memória, histórico, resultados de tools, referências tipadas, instruções e limite de saída não são reconstruídos a partir de texto controlado pelo modelo.
7. O provider MAF aceita exclusivamente o agente `cartorio.atendimento`. Outros agentes falham fechados com `model_route_invalid`.

O SDK participa efetivamente de cada chamada: o adapter instancia `ChatClientAgent`, chama `RunAsync`, recebe `AgentResponse`, lê usage e converte `FunctionCallContent`. Ele não assume o loop governado, persistência, autorização nem execução das tools.

## Ativação

A ativação exige os dois valores abaixo:

```json
{
  "Harness": {
    "Cartorio": {
      "Enabled": true,
      "TenantIds": ["11111111-1111-1111-1111-111111111111"],
      "AgentFramework": {
        "Enabled": true,
        "TenantIds": ["11111111-1111-1111-1111-111111111111"]
      }
    }
  }
}
```

Os IDs acima são sintéticos. A allowlist de `AgentFramework:TenantIds` deve ser subconjunto da allowlist do Cartório; configuração divergente impede o startup. Tenants do Cartório ausentes da allowlist MAF continuam usando o provider anterior. A feature permanece desabilitada por padrão e não foi ativada em produção.

Quando simulação e OpenAI estiverem ambos configurados, o bridge segue a mesma seleção de provider/modelo do catálogo: OpenAI tem precedência. Isso evita registrar `microsoft-agent-framework` enquanto executa silenciosamente o provider de simulação.

## Consequências e limites

- Lease, fencing, fingerprint, auditoria e idempotência permanecem inalterados.
- Não há migration ou novo estado persistido.
- O pacote não cria credencial nem habilita provider pago.
- O teste HTTP usa o provider determinístico existente; ele comprova composição, isolamento e execução do SDK, não comportamento de um modelo externo.
- Uma futura sessão persistida do MAF exige decisão própria de storage e isolamento; este incremento é stateless por execução.
- Fallback continua explícito na rota e recebe o mesmo request tenant-scoped e a mesma allowlist; não amplia ferramentas.

## Evidência da dependência

- Pacote oficial: <https://www.nuget.org/packages/Microsoft.Agents.AI/1.24.0>
- Repositório e exemplos oficiais: <https://github.com/microsoft/agent-framework/tree/main/dotnet>
- Framework alvo confirmado: .NET 8.
