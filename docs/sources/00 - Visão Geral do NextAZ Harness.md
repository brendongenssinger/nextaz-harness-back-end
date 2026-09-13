# 00 - Visão Geral do NextAZ Harness


## 1. Objetivo


O NextAZ Harness será a camada central de execução, orquestração e governança de Inteligência Artificial da plataforma NextAZ.


Ele não representa uma vertical específica como Cartório, Imobiliária, CRM, ERP ou Cobrança. O Harness será a infraestrutura comum de IA utilizada por essas verticais para executar agentes, selecionar modelos, aplicar políticas, controlar ferramentas, montar contexto, acessar memória e rastrear execuções.


A regra arquitetural central é:


> O modelo sugere, o Harness controla e o domínio decide.


Isso significa que decisões determinísticas e sensíveis de negócio não devem ficar sob responsabilidade de um LLM. Exemplo: confirmação de pagamento, liberação de um pedido, autorização de acesso, regras financeiras e validações críticas devem continuar sendo processadas por código e serviços de domínio.


## 2. Papel do Harness na plataforma


O Harness deve receber uma solicitação, comando ou evento e coordenar a execução de IA de ponta a ponta.


Fluxo conceitual:


1. Receber solicitação ou evento.
2. Identificar o Tenant.
3. Identificar canal, contexto e operação.
4. Resolver qual Agent deve ser utilizado.
5. Carregar configuração e políticas do Agent para aquele Tenant.
6. Montar o contexto de execução.
7. Consultar memória e conhecimento quando necessário.
8. Selecionar o modelo através do Model Router.
9. Liberar somente as Tools autorizadas.
10. Executar o Agent através do Agent Runtime.
11. Registrar rastreabilidade, custo, latência, chamadas de Tool e resultado.
12. Publicar comandos ou eventos para os serviços de domínio quando necessário.


## 3. Identidade dos Agents


Cada Agent deve possuir um identificador lógico e estável denominado AgentId.


O AgentId deve representar uma capacidade de negócio e nunca deve representar provider, modelo ou framework de IA.


Exemplos corretos:


- cartorio.atendimento
- cartorio.certidao
- cartorio.cancelamento-protesto
- imobiliaria.atendimento
- imobiliaria.qualificacao-lead
- crm.followup
- cobranca.negociacao
- prospeccao.outbound


Exemplos proibidos:


- GPTAgent
- ClaudeAgent
- GeminiAgent
- SemanticKernelAgent
- OpenAIAgent


Essa separação permite trocar provider, modelo ou framework sem alterar a identidade do Agent.


## 4. AgentId, AgentVersion e ExecutionId


Cada Agent deve possuir pelo menos os seguintes conceitos:


- AgentId: identidade lógica e estável da capacidade de negócio.
- AgentVersion: versão da configuração/comportamento do Agent.
- ExecutionId: identificador único para cada execução.


Exemplo:


AgentId: cartorio.cancelamento-protesto
AgentVersion: 1
ExecutionId: GUID gerado por execução


## 5. Configuração de Agent


A configuração de um Agent deve conter, no mínimo:


- AgentId
- Name
- Vertical
- Objective
- Status
- Version
- ModelPolicy
- MemoryPolicy
- AllowedTools
- RequiredPolicies
- RequiresTenantContext


Exemplo conceitual:


{
  "agentId": "cartorio.cancelamento-protesto",
  "vertical": "cartorio",
  "objective": "Conduzir o processo de solicitação de cancelamento de protesto",
  "allowedTools": [
    "cartorio.buscar-protesto",
    "cartorio.criar-pedido",
    "billing.gerar-link-pagamento",
    "billing.consultar-pagamento"
  ],
  "modelPolicy": "balanced",
  "memoryPolicy": "customer-context",
  "requiresTenantContext": true
}


## 6. Agent Resolver


Os módulos da NextAZ não devem depender diretamente de um Agent específico sempre que a resolução puder ser feita pelo Harness.


Exemplo: uma mensagem recebida pelo WhatsApp pode ser convertida em uma solicitação genérica ao Harness contendo TenantId, canal, operação, conteúdo e metadados.


O Agent Resolver será responsável por decidir qual Agent deve tratar aquela solicitação.


Fluxo:


Tenant -> Canal -> Contexto -> Operação/Intenção -> Agent Resolver -> AgentId


Exemplo:


Mensagem: "Quero cancelar um protesto do meu CPF."


Resultado esperado:


AgentId = cartorio.cancelamento-protesto


## 7. Tools


Agents não devem acessar bancos, APIs externas ou serviços diretamente.


Toda integração externa utilizada por um Agent deve ser exposta através de uma Tool registrada no Harness.


Exemplo para o Agent cartorio.cancelamento-protesto:


- cartorio.buscar-protesto
- cartorio.criar-pedido
- billing.gerar-link-pagamento
- billing.consultar-pagamento
- cartorio.consultar-status-pedido


Cada Tool deve possuir autorização explícita por Agent e por Tenant.


Uma Tool não autorizada nunca deve ser executada, mesmo que o modelo tente chamá-la.


## 8. Multi-Tenant


O Harness será multi-tenant.


O mesmo Agent deve poder ser reutilizado por diversos clientes, sem criação de código duplicado ou Agents exclusivos por cliente.


