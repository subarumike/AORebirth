"""Offline ACGEntrance evidence reconstruction; source files are opened read-only."""
from __future__ import annotations

import argparse
import ctypes
import hashlib
import json
import pathlib
import re
import struct
import subprocess
import sys
import math
import shutil
import os
import gzip
import base64
from collections import Counter

from export_pf1931_dungeon_geometry import RdbReader

ROOT = pathlib.Path(__file__).resolve().parents[1]
CLIENT = pathlib.Path(r"C:\Users\Mike\Documents\AO stripdown\Anarchy Online")
DB = CLIENT / "cd_image/data/db"
PRIOR = pathlib.Path(r"C:\temp\ao-ghidra-pf-instance-20260905")
OUT = ROOT / "docs/generated/missions/acgentrance-reconstruction"
CATALOG = ROOT / "docs/reference/missions/external-location-catalog/ACGEntrances.json"
VERSION = "acgentrance-reconstruction-v1"
EXPECTED = {
    "Gamecode.dll": ("654969a6b65946cb161f0e60aed8589260fc5eca1795488f66bb56f8fff73726", ""),
    "N3.dll": ("8c019efd72d547879a06585b69147ab1546b9617a2fce090e5863791aec8b0bb", ""),
    "GameData.dll": ("7b7d4a44a9bcbbd771507332e3641bbfaf0f80f2a4ff2335c6757f6653f870e3", ""),
    "ResourceDatabase.dat": ("3cabdede7b9b2468ed22f10f536fb2f7083ea05ed9483e2d96b22cf080d736a6", ""),
    "ResourceDatabase.dat.001": ("f8884a2c382ce7c95f20b4423567f176ed40675ba9ce8362527288712871ba73", ""),
    "ResourceDatabase.dat.002": ("2024021f966c3c8a8c083e01cbad2335ba33c19a1661a148060391755a608cc1", ""),
    "ResourceDatabase.idx": ("ba152f59096d5358f4d1b6511d3a3d264999e0a59f1ab7bf3a7cc18a4888c273", ""),
}


def sha(path):
    with pathlib.Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write_json(path, value):
    path = pathlib.Path(path).resolve()
    if not path.is_relative_to(ROOT):
        raise ValueError("Output must stay inside this dedicated AORebirth worktree")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=True, sort_keys=True) + "\n", encoding="utf-8", newline="\n")


def version_info(path):
    lib = ctypes.windll.version
    size = lib.GetFileVersionInfoSizeW(str(path), None)
    if not size:
        return None
    buf = ctypes.create_string_buffer(size)
    if not lib.GetFileVersionInfoW(str(path), 0, size, buf):
        raise ValueError("Version resource read failed")
    ptr, length = ctypes.c_void_p(), ctypes.c_uint()
    if not lib.VerQueryValueW(buf, "\\", ctypes.byref(ptr), ctypes.byref(length)):
        return None
    words = ctypes.cast(ptr, ctypes.POINTER(ctypes.c_uint32))
    def ver(i):
        return ".".join(map(str, (words[i] >> 16, words[i] & 65535, words[i+1] >> 16, words[i+1] & 65535)))
    return {"file_version": ver(2), "product_version": ver(4)}


