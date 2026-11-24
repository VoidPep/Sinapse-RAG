# Sinapse — design (v1)

Data: 2026-11-24 · Status: aguardando aprovação · Revisado após leitura de um builder independente

## Intenção

Hoje existe um protótipo em Python (`<vault>/<pasta>/.rag`, local e não versionado) que indexa notas markdown de um vault Obsidian em SQLite FTS5 (BM25) e expõe a busca ao Claude Code via MCP (`buscar_conhecimento`), registrando a economia aproximada de tokens em `savings.jsonl`. Funciona, mas:

- está amarrado a um vault e a uma pasta (`VAULT = parent.parent`), com textos e nomes da empresa onde foi criado;
- depende de Python instalado;
- só o autor mantém.

O objetivo é transformar isso em um produto genérico e distribuível — **Sinapse** — que qualquer pessoa do time (e de fora) usa no próprio vault, escolhendo quais pastas indexar, em Windows e Fedora, sem instalar runtime. O time trabalha em C#; o Python era protótipo.

## Objetivos (v1)

1. Reescrever em C# .NET o núcleo do protótipo: indexação, busca BM25, servidor MCP, log de economia, relatório de economia.
2. Pastas a indexar e exclusões configuráveis em `sinapse.json` (nada fixo no código).
3. Nenhuma referência à empresa, ao usuário ou a caminhos locais (código, textos das tools, README).
4. Um executável único, self-contained, para `win-x64` e `linux-x64`, gerado por release no GitHub.
5. Paridade de resultados com o protótipo: mesmos chunks e mesma ordem de busca para o mesmo conteúdo.
6. Repositório público: MIT, `.gitignore`, README, branches `main` (releases), `develop` (integração) e `feat/*`.

## Não-objetivos (v1)

- Plugin do Obsidian — repositório separado (`sinapse-obsidian`, TypeScript), feito depois; v1 só define o contrato (`sinapse.json` + comandos).
- Simulador de vetores / mapa LSA (`vec.py`) — só alimentava o mapa do relatório, não a busca.
- Página de busca local com servidor HTTP (`serve.py`) e relatório de estatísticas do índice (`template.html`/`stats.json`) — ficam no protótipo; entram numa versão posterior se fizerem falta.
- Embeddings, busca híbrida, outros formatos (PDF, .docx).
- Assinatura de código do executável.
- Alterar ou apagar o protótipo Python — ele fica como está, como referência.

## Anti-objetivos (falha mesmo que "funcione")

- Publicar no repositório qualquer índice, log ou conteúdo de vault (`*.db`, `savings.jsonl`, HTML gerado).
- Busca pior que o protótipo (trechos diferentes ou fora de ordem sem justificativa).
- Executável que exige .NET instalado ou que demora segundos para abrir (o Claude e o plugin abrem o processo com frequência).
- Arquitetura em camadas/interfaces sem segunda implementação.

## Restrições

- .NET SDK 11 instalado na máquina; **alvo `net10.0`** (LTS, suporte até nov/2028).
- Distribuição: `dotnet publish -r <rid> --self-contained -p:PublishSingleFile=true -p:InvariantGlobalization=true`. AOT é opcional, decidido pelo builder se o SDK MCP for compatível com trimming.
- SQLite via `Microsoft.Data.Sqlite` (bundle `e_sqlite3`, que inclui FTS5). Verificar `fts5` no primeiro teste.
- MCP via SDK oficial `ModelContextProtocol` (NuGet, linha 2.x estável). Fixar a versão exata consultando o NuGet; transporte stdio.
- stdout é o canal do protocolo MCP: nada de log em stdout no comando `mcp` (logs em stderr).
- `gh` CLI não está instalado; criação do repositório remoto e release são passos manuais/CI, fora do escopo de código.

## Abordagens consideradas

| Abordagem | Trade-off | Decisão |
|---|---|---|
| Manter Python + PyInstaller | Zero reescrita; exe 40–60 MB, falso positivo de antivírus, ~1–3 s de start (onefile extrai a cada execução), build por SO, só o autor mantém | Rejeitada |
| **C# .NET, single-file self-contained** | Reescrever ~400 linhas (~2–4 dias); exe 15–30 MB, start ~50–100 ms, cross-publish linux a partir do Windows, time inteiro mantém, SDK MCP oficial | **Escolhida** |
| Arquitetura Clean/Onion/Microkernel | Camadas e interfaces sem segunda implementação; o "domínio" é só Chunk/SearchResult e a lógica é o SQL do BM25 | Rejeitada |
| **Núcleo + entradas finas** (ideia de entrada da hexagonal, sem portas do lado do banco) | Simples de ler; extrai interface quando surgir 2ª implementação (ex.: `IChunker` para PDF) | **Escolhida** |
| Monorepo .NET + plugin | Obsidian exige `manifest.json` na raiz e tags = versão do plugin; colide com o .sln e as tags do exe | Rejeitada → **dois repositórios** |

## Estrutura

