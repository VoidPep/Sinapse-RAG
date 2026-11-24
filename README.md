# Sinapse

Busca local (BM25) nas suas notas markdown, exposta ao Claude Code via MCP. O Sinapse indexa as pastas
que você escolher em um SQLite FTS5 e devolve só os trechos relevantes, registrando uma estimativa
da economia de tokens. É um único executável, sem runtime para instalar.

## Instalar

1. Baixe `sinapse-win-x64.exe` (Windows) ou `sinapse-linux-x64` (Linux, ex.: Fedora) na página de releases.
2. Coloque o executável no seu vault (ou onde preferir).
3. No Linux: `chmod +x sinapse-linux-x64`.

## Configurar (`sinapse.json`)

Por padrão o arquivo fica ao lado do executável; `--config <arquivo>` escolhe outro. Veja `sinapse.example.json`:

```json
{
  "name": "meu-vault",
  "base": ".",
  "folders": ["notas", "projetos"],
  "include": ["**/*.md"],
  "exclude": ["**/.obsidian/**", "**/.trash/**", "templates/**"],
  "dataDir": ".sinapse"
}
```

| Campo | Significado |
|---|---|
| `base` | Raiz usada para gerar o caminho de cada nota no índice (normalmente a raiz do vault). Caminhos relativos são resolvidos a partir da pasta do `sinapse.json`. |
| `folders` | Pastas a varrer (relativas ou absolutas). Vazio = `base` inteira. Pasta inexistente é erro de configuração. |
| `include` / `exclude` | Globs aplicados ao caminho relativo a `base`. Use `**/` para valer em qualquer nível. |
| `dataDir` | Onde ficam `index.db`, `savings.jsonl` e `savings.html`. Nunca é indexado. |

Aceita `/` e `\` nos dois sistemas. Se o arquivo não existir, `sinapse index` cria um padrão e avisa.

## Indexar

```
sinapse index
```

Reconstrói o índice (em `index.db.tmp`, depois troca por `index.db`; se o índice estiver em uso por outro processo,
o anterior é mantido e o comando falha com código 1). Rode de novo após editar notas, ou use a tool `reindexar`.

## Cadastrar no Claude Code

Windows:

```
claude mcp add sinapse_knowledge_base -s user -- "C:\caminho\para\sinapse.exe" mcp
```

Linux:

```
claude mcp add sinapse_knowledge_base -s user -- /caminho/para/sinapse mcp
```

Tools: `buscar_conhecimento(pergunta, k, tipo, pasta)`, `registrar_fallback(pergunta, manual_tokens)` e `reindexar()`.
O `tipo` vem do campo `tipo` do frontmatter da nota (padrão `outro`).

## Comandos

| Comando | Faz |
|---|---|
| `sinapse index` | Reconstrói o índice e imprime o número de arquivos e chunks. |
| `sinapse mcp` | Servidor MCP via stdio. |
| `sinapse report` | Gera `savings.html` em `dataDir` e imprime o caminho. |
| `sinapse --version` | Imprime a versão. |

Contrato para integrações (por exemplo, um plugin):

- `--config <arquivo>` é aceito por todos os comandos.
- `--json` em `index`, `report` e `--version` imprime uma linha JSON em stdout, como `{"ok":true,"files":134,"chunks":2891,"ms":850}`; em erro, `{"ok":false,"error":"..."}`.
- Códigos de saída: `0` sucesso, `1` erro de execução (inclui uso incorreto), `2` configuração inválida ou ausente.

## Economia de tokens

Cada busca via MCP acrescenta uma linha em `dataDir/savings.jsonl` (estimativa: caracteres / 4, comparando
com ler as notas inteiras). `sinapse report` transforma o log em `savings.html` (gráficos por dia e por busca;
o Chart.js é carregado de uma CDN). Para aproveitar um `savings.jsonl` já existente, copie o arquivo para `dataDir`;
o formato é o mesmo, sem conversão.

## Desenvolvimento

```
dotnet test
```

Fluxo de branches: `feat/*` → `develop` → `main`. Uma tag `vX.Y.Z` na `main` dispara o workflow de release,
que publica os executáveis para `win-x64` e `linux-x64`.

## Licença

MIT.