class PE:
    def __init__(self, path):
        self.path = pathlib.Path(path)
        self.data = self.path.read_bytes()
        self.pe = struct.unpack_from("<I", self.data, 60)[0]
        if self.data[self.pe:self.pe+4] != b"PE\0\0":
            raise ValueError("Not PE")
        self.machine, count = struct.unpack_from("<HH", self.data, self.pe+4)
        opt_size = struct.unpack_from("<H", self.data, self.pe+20)[0]
        opt = self.pe+24
        if struct.unpack_from("<H", self.data, opt)[0] != 0x10b:
            raise ValueError("Expected PE32")
        self.base = struct.unpack_from("<I", self.data, opt+28)[0]
        self.directories = [struct.unpack_from("<II", self.data, opt+96+8*i) for i in range(16)]
        self.sections = []
        for i in range(count):
            off = opt+opt_size+40*i
            name = self.data[off:off+8].split(b"\0")[0].decode("ascii")
            vs, va, rs, rp = struct.unpack_from("<IIII", self.data, off+8)
            flags = struct.unpack_from("<I", self.data, off+36)[0]
            self.sections.append((name, va, vs, rp, rs, flags))

    def offset(self, rva):
        for _, va, vs, rp, rs, _ in self.sections:
            if va <= rva < va + max(vs, rs):
                if rva-va >= rs:
                    raise ValueError("RVA in unbacked region")
                return rp+rva-va
        raise ValueError(f"RVA outside sections: {rva:x}")

    def string(self, rva):
        off = self.offset(rva)
        return self.data[off:self.data.index(b"\0", off)].decode("ascii")

    def exports(self):
        rva, size = self.directories[0]
        if not rva:
            return []
        off = self.offset(rva)
        base, nfunc, nname, funcs, names, ords = struct.unpack_from("<IIIIII", self.data, off+16)
        result = []
        for i in range(nname):
            name_rva = struct.unpack_from("<I", self.data, self.offset(names)+4*i)[0]
            ordinal = struct.unpack_from("<H", self.data, self.offset(ords)+2*i)[0]
            addr = struct.unpack_from("<I", self.data, self.offset(funcs)+4*ordinal)[0]
            result.append({"name": self.string(name_rva), "rva": f"0x{addr:08x}", "ordinal": base+ordinal})
        return result

    def imports(self):
        rva, size = self.directories[1]
        result = []
        if not rva:
            return result
        off = self.offset(rva)
        while True:
            orig, timestamp, chain, name, thunk = struct.unpack_from("<IIIII", self.data, off)
            if not any((orig, timestamp, chain, name, thunk)):
                break
            lib = self.string(name)
            table = self.offset(orig or thunk)
            i = 0
            while (entry := struct.unpack_from("<I", self.data, table+4*i)[0]):
                symbol = f"ordinal:{entry & 65535}" if entry & 0x80000000 else self.string(entry+2)
                result.append({"module": lib, "name": symbol, "iat_rva": thunk+4*i})
                i += 1
            off += 20
        return result


def source_paths():
    return [CLIENT / n for n in ("Gamecode.dll", "N3.dll", "GameData.dll", "version.id")] + sorted(DB.glob("ResourceDatabase.*"))


def sources(verify=False):
    manifest = OUT / "acgentrance-source-manifest.json"
    before = json.loads(manifest.read_text()) if manifest.exists() else None
    result = []
    for path in source_paths():
        row = {"path": str(path), "filename": path.name, "size": path.stat().st_size, "sha256": sha(path)}
        prefix, suffix = EXPECTED.get(path.name, ("", ""))
        row["prior_fingerprint_match"] = row["sha256"].startswith(prefix) and row["sha256"].endswith(suffix)
        if path.suffix.lower() == ".dll":
            pe = PE(path)
            row.update(version_info(path) or {'file_version':None, 'product_version':None})
            row.update(architecture="x86" if pe.machine == 0x14c else hex(pe.machine), image_base=f"0x{pe.base:08x}")
            row.update(ghidra_project=f'tools-temp/acgentrance-analysis/ghidra/AcgFresh{path.name.replace(".", "")}.gpr',
                ghidra_program=path.name, analysis_status='FRESH_PRIVATE_IMPORT_GHIDRA_12.1.3_TARGETED_FUNCTIONS_EXPORTED')
        else:
            row.update(architecture=None, image_base=None, file_version=None, product_version=None)
            row.update(ghidra_project=None, ghidra_program=None, analysis_status='BUILD_LABEL' if path.name=='version.id' else 'INDEXED_RESOURCE_INPUT')
        result.append(row)
    if before:
        current = {r['filename']:r for r in result}
        for old in before['inputs']:
            if old['sha256']!=current[old['filename']]['sha256']:
                raise ValueError('SOURCE_INPUT_DRIFT; cannot replace original baseline')
    data = {"extractor_version": VERSION, "inputs": result, "external_catalog": {"path": str(CATALOG.relative_to(ROOT)), "sha256": sha(CATALOG)}, "client_version_label": (CLIENT/'version.id').read_text().strip(), "command": "Tools\\acgentrance_reconstruction.cmd sources",
        "original_main_input_baseline_preserved": True, "version_resource_note": "PE versions absent; version.id provides client build label, not a fabricated DLL version."}
    if verify:
        if before != data:
            raise ValueError("SOURCE_INPUT_DRIFT")
        print("SOURCE_INPUTS_BYTE_IDENTICAL=PASS")
    else:
        write_json(manifest, data)
        print('SOURCE_MANIFEST=PASS; original binary/database fingerprints unchanged')
    if not all(r["prior_fingerprint_match"] for r in result):
        raise ValueError("BINARY_FINGERPRINT_MISMATCH: do not carry RVAs forward")


