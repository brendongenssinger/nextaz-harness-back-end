# Fontes e ordem de leitura

Fonte indicada por Brendon: [NextAZ Harness no Google Drive](https://drive.google.com/drive/folders/1GxWdKrXmuctWdHh7fKU4OPOq9ZvKy_pb).

Leitura realizada nesta implementação, em 12/09/2026. Os arquivos em `sources` são capturas para rastreabilidade; o Google Drive continua sendo a fonte da arquitetura e não foi alterado.

| Ordem | Documento |
|---|---|
| 00 | [00 - Visão Geral do NextAZ Harness](https://docs.google.com/document/d/1hWsk2PsNYvmfiuwTkjwuCwxc8u12s3vA9tEyEK2qGYA/edit?usp=drivesdk) |
| 01.01 | [01.01 - Arquitetura Base do NextAZ Harness](https://docs.google.com/document/d/1cUrdPT6Im2wEvz9Xr_Pi8DZqG-lLnUcWb18gLsLVqgI/edit?usp=drivesdk) |
| 01.02 | [01.02 - Estrutura da Solution .NET e Dependências](https://docs.google.com/document/d/1uA-d66YTT5Xm3As99tbz_s1Hh4z1pqwpCdR-ezxF2I4/edit?usp=drivesdk) |
| 01.03 | [01.03 - Fluxo de Execução e Console Operacional do NextAZ Harness](https://docs.google.com/document/d/1NGRfUKuRv9o-EhWdR6h68WvCpOTCsAdObij4C8qg1Qw/edit?usp=drivesdk) |
| 02.01 | [02.01 - Produtos, Funcionários Digitais e Vertical Cartório](https://docs.google.com/document/d/1j_BJuGhYr9D5EvwrmdxTYUhMofyJhvLBTybHwySQWDg/edit?usp=drivesdk) |

01.02 detalha e prevalece sobre a estrutura física inicialmente sugerida em 01.01. A Application referencia as abstrações do Runtime; Providers/Infrastructure implementam portas. Não se criou dependência de Application com Providers ou banco.

02.01 recomenda começar com `cartorio.atendimento` e manter regras cartorárias no produto Cartório. 02.02 e 02.03 descrevem interfaces do Cartório, fora do escopo deste core.

A pasta 01 continha apenas 01.01, 01.02 e 01.03. Os documentos 01.04 a 01.14 são roadmap descrito em 01.03, e não especificações já publicadas encontradas nesta leitura.

Também foram consultados o hub Obsidian NextAZ, a arquitetura do ecossistema, o padrão de AI Engineering e a documentação local de NextAZ Auth.

Referências técnicas: [seleção de SDK/target .NET](https://learn.microsoft.com/en-us/dotnet/core/versions/selection) e [validação de JWT no ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-8.0).
