"""Candidate-only method-body comparison; comments and renamed identifiers are ignored."""
import collections,io,json,re,subprocess,tarfile
from pathlib import Path
from retained_legacy_guard import KEYWORDS,LEXER
root=Path(__file__).resolve().parents[1]
def methods(source):
    tokens=[m for m in LEXER.finditer(source) if not m.group().startswith(('//','/*'))]
    for i,m in enumerate(tokens):
        if m.group()!='{' or i<2 or tokens[i-1].group()!=')':continue
        depth=1;j=i-2
        while j>=0 and depth:
            depth+=(tokens[j].group()==')')-(tokens[j].group()=='(');j-=1
        if j<0:continue
        name=tokens[j].group()
        if name in {'if','for','foreach','while','switch','catch','using','lock'}:continue
        # Access modifier or return-type declaration must be on this declaration line.
        line_start=source.rfind('\n',0,tokens[j].start())+1
        prefix=source[line_start:tokens[j].start()]
        if not re.search(r'\b(?:public|private|internal|protected|static)\b',prefix):continue
        depth=1;k=i+1
        while k<len(tokens) and depth:
            depth+=(tokens[k].group()=='{')-(tokens[k].group()=='}');k+=1
        body=[]
        for t in tokens[i:k]:
            value=t.group()
            body.append('ID' if re.fullmatch(r'[A-Za-z_]\w*',value) and value not in KEYWORDS else value)
        if len(body)<90:continue
        yield name,source.count('\n',0,m.start())+1,{tuple(body[n:n+20]) for n in range(len(body)-19)}
old={};index=collections.defaultdict(set)
raw=subprocess.check_output(['git','archive','ecefa673^','AORebirth/Server/ZoneEngine'],cwd=root)
with tarfile.open(fileobj=io.BytesIO(raw)) as tar:
    for entry in tar:
        if not entry.isfile() or not entry.name.endswith('.cs'):continue
        for name,line,fragments in methods(tar.extractfile(entry).read().decode('utf-8-sig',errors='replace')):
            key=f'{entry.name}:{line}:{name}';old[key]=fragments
            for fragment in fragments:index[fragment].add(key)
rows=[];scanned=0
for path in (root/'AORebirth/Server/ZoneEngine_New').rglob('*.cs'):
    if 'obj' in path.parts or 'bin' in path.parts:continue
    for name,line,fragments in methods(path.read_text(encoding='utf-8-sig')):
        scanned+=1;hits=collections.Counter(p for f in fragments for p in index.get(f,()))
        matches=[]
        for previous,n in hits.most_common(2):
            ratio=n/max(1,min(len(fragments),len(old[previous])))
            if n>=60 and ratio>=.85:matches.append({'old_method':previous,'common_fragments':n,'containment':round(ratio,3)})
        if matches:rows.append({'path':path.relative_to(root).as_posix(),'method':name,'line':line,'matches':matches,'classification':'REVIEW_REQUIRED'})
report={'scope':'NewEngine method bodies of at least 90 lexical tokens; candidate screen only, not a proof of independent origin.','old_methods':len(old),'current_methods':scanned,'candidates':rows}
(root/'docs/reports/NEWENGINE_LEGACY_METHOD_SCREEN.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,indent=2))