Exemplo recomendado:


AgentId = cartorio.cancelamento-protesto


E cada Tenant possui sua própria configuração:


TenantAgentConfiguration


- TenantId
- AgentId
- Enabled
- Instructions
- AllowedTools
- KnowledgeBaseId
- ModelPolicy
- MemoryPolicy
- Limits
- CustomSettings


Evitar criar Agents como:


- cartorio-goiania-agent
- cartorio-anapolis-agent
- cartorio-brasilia-agent


A customização deve ficar na configuração do Tenant e não na identidade do Agent.


## 9. Model Router


AgentId não representa um modelo de IA.


O Harness deve possuir uma camada chamada Model Router responsável por escolher provider e modelo em tempo de execução.


Fluxo:


Agent -> Agent Policy -> Model Router -> Provider -> Model


Exemplo:


AgentId: cartorio.atendimento
ModelPolicy: fast-cheap


Outro exemplo:


AgentId: cartorio.analise-complexa
ModelPolicy: high-reasoning


A implementação concreta poderá utilizar OpenAI, Gemini, Claude ou outros provedores.


O Agent não deve conhecer diretamente qual provider será utilizado.


## 10. Agent Runtime


O Harness deve possuir uma abstração própria de execução de Agents.


O Semantic Kernel pode ser utilizado inicialmente como implementação do Agent Runtime, mas nenhuma camada de domínio deve depender diretamente do Semantic Kernel.


A arquitetura deve permitir substituir o runtime no futuro, por exemplo por outro framework, sem reescrever os Agents ou os serviços de negócio.


Regra:


NextAZ -> interfaces próprias do Harness -> implementação do Runtime -> Semantic Kernel


Nunca:


Serviço de domínio -> Semantic Kernel diretamente


## 11. Contexto obrigatório de execução


Toda execução do Harness deve possuir, no mínimo:


- TenantId
- AgentId
- ExecutionId
- CorrelationId
- Channel
- Timestamp


Esses identificadores devem acompanhar logs, eventos, chamadas de Tools, memória e observabilidade.


O uso de contexto global ou estado compartilhado entre Tenants é proibido.


## 12. Memória e conhecimento


Memória e RAG devem ser isolados por Tenant.


O PGVector pode ser utilizado como infraestrutura de armazenamento vetorial, porém o Agent não deve consultar diretamente o banco vetorial.


O acesso deve passar por uma abstração do Harness responsável por aplicar filtros de Tenant, KnowledgeBase e Policy.


## 13. Eventos e RabbitMQ


O RabbitMQ continuará sendo o barramento de integração entre os serviços da NextAZ.


O Harness poderá consumir e publicar eventos, mas não deve assumir responsabilidade por regras determinísticas pertencentes aos serviços de domínio.


Exemplo do fluxo de cartório:


WhatsApp -> Harness -> Agent cartorio.cancelamento-protesto -> Tool gerar pedido -> Serviço Cartório


Serviço Cartório -> Serviço de Pagamento -> link de pagamento


Pagamento confirmado -> evento RabbitMQ -> Serviço Cartório libera processamento


A confirmação de pagamento não deve ser inferida pelo LLM.


## 14. Vertical Cartório como primeiro caso de uso


A vertical Cartório será um dos primeiros casos de uso completos do Harness.


Operações iniciais previstas:


- Atendimento via WhatsApp
- Pedido de certidão
- Pedido de cancelamento de protesto
- Consulta por CPF/CNPJ
- Geração de pedido
- Geração de link de pagamento
- Consulta de pagamento
- Entrega do pedido após confirmação determinística


Este caso deverá validar os principais componentes da arquitetura:


- Agent Registry
- Agent Resolver
- Agent Runtime
- Model Router
- Tool Registry
- Policies
- Multi-Tenant
- Memory/RAG
- RabbitMQ
- Observabilidade


## 15. Regras obrigatórias para implementação pelo Codex


1. Nenhum serviço de domínio pode chamar OpenAI, Gemini, Claude ou outro provider diretamente.
2. Nenhum serviço de domínio pode depender diretamente do Semantic Kernel.
3. Toda execução deve conter TenantId, AgentId, ExecutionId e CorrelationId.
4. Toda Tool deve ser validada contra as permissões do Agent e do Tenant.
5. AgentId deve representar capacidade de negócio e nunca tecnologia.
6. Regras determinísticas e críticas de negócio devem permanecer fora do LLM.
7. Memória e conhecimento devem ser isolados por Tenant.
8. O Model Router deve centralizar a seleção de providers e modelos.
9. O Agent Runtime deve ser abstraído por interfaces próprias da NextAZ.
10. Toda execução deve ser observável e auditável.


## 16. Visão arquitetural resumida


NextAZ Harness


Agent Registry
  -> Tenant Configuration
  -> Agent Resolver
  -> Agent Runtime
      -> Model Router
      -> Tool Registry
      -> Memory
      -> Policies
      -> Observability
  -> Semantic Kernel Adapter
      -> OpenAI / Gemini / Claude / outros


O Harness deve ser tratado como infraestrutura estratégica da NextAZ. Os produtos verticais utilizam o Harness, mas não devem acoplar suas regras de negócio a providers, modelos ou frameworks de IA.