def inspect(args):
    aliases = {"playfield-parser": ROOT / "Tools/Algorithman/Extractor Serializer/PlayfieldParser.cs", "statel-parser": ROOT / "Tools/Algorithman/Extractor Serializer/Structs/StatelDataExtractor.cs", "flat-event": ROOT / "Tools/Algorithman/Extractor Serializer/Structs/HLFlatEvent.cs"}
    aliases['new-parser'] = ROOT / 'Tools/Algorithman/Extractor Serializer/NewParser.cs'
    aliases['pfcoord'] = ROOT / 'Tools/Algorithman/Extractor Serializer/Structs/PFCoordHeading.cs'
    path = aliases.get(args.path, CLIENT / args.path[7:] if args.path.startswith("client:") else pathlib.Path(args.path))
    if args.hex:
        if args.path.startswith('rdb:'):
            key = tuple(map(int, args.path.split(':')[1:]))
            data = RdbReader(DB).get(*key)[args.start:args.start+args.length]
        else:
            with path.open("rb") as stream:
                stream.seek(args.start)
                data = stream.read(args.length)
        for i in range(0, len(data), 16):
            row = data[i:i+16]
            print(f"{args.start+i:08x}  {row.hex(' '):47}  {''.join(chr(v) if 32 <= v < 127 else '.' for v in row)}")
    else:
        with path.open(encoding="utf-8-sig", errors="replace") as stream:
            for n, line in enumerate(stream, 1):
                if args.start <= n < args.start+args.length:
                    print(f"{n}: {line.rstrip()}")
                if n >= args.start+args.length:
                    break


def structure():
    reader = RdbReader(DB)
    anchor = 0x111F0E0A
    prior = sorted((pos, key) for key, pos in reader.records.items() if pos <= anchor)[-1]
    pos, key = prior
    payload = reader.get(*key)
    print(json.dumps({"parent": key, "parent_header_offset": hex(pos), "payload_offset": hex(pos+34), "anchor_relative": anchor-pos-34, "payload_length": len(payload), "same_type_count": sum(k[0] == key[0] for k in reader.records)}))
    for start, count in [(0, 96), (anchor-pos-34, 370)]:
        for i in range(start, min(start+count, len(payload)), 16):
            row = payload[i:i+16]
            print(f"{i:08x}  {row.hex(' '):47}  {''.join(chr(v) if 32 <= v < 127 else '.' for v in row)}")
    for path in sorted(PRIOR.glob('*')):
        if path.is_file():
            print("PRIOR_EVIDENCE", path.name, path.stat().st_size)
    for name in ("Gamecode.dll", "N3.dll", "GameData.dll"):
        pe = PE(CLIENT / name)
        names = [e for e in pe.exports() if any(s in e['name'] for s in ('Entrance', 'RDBDynel', 'Statel', 'Quest', 'Mission', 'Door_t', 'GetStat'))]
        write_json(OUT / (name + "-relevant-symbols.json"), {"sha256": sha(CLIENT/name), "exports": names, "imports": [e for e in pe.imports() if any(s in e['name'] for s in ('Entrance', 'Quest', 'Mission', 'GetStat'))]})
        print(name, len(names), "relevant symbols retained")
    for key in sorted(reader.records):
        if key[1] == 41561:
            payload = reader.get(*key)
            print("TEMPLATE", key, len(payload), payload[:100].hex(' '))


