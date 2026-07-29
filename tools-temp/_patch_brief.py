from pathlib import Path
import re
p = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_cap_221330_brief.py")
t = p.read_text(encoding="utf-8")
old = '''def extract_identity(text: str) -> str | None:
    m = re.search(r"\\(SimpleChar:[0-9A-Fa-f]+\\)", text or "")
    return m.group(0) if m else None
'''
new = '''def norm_id(ident: str | None) -> str:
    if not ident:
        return ""
    m = re.search(r"SimpleChar:[0-9A-Fa-f]+", ident)
    if not m:
        return ident.strip()
    return f"({m.group(0)})"


def extract_identity(text: str) -> str | None:
    m = re.search(r"SimpleChar:[0-9A-Fa-f]+", text or "")
    return f"({m.group(0)})" if m else None
'''
if old not in t:
    raise SystemExit("extract_identity block not found")
t = t.replace(old, new, 1)
repls = [
    ('sid = r.get("SourceIdentity") or ""', 'sid = norm_id(r.get("SourceIdentity") or "")'),
    ('tid = r.get("TargetIdentity") or ""', 'tid = norm_id(r.get("TargetIdentity") or "")'),
    ('src = r.get("SourceIdentity") or ""', 'src = norm_id(r.get("SourceIdentity") or "")'),
    ('tgt = r.get("TargetIdentity") or ""', 'tgt = norm_id(r.get("TargetIdentity") or "")'),
]
for a,b in repls:
    c = t.count(a)
    t = t.replace(a,b)
    print(a, "->", c)
t = t.replace(
    'ident = r.get("Identity") or ""\n        if ident not in focus_path_ids:',
    'ident = norm_id(r.get("Identity") or "")\n        if ident not in focus_path_ids:',
)
t = t.replace('if (r.get("Identity") in silver_ids)', 'if (norm_id(r.get("Identity")) in silver_ids)')
t = t.replace(
    'if r.get("Identity") in interact_ids or (r.get("Name") or "") in interact_names:\n            scfu_by_id[r.get("Identity")].append(r)',
    'nid = norm_id(r.get("Identity"))\n        if nid in interact_ids or (r.get("Name") or "") in interact_names:\n            scfu_by_id[nid].append(r)',
)
t = t.replace(
    'for idkey, role in (\n            (r.get("SourceIdentity"), r.get("SourceRole")),\n            (r.get("TargetIdentity"), r.get("TargetRole")),\n        ):',
    'for idkey, role in (\n            (norm_id(r.get("SourceIdentity")), r.get("SourceRole")),\n            (norm_id(r.get("TargetIdentity")), r.get("TargetRole")),\n        ):',
)
old3 = '''opts = opt_re.findall(line)
            # filter noise
            opts = [o for o in opts if o and o not in ("chat-protocol",)]
            lines.append(f"[{fname}] options={opts}")'''
new3 = '''opts = opt_re.findall(line)
            # filter noise
            opts = [o for o in opts if o and o not in ("chat-protocol",)]
            mt2 = re.search(r"text=(.*?) detail=", line)
            if mt2:
                opts = [x.strip() for x in mt2.group(1).split(" | ") if x.strip()]
            lines.append(f"[{fname}] options={opts}")'''
if old3 in t:
    t = t.replace(old3, new3)
    print("answerlist ok")
else:
    print("answerlist missing")
p.write_text(t, encoding="utf-8")
print("patched", p.stat().st_size)
