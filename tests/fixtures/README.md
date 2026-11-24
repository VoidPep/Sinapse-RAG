# Fixtures

`vault/` is a synthetic mini-vault (fictional library). `golden/` holds the expected
chunks and search results produced by the original Python prototype.

Regenerate (the prototype is only read, never modified):

```
python -I tests/fixtures/gen_golden.py <prototype_dir>
```

`<prototype_dir>` must contain `build.py` and `query.py`.

Notes: `crlf.md` is stored with CRLF line endings on purpose (`.gitattributes` keeps
fixture bytes untouched). `.obsidian/ignorado.md` must never be indexed.
