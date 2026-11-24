"""Generate golden outputs from the Python prototype (reference implementation).

Usage: python -I tests/fixtures/gen_golden.py <prototype_dir>

<prototype_dir> must contain build.py and query.py. They are copied (never modified)
into a temp copy of the fixture vault and run there.
"""
import json
import shutil
import sqlite3
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
QUESTIONS = [
    ("prazo de empréstimo", 6, None, None),
    ("acao rapida", 6, None, None),
    ("renovação portal bloqueio multa", 6, None, None),
    ("multa", 6, "regra", None),
    ("prazo", 6, None, "catalogo/"),
    ("renovação portal bloqueio multa", 2, None, None),
    ("horário de funcionamento", 6, None, None),
    ("zzzqqq", 6, None, None),
]


def main(proto):
    proto = Path(proto)
    # build.py leaves its db open, which blocks cleanup on Windows
    with tempfile.TemporaryDirectory(ignore_cleanup_errors=True) as t:
        vault = Path(t) / "vault"
        shutil.copytree(HERE / "vault", vault)
        rag = vault / ".rag"
        rag.mkdir()
        for name in ("build.py", "query.py"):
            shutil.copy(proto / name, rag / name)
        sys.path.insert(0, str(rag))
        import build
        import query
        build.main()

        con = sqlite3.connect(rag / "rag.db")
        out = {}
        for path, tipo, area, heading, text in con.execute(
                "SELECT path,tipo,area,heading,text FROM chunks ORDER BY id"):
            e = out.setdefault(path, {"tipo": tipo, "area": area, "chunks": [], "links": []})
            e["chunks"].append({"heading": heading, "text": text})
        for src, dst in con.execute("SELECT src,dst FROM links"):
            out[src]["links"].append(dst)
        for e in out.values():
            e["links"].sort()
        con.close()

        search = []
        for q, k, tipo, pasta in QUESTIONS:
            hits = query.search(q, k, tipo, pasta)
            search.append({"pergunta": q, "k": k, "tipo": tipo, "pasta": pasta,
                           "hits": [{"path": h["path"], "heading": h["heading"], "score": h["score"]} for h in hits]})

    gold = HERE / "golden"
    gold.mkdir(exist_ok=True)
    (gold / "chunks.json").write_text(json.dumps(dict(sorted(out.items())), ensure_ascii=False, indent=1), encoding="utf-8")
    (gold / "search.json").write_text(json.dumps(search, ensure_ascii=False, indent=1), encoding="utf-8")
    print(len(out), "notes,", len(search), "queries")


if __name__ == "__main__":
    main(sys.argv[1])
