import pathlib
p = pathlib.Path(r"C:\xampp\htdocs\daily\data\claims\test-grants.jsonl")
lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
print("\n".join(lines[-25:]))
