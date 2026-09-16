"""Offline token-shingle comparison against the pre-retirement engine tree."""
import collections
import hashlib
import io
import json
import re
import subprocess
import tarfile
from pathlib import Path
from retained_legacy_guard import KEYWORDS, LEXER

root = Path(__file__).resolve().parents[1]
def tokens(s):
    result=[]
    for match in LEXER.finditer(s):
        token=match.group()
        if token.startswith(('//','/*')):continue
        if re.fullmatch(r'[A-Za-z_]\w*',token) and token not in KEYWORDS:token='IDENTIFIER'
        result.append(token)
    return result
def shingles(s):
    t = tokens(s)
    return {tuple(t[i:i+32]) for i in range(0, len(t)-31, 1)}
raw = subprocess.check_output(['git','archive','ecefa673^','AORebirth/Server/ZoneEngine'],cwd=root)
old = {}
with tarfile.open(fileobj=io.BytesIO(raw)) as archive:
    for entry in archive.getmembers():
        if entry.isfile() and entry.name.endswith('.cs'):
            data = archive.extractfile(entry).read()
            old[entry.name] = shingles(data.decode('utf-8-sig', errors='replace'))
index = collections.defaultdict(set)
for path, fragments in old.items():
    for fragment in fragments:
        index[fragment].add(path)
graph = json.loads((root/'docs/reports/NEWENGINE_CONTENT_ARCHITECTURE_GUARD.json').read_text())
paths = {row['path'] for row in graph['files']}
paths.update(p.relative_to(root).as_posix() for p in (root/'AORebirth/Server/ZoneEngine_New').rglob('*.cs') if 'obj' not in p.parts)
review_path=root/'docs/reports/NEWENGINE_LEGACY_ORIGIN_REVIEW.json'
reviews={r['path']:r for r in json.loads(review_path.read_text())['files']} if review_path.exists() else {}
rows = []
for path in sorted(paths):
    file = root/path
    if not file.is_file(): continue
    text = file.read_text(encoding='utf-8-sig', errors='replace')
    fragments = shingles(text)
    hits = collections.Counter(p for fragment in fragments for p in index.get(fragment, ()))
    matches = []
    for previous, count in hits.most_common(3):
        containment = count / max(1, min(len(fragments), len(old[previous])))
        if count >= 64 and containment >= .65:
            matches.append({'old_path':previous, 'common_shingles':count, 'containment':round(containment,3)})
    explicit = [dict(line=i, text=line.strip()) for i,line in enumerate(text.splitlines(),1) if 'legacy' in line.lower()]
    review=reviews.get(path,{})
    reviewed=review.get('sha256')==hashlib.sha256(text.encode('utf-8')).hexdigest()
    rows.append({'path':path, 'classification':review.get('classification') if reviewed else 'UNKNOWN',
                 'review_basis':review.get('basis') if reviewed else 'Current source has no matching reviewed fingerprint.',
                 'similarity_candidates':matches, 'legacy_mentions':explicit,
                 'limit':'Lexical screen, not proof of independent authorship.'})
out = root/'docs/reports/NEWENGINE_LEGACY_ORIGIN_SCREEN.json'
out.write_text(json.dumps({'baseline':'ecefa673^', 'old_files':len(old), 'files':rows},indent=2)+'\n')
print(json.dumps({'screened':len(rows), 'old_files':len(old), 'similarity_candidates':[r['path'] for r in rows if r['similarity_candidates']],
                  'explicit_mentions':[r['path'] for r in rows if r['legacy_mentions']]}))