def parse_content(data, template=False):
    """Native little-endian templates and big-endian placement overrides.

    Stat/name and event/function grammar follows the existing Algorithman
    NewParser, HLFlatEvent, HLFlatFunction and FunctionSets.cfg parsers.
    Unknown sections reject interpretation without discarding the raw placement.
    """
    endian = '<' if template else '>'
    if template:
        if len(data)<8 or struct.unpack_from('<I',data)[0]!=0xdac6:
            raise ValueError('Unexpected ACGEntrance template header')
    elif len(data)<16 or struct.unpack_from('>III',data) not in ((102,103,1009),):
        raise ValueError('Unexpected placement content header')
    cursor = 4 if template else 12
    def read(fmt):
        nonlocal cursor
        size = struct.calcsize(endian+fmt)
        if cursor+size > len(data):
            raise ValueError('Truncated content section')
        values = struct.unpack_from(endian+fmt, data, cursor)
        cursor += size
        return values[0] if len(values) == 1 else values
    def count3():
        value = read('I')
        if not value or value % 1009:
            raise ValueError('Invalid content 3F1 count')
        count = value//1009-1
        if count > len(data)//4:
            raise ValueError('Content count exceeds payload')
        return count
    sections = read('I')
    if sections > len(data)//4:
        raise ValueError('Content section count exceeds payload')
    result = {'stat_pairs': [], 'name': None, 'name_raw_hex': None, 'name_offset': None, 'events': [], 'sections': [], 'encoding': 'byte-preserving Latin-1; raw bytes authoritative'}
    for _ in range(sections):
        kind = read('I')
        result['sections'].append(kind)
        if kind == 15:
            subtype = read('I')
            if subtype not in (23, 24):
                raise ValueError(f'Unsupported stat subtype {subtype}')
            for _ in range(count3()):
                result['stat_pairs'].append(list(read('Ii')))
        elif kind == 21:
            if read('I') != 33:
                raise ValueError('Unexpected name subtype')
            nname, ndesc = read('HH')
            if cursor+nname+ndesc > len(data):
                raise ValueError('Truncated content name')
            raw = data[cursor:cursor+nname]
            result.update(name_raw_hex=raw.hex(), name=raw.decode('latin-1'), name_offset=cursor, description_raw_hex=data[cursor+nname:cursor+nname+ndesc].hex())
            cursor += nname+ndesc
        elif kind == 2:
            event = {'event_type': read('I'), 'functions': []}
            for _ in range(count3()):
                function_type = read('I')
                reserved = read('II')
                reqcount = read('I')
                if reqcount > len(data)//12:
                    raise ValueError('Invalid function requirement count')
                reqs = [list(read('iii')) for _ in range(reqcount)]
                tick_count, tick_interval, target, unknown = read('IIII')
                # Existing FunctionSets.cfg entry 53082=4n,8x.
                if function_type != 53082:
                    raise ValueError(f'Unsupported content function {function_type}')
                args = list(read('IIII'))
                tail = list(read('II'))
                event['functions'].append({'function_type': function_type, 'arguments': args, 'reserved': reserved, 'requirements': reqs, 'tick_count': tick_count, 'tick_interval': tick_interval, 'target': target, 'unknown': unknown, 'tail': tail})
            result['events'].append(event)
        elif kind in (20, 22):
            subtype = read('I')
            if subtype != (5 if kind == 20 else 36):
                raise ValueError(f'Unexpected section {kind} subtype {subtype}')
            entries = []
            for _ in range(count3()):
                action = read('I')
                values = [read('I') if kind == 20 else list(read('iii')) for _ in range(count3())]
                entries.append({'action': action, 'values': values})
            result['extra_' + str(kind)] = entries
        else:
            raise ValueError(f'Unsupported content section {kind}')
    result['trailing_raw_hex'] = data[cursor:].hex()
    if data[cursor:] not in (b'', b'\0\0\0\0'):
        raise ValueError('Unparsed content tail')
    return result


