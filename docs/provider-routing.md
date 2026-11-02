# Roteamento de providers por tenant e agente

O Harness não escolhe o provider de um agente por variável de ambiente. Em persistência PostgreSQL, a resolução usa a chave completa:

`tenant_id + agent_id + agent_version + model_policy`

Cada agente publicado pode possuir de uma a quatro rotas ordenadas. Cada rota aponta para uma conexão pertencente ao mesmo tenant e define seu próprio modelo e timeout. Assim, um tenant pode publicar, por exemplo:

Cada cadeia de `model_policy` publicada é imutável. Políticas diferentes da mesma versão coexistem com prioridades independentes; qualquer alteração de uma cadeia exige uma nova versão do agente.

1. `openai-responses / gpt-4o-mini`;
2. `gemini / gemini-pro` como fallback;
3. `grok / modelo-x` como fallback adicional.

Uma rota só executa quando o adapter correspondente está instalado no runtime. A estrutura aceita novos providers, mas esta entrega implementa a resolução dinâmica de credenciais no adapter `openai-responses`; Gemini e Grok ainda exigem adapters próprios.

## Criptografia dos tokens

O token é gravado no PostgreSQL somente como ciphertext autenticado. Para cada conexão, o Harness gera uma chave AES-256 aleatória, cifra o token com AES-256-GCM e vincula `tenantId + providerConnectionId` como dado autenticado. A chave AES é envolvida no Azure Key Vault com RSA-OAEP-256; a chave RSA nunca deixa o cofre.

O host recebe apenas o identificador público da chave:

`Harness__ProviderEncryption__KeyVaultKeyId=https://<vault>.vault.azure.net/keys/nextaz-harness-provider-kek/<version>`

O container usa `DefaultAzureCredential`. A identidade executora precisa das operações `wrapKey` e `unwrapKey`; ela não precisa exportar a chave RSA.

Criação inicial da chave e leitura do identificador versionado:

```powershell
az keyvault key create --vault-name <vault> --name nextaz-harness-provider-kek --kty RSA --size 3072 --ops wrapKey unwrapKey
az keyvault key show --vault-name <vault> --name nextaz-harness-provider-kek --query key.kid -o tsv
```

O segundo comando fornece exatamente o valor de `Harness__ProviderEncryption__KeyVaultKeyId`. Em uma VPS sem identidade gerenciada ou Workload Identity, o arquivo privado do container também precisa fornecer uma credencial aceita por `DefaultAzureCredential`, por exemplo `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` e `AZURE_CLIENT_SECRET`.

O banco mantém `encrypted_api_key`, nonce, tag, `encrypted_data_key`, algoritmo e o identificador versionado da chave do Key Vault. Isso permite descriptografar registros antigos durante uma rotação, enquanto a versão anterior continuar habilitada.

## API administrativa

Somente uma sessão tenant-scoped com papel `HarnessAdmin`, produto e escopo do Harness pode alterar as conexões.

### Cadastrar ou rotacionar uma conexão

`PUT /api/v1/admin/tenants/{tenantId}/provider-connections/{connectionKey}`

```json
{
  "providerId": "openai-responses",
  "displayName": "OpenAI principal",
  "endpoint": "https://api.openai.com/v1/",
  "apiKey": "enviado uma vez e não retornado",
  "enabled": true
}
```

### Publicar a cadeia de rotas de uma versão do agente

`PUT /api/v1/admin/tenants/{tenantId}/agents/{agentId}/versions/{agentVersion}/model-routes`

```json
{
  "routes": [
    {
      "providerConnectionId": "00000000-0000-0000-0000-000000000000",
      "modelPolicy": "receipt-classification-v2",
      "modelId": "gpt-4o-mini",
      "timeoutSeconds": 25,
      "enabled": true
    }
  ]
}
```

A publicação é transacional e serializada por tenant, agente, versão e política. Repetir exatamente a mesma cadeia é idempotente; tentar alterar uma cadeia já publicada retorna conflito. Conexões de outro tenant ou desabilitadas são rejeitadas.

Em produção, `/health/ready` permanece indisponível enquanto qualquer combinação obrigatória não tiver rota habilitada e credencial descriptografável. Em ambiente greenfield, cadastre a conexão e a rota antes de liberar tráfego ao consumidor.