```
sinapse/
  Sinapse.sln
  src/Sinapse.Core/        class library, não conhece MCP/console
    Config.cs              lê sinapse.json
    Chunker.cs             markdown → chunks (port de frontmatter/sections/split)
    Indexer.cs             varre pastas → SQLite (chunks, fts, links)
    Search.cs              consulta BM25
    SavingsLog.cs          savings.jsonl + fallback
    SavingsReport.cs       savings.jsonl → savings.html
  src/Sinapse/             console exe
    Program.cs             roteia: index | mcp | report | --version
    McpTools.cs            buscar_conhecimento, registrar_fallback, reindexar
  tests/Sinapse.Tests/     xUnit; SQLite real em pasta temporária
  sinapse.example.json
  .github/workflows/ci.yml       build + testes em push/PR (develop e main)
  .github/workflows/release.yml  tag vX.Y.Z → sinapse-win-x64.exe, sinapse-linux-x64
  docs/superpowers/specs/
  README.md  LICENSE (MIT)  .gitignore  CHANGELOG.md
```

Classes concretas; injeção de dependência só onde o host do SDK MCP exige.

## Contrato: `sinapse.json`

Localização: por padrão, ao lado do executável (o usuário coloca o exe no vault). `--config <arquivo>` sobrescreve. Caminhos relativos são resolvidos a partir da pasta do `sinapse.json`.

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

