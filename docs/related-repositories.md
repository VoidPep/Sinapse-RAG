# Repositórios relacionados

| Repositório | URL | Conteúdo |
|---|---|---|
| Sinapse-RAG (este) | https://github.com/VoidPep/Sinapse-RAG.git | CLI/servidor MCP em C# .NET (`sinapse`) |
| Sinapse-Obsidian | https://github.com/VoidPep/Sinapse-Obsidian.git | Plugin do Obsidian em TypeScript (id `sinapse`) — configura `sinapse.json` e chama o executável |

## Acesso

O token pessoal do GitHub que o mantenedor usa hoje (fine-grained) tem acesso de escrita aos **dois** repositórios acima: Contents e Pull requests em Read and write. O mesmo token serve para enviar branches e abrir PRs no Sinapse-Obsidian quando o plugin for criado.

O valor do token **nunca** é gravado neste repositório, na configuração do git ou em arquivos do projeto. É informado na hora do uso.

## Contrato entre os dois

O plugin depende só de:
- o formato do `sinapse.json` (ver `sinapse.example.json` e o design em `docs/superpowers/specs/2026-11-24-sinapse-design.md`);
- os comandos `sinapse index | report | --version`, com `--config`, `--json` e os exit codes `0/1/2`.

Detalhes das regras do Obsidian que motivaram os dois repositórios separados (`manifest.json` na raiz, tags iguais à versão do plugin) estão no design, seção "Abordagens consideradas".
