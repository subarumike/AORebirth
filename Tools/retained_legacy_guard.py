"""Reject retired gameplay source and embedded game tables from the runtime graph."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ENGINE = 'AORebirth/Server/ZoneEngine_New/'

# Preserve language structure and literals, but ignore identifier renaming and formatting.
# Namespace/using prefixes are excluded so a copied type cannot evade detection by moving.
KEYWORDS = set('abstract as async await base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly record ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using var virtual void volatile while yield'.split())
LEXER = re.compile(r'//[^\n]*|/\*.*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|\b\w+\b|[^\s]', re.S)

def structural_fingerprint(source):
    body = re.search(r'\b(?:class|struct|enum|interface|record)\s+\w+', source)
    if body:
        source = source[body.start():]
    tokens = []
    for match in LEXER.finditer(source):
        token = match.group()
        if token.startswith(('//', '/*')):
            continue
        if re.fullmatch(r'[A-Za-z_]\w*', token) and token not in KEYWORDS:
            token = 'IDENTIFIER'
        tokens.append(token)
    digest = hashlib.sha256(' '.join(tokens).encode()).hexdigest()
    # Every offset makes small insertions/deletions unable to shift all comparisons.
    fragments = sorted({hashlib.sha256(' '.join(tokens[i:i+32]).encode()).hexdigest()[:24]
                        for i in range(max(0, len(tokens)-31))})
    return {'structural_sha256': digest, 'structural_fragments': fragments, 'structural_tokens': len(tokens)}
def normalized(source):
    source = re.sub(r'/\*.*?\*/|//[^\n]*', '', source, flags=re.S)
    return ' '.join(re.findall(r'\w+|[^\s\w]', source))

def inspect_source(path, source, inventory):
    failures = []
    if path in {row['relocated_path'] for row in inventory}:
        failures.append('retired inventory path')
    digest = hashlib.sha256(' '.join(re.findall(r'\w+|[^\s\w]', source)).encode()).hexdigest()
    if any(digest == row['token_sha256'] for row in inventory):
        failures.append('retired implementation fingerprint')
    shape = structural_fingerprint(source)
    fragments = set(shape['structural_fragments'])
    for row in inventory:
        if row.get('structural_tokens', 0) < 96:
            continue  # Small DTO shapes are reviewed by origin, not inferred from syntax alone.
        previous = set(row.get('structural_fragments', []))
        if shape['structural_sha256'] == row.get('structural_sha256') or (
            len(previous) >= 64 and len(previous & fragments) / len(previous) >= .85):
            failures.append('retired renamed or near-copy implementation: '+row['relocated_path'])
            break
    if re.search(r'\bnamespace\s+ZoneEngine\.Core\b', source):
        failures.append('retired gameplay namespace')
    if re.search(r'\b(?:class|struct)\s+(?:Legacy|Old|Compat)\w*(?:Gameplay|Mission|Combat|Dialogue)\w*',source):
        failures.append('gameplay compatibility type')
    if re.search(r'\b(?:class|struct)\s+Accepted\w*Catalog\b',source):
        failures.append('compiled accepted content catalog')
    # Literal initializer content; numeric protocol tuples below this threshold are separately reviewed.
    for match in re.finditer(r'\b(?:static\s+)?(?:readonly\s+)?(?:int|double|float|string)\s*\[[,\s]*\]\s+\w+\s*=\s*(?:new[^\{;]*\s*)?\{',source):
        start=match.end(); depth=1; end=start
        while depth and end<len(source):
            depth += (source[end]=='{') - (source[end]=='}'); end+=1
        literal=source[start:end-1]
        if len(re.findall(r'"[^"\n]*"|(?<!\w)-?\d+(?:\.\d+)?',literal)) >= 12:
            failures.append('compiled literal table at line '+str(source[:match.start()].count('\n')+1))
    return failures

def run(root=ROOT):
    audit=json.loads((root/'docs/reports/NEWENGINE_RETAINED_LEGACY_IMPLEMENTATION_AUDIT.json').read_text())
    inventory=audit['known_retained_files'] + [r for r in audit.get('additional_retained_or_derived_files',[]) if 'token_sha256' in r] + audit.get('retired_method_fingerprints',[])
    graph=json.loads((root/'docs/reports/NEWENGINE_CONTENT_ARCHITECTURE_GUARD.json').read_text())
    paths={row['path'] for row in graph['files'] if (root/row['path']).is_file()}
    paths.update(p.relative_to(root).as_posix() for p in (root/ENGINE).rglob('*.cs') if 'obj' not in p.parts and 'bin' not in p.parts)
    violations=[]
    for path in sorted(paths):
        source=(root/path).read_text(encoding='utf-8-sig',errors='replace')
        for reason in inspect_source(path,source,inventory):
            violations.append({'path':path,'reason':reason})
    for path in (root/ENGINE).glob('*.csproj'):
        if re.search(r'Include="[^"\n]*[/\\]ZoneEngine[/\\]',path.read_text()):
            violations.append({'path':path.relative_to(root).as_posix(),'reason':'old engine compile path'})
    return {'runtime_files_examined':len(paths),'known_retained_files':len(audit['known_retained_files']),'retired_fingerprints':len(inventory),'violations':violations,
            'limits':'Syntactic guard; historical origin and content-special-case review remain separate required audits.'}

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--write',action='store_true');args=parser.parse_args()
    report=run()
    if args.write:
        (ROOT/'docs/reports/NEWENGINE_RETAINED_LEGACY_GUARD.json').write_text(json.dumps(report,indent=2)+'\n')
    print('RETAINED_LEGACY_IMPLEMENTATION_GUARD='+('FAIL' if report['violations'] else 'PASS'))
    for issue in report['violations']:print(issue['path']+': '+issue['reason'])
    raise SystemExit(bool(report['violations']))