def parse_container(payload, resource, global_offset):
    if len(payload) < 4:
        raise ValueError("Truncated container")
    count = struct.unpack_from("<I", payload)[0]
    if count > (len(payload)-4)//4:
        raise ValueError("Invalid container count")
    cursor = 4
    result = []
    for ordinal in range(count):
        if cursor+4 > len(payload):
            raise ValueError("Truncated record length")
        size = struct.unpack_from("<I", payload, cursor)[0]
        cursor += 4
        if size < 64 or cursor+size > len(payload):
            raise ValueError(f"Invalid placement boundary {resource}:{ordinal}:{size}")
        data = payload[cursor:cursor+size]
        typ, instance = struct.unpack_from("<II", data)
        override_size = struct.unpack_from("<I", data, 60)[0]
        if size != 64+override_size:
            raise ValueError(f"Placement override size mismatch {resource}:{ordinal}")
        if typ == 0xdac6:
            pf = struct.unpack_from("<I", data, 20)[0]
            position = list(struct.unpack_from("<3f", data, 24))
            rotation = list(struct.unpack_from("<4f", data, 36))
            if not all(math.isfinite(v) for v in position+rotation):
                raise ValueError("Nonfinite transform")
            flat = None
            reason = None
            try:
                flat = parse_content(data[64:]) if override_size else None
            except ValueError as error:
                reason = str(error)
            result.append({"identity_type": typ, "identity_instance_uint32": instance, "identity_instance_signed": instance-(1<<32) if instance >= 1<<31 else instance,
                "identity_instance_hex": f"0x{instance:08X}", "explicit_playfield_id": pf,
                "content_identity_component_diagnostic": (instance >> 16) & 0x3fff, "low_16_bits_diagnostic": instance & 65535, "high_16_bits_diagnostic": instance >> 16,
                "parent_resource_type": resource[0], "parent_resource_instance": resource[1], "container_record_ordinal": ordinal,
                "source_record_offset": global_offset+cursor, "source_record_length": size, "parent_payload_offset": global_offset,
                "template_identity_type": None, "template_identity_instance": struct.unpack_from("<I", data, 56)[0],
                "raw_position_components": position, "raw_rotation_components": rotation, "raw_scale_components": None,
                "raw_transform_bytes": data[24:52].hex(), "unknown_header_bytes": data[8:20].hex(), "unknown_word_52": struct.unpack_from("<I", data, 52)[0],
                "override_raw_hex": data[64:].hex(), "flat_override": flat, "override_parse_error": reason,
                "source_record_sha256": hashlib.sha256(data).hexdigest(), "extraction_confidence": "PROVEN_FROM_LOCAL_EXTRACTION", "extractor_version": VERSION})
        cursor += size
    if cursor != len(payload):
        raise ValueError(f"Unparsed container tail: {resource}, {len(payload)-cursor}")
    return result, count


def extract():
    reader = RdbReader(DB)
    result, counts = [], []
    for key in sorted(k for k in reader.records if k[0] == 1000026):
        payload = reader.get(*key)
        rows, count = parse_container(payload, key, reader.records[key]+34)
        result.extend(rows)
        counts.append({"resource_type": key[0], "resource_instance": key[1], "records": count, "acgentrances": len(rows), "payload_sha256": hashlib.sha256(payload).hexdigest(), "offset": reader.records[key]+34})
    ids = Counter((r['identity_type'], r['identity_instance_uint32']) for r in result)
    if any(v > 1 for v in ids.values()):
        raise ValueError("Duplicate complete identities")
    write_json(OUT / "acgentrance-records-initial.json", result)
    write_json(OUT / "acgentrance-container-coverage.json", counts)
    print(json.dumps({"containers": len(counts), "placements": sum(r['records'] for r in counts), "acgentrances": len(result), "playfields": len({r['explicit_playfield_id'] for r in result}), "names_parsed": sum(r['flat_override'] is not None for r in result), "parse_errors": dict(Counter(r['override_parse_error'] for r in result)), "templates": dict(Counter(r['template_identity_instance'] for r in result)), "pf505": len([r for r in result if r['explicit_playfield_id'] == 505])}))


def diagnostics():
    reader = RdbReader(DB)
    rows = json.loads((OUT / 'acgentrance-records-initial.json').read_text())
    external = json.loads(re.sub(r',\s*}\s*$', '\n}', CATALOG.read_text(encoding='utf-8-sig')))
    reverse = {v: k for k, values in external.items() for v in values}
    for row in rows:
        name = row['flat_override']['name'] if row['flat_override'] else None
        if name != reverse.get(row['identity_instance_uint32']) or not row['flat_override']:
            print("NAME_COMPARISON", row['identity_instance_hex'], row['explicit_playfield_id'], repr(name), repr(reverse.get(row['identity_instance_uint32'])), row['override_raw_hex'] if not row['flat_override'] else '')
    for tid in sorted({r['template_identity_instance'] for r in rows}):
        for key in sorted(k for k in reader.records if k[1] == tid):
            payload = reader.get(*key)
            print("TEMPLATE", key, len(payload), payload[:100].hex(' '))
            try:
                flat = parse_content(payload, template=True)
                write_json(OUT / 'templates' / f'{key[0]}-{key[1]}.json', {"source_offset": reader.records[key]+34, "payload_sha256": hashlib.sha256(payload).hexdigest(), **flat})
                print("TEMPLATE_PARSED", repr(flat['name']), flat['stat_pairs'])
            except ValueError as error:
                print("TEMPLATE_UNPARSED", str(error))
    for path in sorted(CLIENT.glob('*version*')):
        if path.is_file():
            print("VERSION_FILE", str(path), path.read_bytes()[:160])