- `base`: raiz usada para gerar o caminho de cada nota no índice (normalmente a raiz do vault). Caminho armazenado = relativo a `base` com `/`; se a nota estiver fora de `base`, caminho absoluto.
- `folders`: pastas a varrer (relativas ou absolutas); vazio = `base` inteira.
- `include`/`exclude`: globs, aplicados ao caminho relativo a `base`. Use `**/` para pegar em qualquer nível (o protótipo excluía `.obsidian` em qualquer nível). *(decisão do autor do design)*
- `dataDir` é sempre excluído da indexação, independente de `exclude`. *(decisão do autor do design)*
- `dataDir`: onde ficam `index.db` e `savings.jsonl`.
- Arquivo ausente: `sinapse index` cria um `sinapse.json` padrão (`base: "."`, `folders: []`) e avisa.
- Caminhos aceitam `/` e `\` nos dois SOs.

O plugin do Obsidian lê/escreve esse arquivo e chama o executável; mudanças de formato exigem versão nova (`"version": 1` no arquivo é decisão do builder).

## Comandos

| Comando | Faz |
|---|---|
| `sinapse index` | Reconstrói o índice. Imprime nº de arquivos/chunks. |
| `sinapse mcp` | Servidor MCP stdio. |
| `sinapse report` | Gera `savings.html` em `dataDir` a partir de `savings.jsonl`. Imprime o caminho do arquivo. |
| `sinapse --version` | Versão (vinda da tag/`<Version>` do csproj). |

Contrato para o plugin *(decisão do autor do design — barato agora, quebraria compatibilidade depois)*:
- `--config <arquivo>` aceito por todos os comandos.
- `--json` em `index`, `report` e `--version`: uma linha JSON em stdout (ex.: `{"ok":true,"files":134,"chunks":2891,"ms":850}`; erro: `{"ok":false,"error":"..."}`).
- Exit codes: `0` sucesso, `1` erro de execução, `2` configuração inválida/ausente.

Reconstrução do índice *(desvio deliberado do protótipo)*: vários processos podem ter o banco aberto (várias sessões do Claude com `sinapse mcp`, mais o plugin). Apagar um arquivo SQLite aberto falha no Windows. Por isso: construir em `index.db.tmp` e trocar com `File.Move(..., overwrite: true)`, com algumas tentativas curtas se o arquivo estiver em uso; conexões de busca com `Pooling=False` para não segurar o arquivo entre consultas. Falha na troca mantém o índice anterior e retorna exit code `1`.

Cadastro no Claude Code (README): `claude mcp add sinapse_knowledge_base -s user -- <caminho>/sinapse mcp`.

## Comportamento a portar (paridade com o protótipo)

Referência: `build.py`, `query.py`, `kb_mcp.py`, `gen_savings.py` do protótipo.

**Chunking** (`build.py`):
- Normalizar `\r\n` → `\n` antes de tudo (o Python faz isso implicitamente ao ler texto).
- Frontmatter: `\A---\n(.*?)\n---\n`, linhas `k: v` (split no primeiro `:`). `tipo` (padrão `"outro"`) e `area` (padrão `""`).
- Seções: headings `^(#{1,3}) +(.*)$` (multiline); heading path `"H1 > H2 > H3"`; texto antes do 1º heading vira seção com heading `""`.
- Junta seções consecutivas enquanto `len(acumulado) + len(nova) < 1200`, mantendo o heading da primeira.
- Split por parágrafo (`\n\s*\n`), empacota até 1200 chars (+2 do separador), corta à força parágrafos > 1200.
- Descarta pedaços com < 30 chars.
- Links `[[alvo]]`/`[[alvo|x]]`/`[[alvo#x]]` → tabela `links(src, dst)` com o último segmento de `alvo`.
- Comprimento em chars: o Python conta code points, C# `string.Length` conta UTF-16. Diferença só em caracteres fora do BMP (emoji); aceitável, documentar no teste.

**Schema** idêntico ao protótipo: `chunks(id, path, title, tipo, area, heading, text)`, `fts` FTS5 external-content com `tokenize='unicode61 remove_diacritics 2'`, `links`, índices `ix_links_dst`, `ix_chunks_path`.

**Ordem determinística** *(decisão do autor do design)*: arquivos são indexados em ordem ordinal do caminho relativo (o protótipo usa `rglob`, sem ordem garantida), e a busca desempata por `ORDER BY s, c.id`.

**Busca** (`query.py`): termos = `\w{3,}`; consulta `"t1" OR "t2" …`; `bm25(fts,5,3,1)`; filtros opcionais `tipo` (igualdade) e `pasta` (prefixo de path); `k` padrão 6, máximo 20; score = `-bm25` arredondado a 2 casas. Conexão aberta e fechada por consulta (o `index` precisa poder recriar o arquivo).

**MCP** (`kb_mcp.py`): server name `sinapse_knowledge_base`. Tools com os **mesmos nomes e parâmetros** (`buscar_conhecimento`, `registrar_fallback`, `reindexar`) para não quebrar instruções existentes; descrições genéricas, sem citar empresa/vault ("base de conhecimento das notas configuradas"; para ler a nota inteira, usar o caminho retornado). Formato do texto de resposta igual ao protótipo. Erros de tool viram `isError`, sem derrubar o servidor.

**Log de economia**: mesmo formato de linha do `savings.jsonl` atual (`ts, pergunta, notas, returned_tokens, manual_tokens, saved_tokens`, e `fallback` opcional — linhas antigas não o têm), de modo que o histórico do protótipo pode ser copiado para `dataDir` sem conversão (documentar no README; sem código de migração), chars/4, falha de log nunca quebra a busca. `registrar_fallback` corrige a última linha com a mesma pergunta.

**Relatório**: `savings.html` equivalente ao `gen_savings.py` (por dia + acumulado, por busca). Chart.js via CDN com versão fixa.

## Testes

- Unidade (xUnit) para `Chunker` (frontmatter, CRLF, headings, junção, split, < 30 chars, links), `Config` (relativo/absoluto, separadores, globs), `SavingsLog` (append, fallback).
- Integração com SQLite real em pasta temporária: `index` + `search` sobre um mini-vault de fixture; verifica FTS5 disponível.
- **Paridade (golden):** fixture de notas + saída esperada gerada pelo protótipo Python (chunks e top-k de um conjunto de perguntas) versionada em `tests/fixtures`. O conteúdo das fixtures é sintético — nunca notas reais do vault. A saída esperada é gerada por um script pequeno (`tests/fixtures/gen_golden.py`) que importa as funções do protótipo (`frontmatter`, `sections`, `split`, `search`) e alimenta os arquivos em ordem ordinal, sem alterar o protótipo. Na comparação da busca, resultados com score igual formam um grupo sem ordem; scores comparados com tolerância de 0,01.
- Smoke do MCP: iniciar `sinapse mcp`, `initialize` + `tools/list` + `tools/call buscar_conhecimento`.
- CI roda em `windows-latest` e `ubuntu-latest`.

## Repositório e release

- `.gitignore`: `bin/ obj/ *.db *.db-* savings.jsonl *.html` gerados em `dataDir`, `.sinapse/`, `sinapse.json` (só `sinapse.example.json` é versionado), `.vs/ .idea/ *.user`, `TestResults/`.
- Branches: `feat/*` → PR → `develop` → `main`; release por tag `vX.Y.Z` na `main`.
- Release: `dotnet publish` win-x64 e linux-x64 (single-file, self-contained, invariant globalization) e anexa os binários + `sinapse.example.json`. Binário linux compilado em `ubuntu-22.04` (glibc compatível com Fedora).
- README genérico: o que é, instalar (Windows/Fedora), configurar `sinapse.json`, cadastrar no Claude Code, comandos, como ler o relatório. Nenhum usuário/caminho real.
- LICENSE MIT, titular: o autor (nome a confirmar no momento de criar o arquivo).

## Deixado para o builder

- Versão exata dos pacotes NuGet (MCP, Sqlite, xUnit) e da action do GitHub.
- Se dá para usar AOT/trimming com o SDK MCP; se não, single-file sem AOT.
- Biblioteca de glob (`Microsoft.Extensions.FileSystemGlobbing` sugerida) e de parse de argumentos (pode ser `switch` no `args`).
- Template do `savings.html` como recurso embutido.

## Consequência fora do repositório

Ao trocar o servidor MCP no Claude Code de o nome antigo do protótipo para `sinapse_knowledge_base`, atualizar as referências em `~/.claude/AGENT_WORKFLOW.md` (as tools mantêm o nome; muda o prefixo `mcp__…`). Feito manualmente na troca, não neste repositório.
