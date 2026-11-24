# Changelog

## 0.1.0

- Comandos `index`, `mcp`, `report` e `--version`, com `--config` e `--json` (contrato para o plugin).
- Indexação de markdown em SQLite FTS5 (BM25) a partir de `sinapse.json`; troca atômica do índice.
- Servidor MCP (`sinapse_knowledge_base`) com `buscar_conhecimento`, `registrar_fallback` e `reindexar`.
- Log de economia de tokens (`savings.jsonl`) e relatório `savings.html`.
- Executável único self-contained para `win-x64` e `linux-x64`.

Possível evolução: trimming/AOT. O `PublishTrimmed` com o SDK MCP ainda emite avisos de trimming, então não foi habilitado na v1.