def native(args):
    if not re.fullmatch('[a-z0-9-]+',args.label):
        raise ValueError('Native evidence label must be a plain lowercase slug')
    if args.module not in EXPECTED or not args.module.endswith('.dll'):
        raise ValueError('Only the explicitly fingerprinted modules may be analyzed')
    if sha(CLIENT/args.module) != EXPECTED[args.module][0]:
        raise ValueError('Native module SHA mismatch')
    toolchain = pathlib.Path(r'C:\temp\ao-ghidra-toolchain-20260829')
    headless = toolchain / 'ghidra/ghidra_12.1.3_PUBLIC/support/analyzeHeadless.bat'
    project_root = ROOT / 'tools-temp/acgentrance-analysis/ghidra'
    project_root.mkdir(parents=True, exist_ok=True)
    project = 'AcgFresh' + args.module.replace('.', '')
    env = dict(os.environ)
    java = list(toolchain.rglob('bin/java.exe'))
    if len(java) != 1:
        raise ValueError(f'Expected one bundled Java runtime, found {len(java)}')
    env['JAVA_HOME'] = str(java[0].parents[1])
    private_home = ROOT / 'tools-temp/acgentrance-analysis/home'
    private_tmp = ROOT / 'tools-temp/acgentrance-analysis/tmp'
    private_home.mkdir(parents=True, exist_ok=True)
    private_tmp.mkdir(parents=True, exist_ok=True)
    env['JAVA_TOOL_OPTIONS'] = f'-Duser.home={private_home} -Djava.io.tmpdir={private_tmp}'
    env['APPDATA'] = str(private_home)
    env['LOCALAPPDATA'] = str(private_home)
    output = OUT / ('native-' + args.label + '.txt')
    log = ROOT / 'tools-temp/acgentrance-analysis' / (args.label+'.log')
    operation = ['-process', args.module, '-readOnly', '-noanalysis'] if (project_root / (project+'.gpr')).exists() else ['-import', str(CLIENT / args.module), '-analysisTimeoutPerFile', '300']
    command = [str(headless), str(project_root), project, *operation, '-log', str(log.with_suffix('.headless.log')), '-scriptPath', str(ROOT / 'Tools/acgentrance'), '-postScript', 'ExportAcgEvidence.java', str(output), *args.rvas]
    with log.open('wb') as stream:
        done = subprocess.run(command, cwd=ROOT, env=env, stdout=stream, stderr=subprocess.STDOUT)
    if done.returncode or not output.exists():
        print(log.read_text(errors='replace')[-4500:])
        raise ValueError('Native evidence export failed')
    if 'Executable SHA256: '+sha(CLIENT/args.module) not in output.read_text():
        raise ValueError('Private Ghidra program does not match current source binary')
    from acgentrance_evidence import compact_native
    compact_native(output)
    print(f'NATIVE_EVIDENCE=PASS {output.relative_to(ROOT)}')


