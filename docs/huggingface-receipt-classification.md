# Classificação de recibos com Hugging Face

Modelo privado aprovado para integração:
`brendonmascarenhas/train-identify-recibos`.

## Responsabilidades

1. O Funcionário Digital envia ao Harness uma URL HTTPS temporária da mídia.
2. O Harness valida tenant, cliente M2M, operação e host de mídia autorizado.
3. O provider baixa no máximo 10 MiB, sem redirects, e aceita apenas JPEG, PNG ou WebP.
4. O modelo do Hugging Face devolve rótulo e confiança.
5. Só uma classificação positiva acima do limiar habilita a extração detalhada já existente.
6. O Funcionário Digital continua responsável por validar valor, cliente, itens, idempotência e
   confirmação. O classificador nunca confirma pagamento ou venda.

Rota autenticada: `POST /api/v1/receipt-classifications`.

Operação auditada: `funcionario-digital.recibo`.

Escopo M2M: `receipts.classify`, produto `funcionario-digital`, role `Service`; `sub`, `client_id`
e `azp` devem identificar o mesmo cliente explicitamente permitido.

## Segredo

O cofre existente é `nextaz-kv-prod-70bec273`. O nome canônico do novo segredo é
`harness-huggingface-api-key`. O valor deve ser incluído diretamente no Azure Portal ou em um
terminal local seguro; não colar o token em chat, issue, commit ou log.

Depois de criado, sincronize-o para o arquivo privado de produção sem imprimir o valor:

```powershell
pwsh -NoProfile -File scripts/sync-huggingface-secret-from-keyvault.ps1 `
  -EnvFile C:\caminho-privado\.env.production
```

Para validar somente o modelo com uma imagem local, sem iniciar o Harness:

```powershell
pwsh -NoProfile -File scripts/setup-huggingface-receipt-local.ps1
pwsh -NoProfile -File scripts/test-huggingface-receipt-model.ps1 `
  -ImagePath C:\caminho\recibo.jpg
```

O comando lê por padrão o segredo `harness-huggingface-api-key` do Key Vault. Para um teste
descartável sem salvar o token, acrescente `-PromptForToken`; o valor será solicitado de forma
oculta e existirá somente na memória do processo.

O arquivo privado é então entregue ao container por `env_file`. A VPS não recebe credencial ampla
do Azure; apenas o segredo específico materializado no ambiente protegido. Se o deploy for
automatizado, preferir GitHub OIDC com permissão de leitura somente nesse segredo.

## Configuração ainda obrigatória

- `Harness__Providers__HuggingFace__ReceiptLabels__0=recibo`: rótulo exato confirmado no
  `id2label` do `config.json` privado; a classe oposta é `nao_recibo`.
- `Harness__Providers__HuggingFace__MinimumScore`: começa em `0.90`, sujeito a calibração com
  falsos positivos e falsos negativos reais.
- `Harness__Providers__HuggingFace__AllowedMediaHosts__0`: atualmente
  `nextazstorage.blob.core.windows.net`.
- `Harness__Receipts__TenantIds__0`: tenant explicitamente autorizado.
- `Authentication__ReceiptClassifierClientIds__0`: cliente M2M do Funcionário Digital.

O provider aceita a resposta oficial de classificação de imagem do Hugging Face como uma lista de
objetos `{ label, score }`. O custo estimado por chamada é configurável e entra no relatório do
Harness; deixar vazio significa custo desconhecido, não custo zero.

O provider serverless `hf-inference` recusou este modelo personalizado com `Model not supported by
provider hf-inference`. Por isso, a configuração do endpoint permanece vazia por padrão e não deve
ser ativada com a URL do router serverless. Produção exige um Inference Endpoint dedicado ou um
serviço HTTPS próprio que hospede os pesos e preserve o mesmo contrato. A escolha depende de medir
memória/latência/custo; o teste local não autoriza hospedar o modelo na VPS de 2 GB.

## Estado de validação

- Build e testes locais aprovam contrato, isolamento, allowlist de mídia, limiar e custo estimado.
- O segredo foi lido do Key Vault sem exibir o valor e abriu o repositório privado.
- Labels confirmados: `nao_recibo` e `recibo`.
- Inferência CPU local real: captura de tela negativa classificada como `nao_recibo` com 98,23%.
- O provider serverless foi testado e comprovadamente não suporta este modelo.
- Nenhum deploy, commit ou integração no Funcionário Digital foi feita.
