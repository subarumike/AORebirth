"""Offline catalog reconciliation. Byte/name candidates never silently become identities."""
from __future__ import annotations

import argparse
import base64
from collections import Counter, defaultdict
import hashlib
import gzip
import json
from pathlib import Path
import re
from functools import lru_cache
from analyze_level2_mission_slider_capture import load_session

ROOT = Path(__file__).resolve().parents[1]
PRIMARY = ROOT / 'docs/generated/missions/modern-capture/level2-slider-discovery/primary-request-evidence.jsonl'
RAW = ROOT / 'docs/reference/missions/modern-capture/level2-slider-discovery/raw'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def packet(value):
    data = base64.b64decode(value['base64'], validate=True)
    if len(data) != value['byte_length'] or sha(data) != value['sha256']:
        raise ValueError('Raw packet length/hash mismatch')
    return data


def hits(data, ids):
    # Every match still receives a full exact four-byte comparison at every
    # possible offset. Prefix filtering avoids scanning all IDs for every byte.
    prefix = bytes(sorted({value >> 24 for value in ids}))
    result = []
    for m in re.finditer(b'[' + re.escape(prefix) + b']', data):
        for offset, order in ((m.start(), 'big'), (m.start()-3, 'little')):
            if 0 <= offset <= len(data)-4:
                value = int.from_bytes(data[offset:offset+4], order)
                if value in ids:
                    result.append(dict(offset=offset, byte_order=order, location_id=value))
    return sorted(result, key=lambda h: (h['offset'], h['byte_order']))


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--catalog', type=Path, required=True)
    p.add_argument('--output-dir', type=Path, required=True)
    p.add_argument('--sessions-root', type=Path)
    args = p.parse_args()
    catalog_bytes = args.catalog.read_bytes()
    # Supplied file has one terminal object comma; keep original bytes/hash intact.
    catalog_text = catalog_bytes.decode('utf-8-sig')
    catalog_text, trailing_commas = re.subn(r',\s*}\s*$', '\n}', catalog_text)
    catalog = json.loads(catalog_text)
    reverse = defaultdict(list)
    for name, ids in catalog.items():
        if not isinstance(name, str) or not isinstance(ids, list) or not ids:
            raise ValueError('Invalid catalog entry')
        for value in ids:
            if type(value) is not int or not 0 <= value <= 0xffffffff:
                raise ValueError('Invalid unsigned 32-bit ID')
            reverse[value].append(name)
    primary = {r['request_id']: r for r in map(json.loads, PRIMARY.read_text().splitlines())}
    primary_sessions = {r['session_id'] for r in primary.values()}
    # Reuse the established strict primary-capture validation: four packet
    # layers, exact hashes, all sliders, five offers, level and stop reason.
    for sid in sorted(primary_sessions):
        load_session(sid, True, 'authoritative catalog reconciliation')
    paths_by_session = {p.parent.name: p for p in RAW.glob('*/events.jsonl')}
    if args.sessions_root:
        for path in args.sessions_root.glob('*/events.jsonl'):
            if path.parent.name in paths_by_session:
                if sha(path.read_bytes()) != sha(paths_by_session[path.parent.name].read_bytes()):
                    raise ValueError(f'Retained/local capture conflict: {path.parent.name}')
            else:
                paths_by_session[path.parent.name] = path
    paths = [paths_by_session[k] for k in sorted(paths_by_session)]
    summary = dict(catalog_sha256=sha(catalog_bytes), terminal_object_commas_removed_for_parsing=trailing_commas, catalog_names=len(catalog),
                   catalog_ids=len(reverse), catalog_memberships=sum(map(len, catalog.values())),
                   duplicate_ids={str(k): v for k, v in reverse.items() if len(v) > 1})
    args.output_dir.mkdir(parents=True, exist_ok=True)
    name_regex = re.compile(r'(?<!\w)(?:' + '|'.join(re.escape(n) for n in sorted(catalog, key=len, reverse=True)) + r')(?!\w)')
    totals = {key: Counter() for key in ('primary_level2', 'all_existing_harvester_offers')}
    name_counts = {key: Counter() for key in totals}
    chunk_counts = {key: Counter() for key in totals}
    raw_counts = {key: Counter() for key in totals}
    manifest = []
    primary_ids = set()
    locations = defaultdict(lambda: {'offers': 0, 'names': set(), 'unknown5_pairs': set()})

    @lru_cache(maxsize=32768)
    def chunk_hits(value):
        return hits(base64.b64decode(value, validate=True), reverse)

    out_file = (args.output_dir / 'all-offers.jsonl.gz').open('wb')
    out = gzip.GzipFile(filename='', mode='wb', fileobj=out_file, mtime=0)
    primary_out = (args.output_dir / 'level2-offers.jsonl').open('w', encoding='utf-8')
    for path in paths:
        before = path.stat()
        source_hash = hashlib.sha256()
        starts, outbound, inbound = {}, {}, {}
        counters = Counter()
        seen_cohorts = set()
        with path.open('rb') as source:
          for lineno, line in enumerate(source, 1):
            source_hash.update(line)
            event = json.loads(line)
            rid = event.get('request_id')
            payload = event.get('payload', {})
            etype = event['event_type']
            counters[etype] += 1
            if etype == 'request_started':
                starts[rid] = {k: payload.get(k) for k in ('character_level', 'sliders', 'slider_state_id', 'roll_origin', 'terminal_identity')}
            elif etype in ('request_transmitted', 'raw_response_received') and payload.get('raw_packet'):
                decoded = packet(payload['raw_packet'])
                (outbound if etype == 'request_transmitted' else inbound)[rid] = sha(decoded)
            if etype != 'cohort_received':
                continue
            cohort = payload
            cid = cohort.get('cohort_id', f'{rid}/line/{lineno}')
            if cid in seen_cohorts:
                raise ValueError(f'Duplicate cohort: {cid}')
            seen_cohorts.add(cid)
            data = packet(cohort['raw_response_packet']) if cohort.get('raw_response_packet') else b''
            if rid in primary:
                if sha(data) != primary[rid]['inbound_sha256'] or outbound.get(rid) != primary[rid]['outbound_sha256']:
                    raise ValueError('Primary packet link mismatch')
                primary_ids.add(rid)
            request = starts.get(rid, {})
            origin = request.get('roll_origin') or {}
            terminal = (origin.get('terminal_identity') or request.get('terminal_identity') or cohort.get('terminal_identity') or {}).get('instance')
            terminal_u32 = (terminal & 0xffffffff) if terminal is not None else None
            ph = hits(data, reverse)
            for h in ph:
                h['equals_roll_terminal_instance'] = h['location_id'] == terminal_u32
            scopes = ['all_existing_harvester_offers'] + (['primary_level2'] if rid in primary else [])
            for scope in scopes:
                totals[scope]['cohorts'] += 1
                totals[scope]['cohorts_without_raw_response'] += not bool(data)
                totals[scope]['cohorts_with_five_offers'] += len(cohort['offers']) == 5
                totals[scope]['cohorts_with_linked_outbound_and_inbound'] += bool(outbound.get(rid)) and inbound.get(rid) == sha(data)
                raw_counts[scope].update((h['location_id'], h['byte_order'], h['equals_roll_terminal_instance']) for h in ph)
            for offer in cohort['offers']:
                ch = []
                for key, value in offer.get('unknown_fields', {}).items():
                    if key.endswith('Base64'):
                        ch.extend(dict(field=key, equals_roll_terminal_instance=hit['location_id'] == terminal_u32, **hit) for hit in chunk_hits(value))
                description = offer.get('description', '')
                names = sorted({m.group() for m in name_regex.finditer(description)})
                destination = offer.get('mission_destination') or {'playfield_identity': offer.get('playfield'), 'coordinates': offer.get('location')}
                for scope in scopes:
                    totals[scope]['offers'] += 1
                    totals[scope]['offers_with_chunk_catalog_hits'] += bool(ch)
                    totals[scope]['offers_with_name_candidates'] += bool(names)
                    totals[scope]['offers_without_name_candidates'] += not names
                    totals[scope]['offers_with_nonterminal_chunk_hits'] += any(not h['equals_roll_terminal_instance'] for h in ch)
                    name_counts[scope].update(names)
                    chunk_counts[scope].update((h['field'], h['offset'], h['byte_order'], h['location_id'], h['equals_roll_terminal_instance']) for h in ch)
                unknown5 = base64.b64decode(offer.get('unknown_fields', {}).get('UnkChunk5Base64', ''))
                pair5 = [int.from_bytes(unknown5[i:i+4], 'big') for i in range(0, len(unknown5), 4)]
                key = json.dumps(destination, sort_keys=True)
                locations[key]['offers'] += 1
                locations[key]['names'].update(names)
                locations[key]['unknown5_pairs'].add(tuple(pair5))
                row = dict(session_id=event['session_id'], request_id=rid, cohort_id=cid,
                           offer_index=offer['offer_index'], mission_identity=offer.get('mission_identity'),
                           primary_level2=rid in primary, source_line=lineno,
                           inbound_sha256=sha(data) if data else None, outbound_sha256=outbound.get(rid),
                           level=request.get('character_level'), slider_state_id=request.get('slider_state_id'),
                           roll_origin=origin, destination=destination,
                           chunk_catalog_hits=ch, packet_catalog_hits=ph,
                           unknown_chunk5_big_endian_words=pair5,
                           description_sha256=sha(description.encode('utf-8')),
                           description_name_candidates=names,
                           selected_location_id=None, reconciliation_status='UNRESOLVED_NO_PROVEN_DESTINATION_ID_BRIDGE')
                encoded = json.dumps(row, separators=(',', ':'))+'\n'
                out.write(encoded.encode('utf-8'))
                if rid in primary:
                    primary_out.write(encoded)
        after = path.stat()
        if (before.st_size, before.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
            raise ValueError(f'Capture changed during read: {path}')
        manifest.append(dict(session_id=path.parent.name, path=path.as_posix(), sha256=source_hash.hexdigest(),
                             bytes=before.st_size, events=dict(counters), primary_level2=path.parent.name in primary_sessions))
    out.close()
    out_file.close()
    primary_out.close()
    if primary_ids != set(primary) or totals['primary_level2']['offers'] != 270:
        raise ValueError('Incomplete primary coverage')
    for scope, counter in totals.items():
        summary[scope] = dict(counter)
        summary[scope]['confirmed_selected_location_ids'] = 0
        summary[scope]['name_candidates'] = dict(sorted(name_counts[scope].items()))
        summary[scope]['packet_catalog_hits'] = [dict(location_id=k[0], byte_order=k[1], equals_roll_terminal_instance=k[2], count=v, catalog_names=reverse[k[0]]) for k,v in sorted(raw_counts[scope].items())]
        summary[scope]['chunk_catalog_hits'] = [dict(field=k[0], offset=k[1], byte_order=k[2], location_id=k[3], equals_roll_terminal_instance=k[4], count=v) for k,v in sorted(chunk_counts[scope].items())]
    summary['sessions'] = len(paths)
    summary['primary_level2']['verified_slider_states'] = len({r['state_index'] for r in primary.values()})
    summary['source_role'] = 'AUTHORITATIVE_EXTERNAL_GAME_CODE_EXTRACT'
    summary['authoritative_for'] = ['Complete mission-location ID catalog', 'Exact location ID values', 'Exact associated display names']
    summary['origin'] = 'Supplied by another project; reportedly extracted directly from AO game code'
    summary['AOREBIRTH_LOCAL_GHIDRA_EXTRACTION'] = 'NO'
    summary['AOREBIRTH_INDEPENDENT_REPRODUCTION'] = 'NO'
    summary['LIVE_MISSION_CAPTURE_PERFORMED'] = 'NO'
    summary['RUNTIME_MISSION_LOGIC_CHANGED'] = 'NO'
    summary['SOURCE_INDEPENDENTLY_REPRODUCED'] = 'NO'
    summary['scope_boundary'] = 'All cohort_received offers in the enumerated retained and local MissionOfferHarvester sessions; zero-offer cohorts and legacy sessions retained in manifest. No claim to inventory unrelated capture products.'
    summary['byte_scan_boundary'] = 'Exact unsigned 32-bit values at every byte offset in both endian orders; not proof that an ID could not use a different encoding.'
    (args.output_dir / 'summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
    (args.output_dir / 'source-manifest.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
    (args.output_dir / 'catalog-index.json').write_text(json.dumps({str(k): dict(unsigned=k, signed=k if k < 0x80000000 else k-0x100000000, hex=f'0x{k:08X}', names=v) for k,v in sorted(reverse.items())}, indent=2)+'\n', encoding='utf-8')
    loc_rows = [dict(destination=json.loads(k), offers=v['offers'], description_name_candidates=sorted(v['names']), unknown_chunk5_pairs=sorted(v['unknown5_pairs'])) for k,v in sorted(locations.items())]
    (args.output_dir / 'observed-destinations.json').write_text(json.dumps(loc_rows, indent=2)+'\n', encoding='utf-8')
    generated = ['summary.json', 'source-manifest.json', 'catalog-index.json', 'observed-destinations.json', 'level2-offers.jsonl', 'all-offers.jsonl.gz']
    artifact_manifest = dict(catalog_sha256=sha(catalog_bytes), primary_request_index_sha256=sha(PRIMARY.read_bytes()),
                            generator_sha256=sha(Path(__file__).read_bytes()),
                            files={name: sha((args.output_dir/name).read_bytes()) for name in generated})
    (args.output_dir / 'artifact-manifest.json').write_text(json.dumps(artifact_manifest, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({scope: dict(c) for scope,c in totals.items()}))


if __name__ == '__main__':
    main()