def native_index():
    inventory, consumers = [], []
    for path in sorted(CLIENT.glob('*.dll')):
        before = sha(path)
        try:
            pe = PE(path)
            matches = [r for r in pe.imports() if 'GetEntranceDoor' in r['name']]
            consumers.extend({'module': path.name, **r} for r in matches)
            inventory.append({'path': str(path), 'sha256': before, 'get_entrance_door_imports': matches, 'status': 'PE32_IMPORTS_INSPECTED'})
        except (ValueError, UnicodeError, struct.error) as error:
            inventory.append({'path': str(path), 'sha256': before, 'status': 'UNPARSED', 'reason': str(error)})
    write_json(OUT / 'client-module-import-coverage.json', inventory)
    print('GET_ENTRANCE_DOOR_CONSUMERS', consumers)
    pe = PE(CLIENT/'Gamecode.dll')
    for rva in (0x1c18ac, 0x1be158):
        off = pe.offset(rva)
        print('RTTI', hex(rva), pe.data[off+8:pe.data.index(b'\0', off+8)])
    for classname in ('Door_t', 'StatHolder_t', 'QuestAlternativeIIR_t', 'CreateQuestIIR_t'):
        marker = ('.?AV'+classname+'@@').encode()
        pos = pe.data.find(marker)
        if pos < 0:
            print('RTTI_NOT_FOUND', classname)
            continue
        section = next(s for s in pe.sections if s[3] <= pos < s[3]+s[4])
        td = pe.base + section[1] + pos-section[3]-8
        refs = []
        for m in re.finditer(re.escape(struct.pack('<I', td)), pe.data):
            off = m.start()-12
            if off < 0:
                continue
            sig, offset, cd = struct.unpack_from('<III', pe.data, off)
            if sig != 0 or offset != 0 or cd != 0:
                continue
            sec = next((s for s in pe.sections if s[3] <= off < s[3]+s[4]), None)
            if not sec:
                continue
            col = pe.base+sec[1]+off-sec[3]
            for v in re.finditer(re.escape(struct.pack('<I', col)), pe.data):
                vt_off = v.start()+4
                vsec = next(s for s in pe.sections if s[3] <= vt_off < s[3]+s[4])
                vt_rva = vsec[1]+vt_off-vsec[3]
                getter = struct.unpack_from('<I', pe.data, vt_off+0x3c)[0]
                refs.append({'vtable_rva': hex(vt_rva), 'getter_rva': hex(getter-pe.base), 'complete_object_locator': hex(col-pe.base), 'first_slots': [hex(v-pe.base) for v in struct.unpack_from('<8I', pe.data, vt_off)]})
        print('RTTI_VTABLE', classname, refs)
    rows = json.loads((OUT/'acgentrance-records-initial.json').read_text())
    print('PLACEMENT_STAT_BD', Counter(v for r in rows for k,v in r['flat_override']['stat_pairs'] if k == 189))


def focused():
    pe = PE(CLIENT/'Gamecode.dll')
    print('WORLD_POS_SYMBOLS', [e for e in PE(CLIENT/'GameData.dll').exports() if 'WorldPos' in e['name']])
    for match in re.finditer(rb'\.\?AV[^\x00]{1,120}@@\x00', pe.data):
        if any(s in match[0] for s in (b'Stat', b'Quest', b'Mission', b'Ability')):
            print('TYPE', match[0][:-1].decode('ascii', errors='replace'))
    prior = ROOT/'docs/generated/missions/location-reconciliation'
    manifest = json.loads((prior/'source-manifest.json').read_text())
    print('PRIOR_SOURCES', len(manifest), manifest[:1])
    with gzip.open(prior/'all-offers.jsonl.gz', 'rt', encoding='utf-8') as stream:
        print('OFFER_EXAMPLE', stream.readline().strip())


def capture_example():
    prior = ROOT/'docs/generated/missions/location-reconciliation'
    for src in json.loads((prior/'source-manifest.json').read_text()):
        if not src['primary_level2']:
            continue
        path = ROOT/'docs/reference/missions/modern-capture/level2-slider-discovery/raw'/src['session_id']/'events.jsonl'
        for line in path.open(encoding='utf-8'):
            event = json.loads(line)
            if event['event_type'] == 'cohort_received':
                c = event['payload']
                print('COHORT_KEYS', sorted(c))
                raw = base64.b64decode(c['raw_response_packet']['base64'])
                print('HEADER', len(raw), raw[:100].hex(' '))
                from acgentrance_mission_decoder import decode_cohort
                decoded = decode_cohort(raw, c['offers'])
                print('DECODED', json.dumps([{k:v for k,v in row.items() if k not in ('description_raw_hex', 'title_raw_hex')} for row in decoded['offers']]))
                return


def coordinate_proof():
    rows = [json.loads(line) for line in (OUT/'acgentrance-records.jsonl').read_text().splitlines()]
    byid = {r['identity_instance_uint32']:r for r in rows}
    conflicts, counts = [], Counter()
    with gzip.open(OUT/'mission-location-full-corpus-reconciliation.jsonl.gz', 'rt', encoding='utf-8') as stream:
        for line in stream:
            r = json.loads(line)
            ids = r['exact_local_float32_candidates_diagnostic']
            counts['one_unique_exact_coordinate'] += len(ids)==1
            if len(ids)==1 and r['description_name_candidates']:
                n = byid[ids[0]]['display_name_exact']
                if n in r['description_name_candidates']:
                    counts['coordinate_and_description_name_agree'] += 1
                else:
                    conflicts.append({'request_id':r['request_id'], 'offer_index':r['offer_index'], 'local_name':n, 'names':r['description_name_candidates']})
    print('COORDINATE_PROOF', dict(counts), 'NAME_CONFLICTS', len(conflicts), conflicts[:5])
    for module in ('N3.dll', 'Gamecode.dll'):
        print('POSITION_SYMBOLS', module, [r for r in PE(CLIENT/module).exports() if any(s in r['name'] for s in ('AddChildDynel', 'SetRelPos', 'GetRelPos', 'GetGlobalPos', 'SetGlobalPos'))])


def factory_proof():
    pe = PE(CLIENT/'Gamecode.dll')
    for rva in (0x15217b, 0x152559, 0x152577):
        off = pe.offset(rva)
        print('FACTORY_REFERENCE', hex(rva), pe.data[off-16:off+20].hex(' '))
    for m in re.finditer(re.escape(struct.pack('<I', 0xdac6)), pe.data):
        off = m.start()
        sec = next((s for s in pe.sections if s[3]<=off<s[3]+s[4]), None)
        if sec:
            print('TYPE_DAC6_REFERENCE', hex(sec[1]+off-sec[3]), pe.data[max(0,off-12):off+20].hex(' '))


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    p = argparse.ArgumentParser(description=__doc__)
    sub = p.add_subparsers(dest="command", required=True)
    sub.add_parser("sources").add_argument("--verify", action="store_true")
    sub.add_parser("structure")
    sub.add_parser("extract")
    sub.add_parser("diagnostics")
    sub.add_parser('native-index')
    sub.add_parser('focused')
    sub.add_parser('capture-example')
    sub.add_parser('coordinate-proof')
    sub.add_parser('factory-proof')
    gp = sub.add_parser('generate')
    gp.add_argument('--check', action='store_true')
    gp.add_argument('--catalog-only', action='store_true')
    sub.add_parser('references').add_argument('--verify', action='store_true')
    sub.add_parser('test')
    sub.add_parser('repository-provenance')
    sub.add_parser('compact-native')
    np = sub.add_parser('native')
    np.add_argument('module')
    np.add_argument('label')
    np.add_argument('rvas', nargs='+')
    ip = sub.add_parser("inspect")
    ip.add_argument("path")
    ip.add_argument("--start", type=lambda x: int(x, 0), default=1)
    ip.add_argument("--length", type=int, default=100)
    ip.add_argument("--hex", action="store_true")
    args = p.parse_args()
    if args.command == "sources":
        sources(args.verify)
    elif args.command == "inspect":
        inspect(args)
    elif args.command == "structure":
        structure()
    elif args.command == "extract":
        extract()
    elif args.command == "diagnostics":
        diagnostics()
    elif args.command == 'native':
        native(args)
    elif args.command == 'native-index':
        native_index()
    elif args.command == 'focused':
        focused()
    elif args.command == 'capture-example':
        capture_example()
    elif args.command == 'coordinate-proof':
        coordinate_proof()
    elif args.command == 'factory-proof':
        factory_proof()
    elif args.command == 'generate':
        from acgentrance_artifacts import run
        run(args.check, args.catalog_only)
    elif args.command == 'references':
        from acgentrance_evidence import reference_manifest
        reference_manifest(args.verify)
    elif args.command == 'test':
        from test_acgentrance_reconstruction import run
        run()
    elif args.command == 'repository-provenance':
        from acgentrance_evidence import repository_provenance
        repository_provenance()
    elif args.command == 'compact-native':
        from acgentrance_evidence import compact_all
        compact_all()


if __name__ == "__main__":
    main()
