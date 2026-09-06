"""Deterministic, evidence-only projections and fixed-corpus reconciliation."""
import base64
import csv
import gzip
import hashlib
import io
import json
import math
import pathlib
import re
import struct
from collections import Counter, defaultdict

from acgentrance_reconstruction import ROOT, OUT, DB, CLIENT, CATALOG, VERSION, RdbReader, parse_container, parse_content, sha
from acgentrance_mission_decoder import decode_cohort, decode_worldpos

GENERATED = {}
CHECK = False


def audit_index(raw):
    if len(raw)<188:
        raise ValueError('Truncated RDB index header')
    pos = struct.unpack_from('<I', raw, 72)[0]
    visited, keys = set(), {}
    while pos:
        if pos in visited or pos+28>len(raw):
            raise ValueError('RDB index cycle or truncated node')
        visited.add(pos)
        nxt = struct.unpack_from('<I', raw, pos)[0]
        count = struct.unpack_from('<h', raw, pos+8)[0]
        if count<0 or pos+28+count*16>len(raw):
            raise ValueError('RDB index count exceeds node bounds')
        for off in range(pos+28, pos+28+count*16, 16):
            high, low = struct.unpack_from('<II', raw, off)
            key = struct.unpack_from('>ii', raw, off+8)
            if key in keys:
                raise ValueError('Duplicate RDB index identity')
            keys[key] = high<<32 | low
        pos = nxt
    return keys, len(visited)


def emit(name, data):
    if isinstance(data, str):
        data = data.encode('utf-8')
    GENERATED[name] = hashlib.sha256(data).hexdigest()
    path = OUT/name
    if CHECK:
        if not path.exists() or path.read_bytes() != data:
            raise ValueError('STALE_ARTIFACT: '+name)
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)


def json_file(name, value):
    emit(name, json.dumps(value, indent=2, sort_keys=True, ensure_ascii=True)+'\n')


def lines_file(name, rows):
    data = ''.join(json.dumps(r, separators=(',', ':'), sort_keys=True, ensure_ascii=True)+'\n' for r in rows).encode()
    if name.endswith('.gz'):
        buffer = io.BytesIO()
        with gzip.GzipFile(filename='', mode='wb', fileobj=buffer, mtime=0) as stream:
            stream.write(data)
        data = buffer.getvalue()
    emit(name, data)


def unique_ids(rows):
    keys = [(r['identity_type'], r['identity_instance_uint32']) for r in rows]
    if len(keys) != len(set(keys)):
        raise ValueError('DUPLICATE_COMPLETE_IDENTITY')


def stat_resolution(template_pairs, override_pairs):
    direct = [v for k,v in template_pairs if k == 189]
    overrides = [v for k,v in override_pairs if k == 189]
    conflict = len(set(direct)) > 1 or len(set(overrides)) > 1
    # Only serialized inputs are resolved here. No claim that arbitrary construction
    # or event scripts cannot subsequently alter them; a runtime bridge is required.
    return {'stat_0xBD_direct_value': None,
        'stat_0xBD_inherited_value': direct[0] if len(set(direct)) == 1 else None,
        'stat_0xBD_override_value': overrides[0] if len(set(overrides)) == 1 else None,
        'stat_0xBD_effective_value': None, 'stat_getter_mode': 2,
        'stat_resolution_source': 'REFERENCED_TEMPLATE_AND_PLACEMENT_OVERRIDE',
        'stat_resolution_path': ['1000020 template stat section', '1000026 override stat section',
            'Gamecode.dll RVA 0x80bb7 applies override pairs', 'RVA 0x3d90 -> StatHolder_t getter RVA 0x2e62b'],
        'stat_resolution_confidence': 'STAT_0xBD_UNRESOLVED',
        'construction_default_value': 0,
        'construction_default_source': 'Gamecode.dll Door_t initializer RVA0x7F113 explicitly adds stat189=0; factory registration RVA0x152559 maps DAC6 to Door_t creator0xD213',
        'reason': 'CONFLICTING_SERIALIZED_VALUES' if conflict else 'CONSTRUCTION_AND_DYNAMIC_ASSIGNMENT_NOT_FULLY_PROVEN',
        'serialized_value_present': bool(direct or overrides), 'missing_stat_getter_sentinel': 1234567890,
        'mode_behavior': 'StatHolder_t getter ignores the second argument; reads array slot or returns sentinel. This does not prove all construction-time writes.'}


def scoped_key_collisions(rows):
    groups = defaultdict(list)
    for r in rows:
        key, scope = r.get('operational_entrance_key'), r.get('registry_owner_instance')
        if key is not None:
            if scope is None:
                raise ValueError('Cannot index an operational key without loaded-owner scope')
            groups[(scope, key)].append(r['identity_instance_uint32'])
    return [{'scope': s, 'key': k, 'identities': ids} for (s,k),ids in sorted(groups.items()) if len(ids)>1]


def catalog_rows():
    keys, node_count = audit_index((DB/'ResourceDatabase.idx').read_bytes())
    reader = RdbReader(DB)
    if keys!=reader.records:
        raise ValueError('Existing RDB reader disagrees with index validation')
    json_file('acgentrance-rdb-index-coverage.json', {'index_entries':len(keys), 'linked_nodes':node_count,
        'resource_type_counts':dict(Counter(k[0] for k in keys)), 'duplicates':0,
        'scope':'Every active index entry audited; every 1000026 placement container fully parsed. Not an unindexed/deleted-record or arbitrary-byte signature census.'})
    rows, coverage = [], []
    for key in sorted(k for k in reader.records if k[0] == 1000026):
        payload = reader.get(*key)
        extracted, count = parse_container(payload, key, reader.records[key]+34)
        rows.extend(extracted)
        coverage.append({'resource': list(key), 'payload_offset': reader.records[key]+34,
            'payload_sha256': hashlib.sha256(payload).hexdigest(), 'placements': count, 'acgentrances': len(extracted)})
    unique_ids(rows)
    rows.sort(key=lambda r: (r['explicit_playfield_id'], r['identity_instance_uint32'], r['source_record_offset']))
    templates = {}
    for tid in sorted({r['template_identity_instance'] for r in rows}):
        payload = reader.get(1000020, tid)
        if struct.unpack_from('<I', payload)[0] != 0xdac6:
            raise ValueError('Referenced template is not ACGEntrance type')
        templates[tid] = {'resource_type': 1000020, 'resource_instance': tid,
            'source_offset': reader.records[(1000020, tid)]+34,
            'payload_sha256': hashlib.sha256(payload).hexdigest(), 'raw_hex': payload.hex(),
            **parse_content(payload, template=True)}
    source_manifest = json.loads((OUT/'acgentrance-source-manifest.json').read_text())
    database_hash = next(r['sha256'] for r in source_manifest['inputs'] if r['filename']=='ResourceDatabase.dat')
    for r in rows:
        flat = r['flat_override']
        if flat is None:
            raise ValueError('Unparsed override must be investigated before full catalog generation')
        template = templates[r['template_identity_instance']]
        name_source = flat if flat['name'] is not None else template
        name_offset = r['source_record_offset']+64+flat['name_offset'] if name_source is flat else template['source_offset']+template['name_offset']
        r.update(display_name_exact=name_source['name'], display_name_raw_bytes=name_source['name_raw_hex'],
            text_encoding=name_source['encoding'], string_resource_type=r['parent_resource_type'] if name_source is flat else 1000020,
            string_resource_instance=r['parent_resource_instance'] if name_source is flat else r['template_identity_instance'],
            string_source_offset=name_offset, localization_key=None,
            name_resolution_path='PLACEMENT_OVERRIDE_SECTION_21' if name_source is flat else 'REFERENCED_TEMPLATE_SECTION_21_NO_PLACEMENT_NAME_SECTION',
            source_database_sha256=database_hash, source_manifest='acgentrance-source-manifest.json',
            evidence_manifest='mission-location-evidence-manifest.json',
            district_or_container_identity={'type': r['parent_resource_type'], 'instance': r['parent_resource_instance']},
            template_resource_type=1000020,
            operational_entrance_key=None, operational_entrance_key_stat_id=189,
            operational_entrance_key_scope='LOADED_n3Playfield_t_OBJECT_INSTANCE', registry_owner_instance=None,
            normalized_ao_position=None, normalized_ao_rotation=None,
            coordinate_classification='RAW_AO_PLACEMENT_VECTOR3_AND_QUATERNION_NO_ENGINE_CONVERSION',
            relationship_classification='PLACEMENT_ONLY', door_identity=None, statel_identity=None,
            building_identity=None, room_identity=None, interaction_radius=None,
            **stat_resolution(template['stat_pairs'], flat['stat_pairs']))
    json_file('acgentrance-container-coverage.json', coverage)
    json_file('acgentrance-templates.json', list(templates.values()))
    lines_file('acgentrance-records.jsonl', rows)
    csv_keys = ['identity_type', 'identity_instance_uint32', 'identity_instance_signed', 'identity_instance_hex',
        'explicit_playfield_id', 'display_name_exact', 'operational_entrance_key', 'stat_resolution_confidence',
        'raw_position_components', 'raw_rotation_components', 'template_identity_instance', 'source_record_offset', 'source_record_sha256', 'evidence_manifest']
    csvout = io.StringIO(newline='')
    writer = csv.DictWriter(csvout, csv_keys, lineterminator='\n')
    writer.writeheader()
    for r in rows:
        writer.writerow({k: json.dumps(r[k], ensure_ascii=True) if isinstance(r[k], (dict,list)) else r[k] for k in csv_keys})
    emit('acgentrance-records.csv', csvout.getvalue())
    base = ['identity_type', 'identity_instance_uint32', 'explicit_playfield_id', 'source_record_offset', 'evidence_manifest']
    for name, fields in {
        'acgentrance-stat-0xbd-resolution.jsonl': [k for k in rows[0] if k.startswith('stat_')]+['reason', 'missing_stat_getter_sentinel','construction_default_value','construction_default_source'],
        'acgentrance-operational-key-crosswalk.jsonl': ['operational_entrance_key', 'operational_entrance_key_stat_id', 'operational_entrance_key_scope', 'registry_owner_instance', 'stat_resolution_confidence'],
        'acgentrance-coordinate-map.jsonl': ['raw_position_components', 'raw_rotation_components', 'raw_scale_components', 'raw_transform_bytes', 'normalized_ao_position', 'normalized_ao_rotation', 'coordinate_classification'],
        'acgentrance-door-room-links.jsonl': ['door_identity', 'room_identity', 'building_identity', 'statel_identity', 'interaction_radius', 'relationship_classification'],
    }.items():
        lines_file(name, [{k:r[k] for k in base+fields} for r in rows])
    lines_file('acgentrance-pf505-fixture.jsonl', [r for r in rows if r['explicit_playfield_id']==505])
    pf_table = ['# PF505 extracted ACGEntrance placements', '',
        'Generated by `Tools\\acgentrance_reconstruction.cmd generate`; provenance: `mission-location-evidence-manifest.json`.', '',
        'All final operational keys are unresolved. Door construction default for stat 0xBD is zero, not an allocated key.', '',
        '| Complete instance | Exact local name | Explicit PF | Raw AO XYZ | Record offset | Operational key |',
        '| --- | --- | --- | --- | --- | --- |']
    for r in rows:
        if r['explicit_playfield_id']==505:
            pf_table.append(f"| {r['identity_instance_hex']} | {r['display_name_exact']} | 505 | {r['raw_position_components']} | 0x{r['source_record_offset']:X} | null |")
    emit('acgentrance-pf505.md', '\n'.join(pf_table)+'\n')
    return rows, coverage


def compare_external(rows):
    external = json.loads(re.sub(r',\s*}\s*$', '\n}', CATALOG.read_text(encoding='utf-8-sig')))
    rev = defaultdict(list)
    for name, ids in external.items():
        for iid in ids:
            if not isinstance(iid, int) or not 0<=iid<=0xffffffff:
                raise ValueError('Invalid external UInt32')
            rev[iid].append(name)
    local = {r['identity_instance_uint32']:r for r in rows}
    comparison = []
    for iid in sorted(set(rev)|set(local)):
        names, row = rev.get(iid, []), local.get(iid)
        name = row['display_name_exact'] if row else None
        if row is None:
            status = 'EXTERNAL_ONLY'
        elif not names:
            status = 'LOCAL_ONLY'
        elif len(names)>1:
            status = 'AMBIGUOUS'
        elif name is None:
            status = 'EXACT_ID_LOCAL_NAME_UNRESOLVED'
        elif name == names[0]:
            status = 'EXACT_ID_NAME_MATCH'
        elif name.casefold() == names[0].casefold():
            status = 'EXACT_ID_CASE_DIFFERENCE'
        elif name.strip() == names[0].strip():
            status = 'EXACT_ID_WHITESPACE_DIFFERENCE'
        elif name.encode('ascii', errors='replace').decode() == names[0]:
            status = 'EXACT_ID_ENCODING_DIFFERENCE'
        else:
            status = 'EXACT_ID_SUBSTANTIVE_NAME_DIFFERENCE'
        comparison.append({'identity_instance_uint32': iid, 'identity_instance_hex': f'0x{iid:08X}',
            'external_names_exact': names, 'local_name_exact': name, 'classification': status,
            'local_raw_name_hex': row['display_name_raw_bytes'] if row else None,
            'source_record_offset': row['source_record_offset'] if row else None,
            'evidence_manifest': 'mission-location-evidence-manifest.json'})
    counts = Counter(r['classification'] for r in comparison)
    summary = {'external_total_ids': sum(map(len, external.values())), 'external_unique_ids': len(rev),
        'external_exact_name_keys': len(external), 'local_total_ids': len(rows), 'local_unique_ids': len(local),
        'exact_id_intersection': len(set(rev)&set(local)), 'classifications': dict(counts),
        'external_duplicate_ids': {str(k):v for k,v in rev.items() if len(v)>1}, 'local_duplicate_ids': [],
        'external_catalog_sha256': sha(CATALOG), 'external_source_role': 'AUTHORITATIVE_EXTERNAL_GAME_CODE_EXTRACT',
        'provenance': 'Supplied by another project, reportedly extracted from AO game code/client content. This task independently extracts local records, not the origin of the supplied file.',
        'SOURCE_INDEPENDENTLY_REPRODUCED': 'PARTIAL', 'version_related_differences': 'UNKNOWN; no external source-client version fingerprint'}
    lines_file('acgentrance-external-catalog-comparison.jsonl', comparison)
    json_file('acgentrance-external-catalog-summary.json', summary)
    return summary


def position_candidates(rows):
    index = defaultdict(list)
    for r in rows:
        index[(r['explicit_playfield_id'], struct.pack('>3f', *r['raw_position_components']))].append(r['identity_instance_uint32'])
    return index


def resolve_offer(prior, decoded, index):
    dest = prior['destination']
    pf, coords = dest.get('playfield_identity'), dest.get('coordinates')
    ids = []
    if pf and coords:
        bits=struct.pack('>3f', *(coords[a] for a in 'xyz'))
        if decoded is not None:
            native=decoded['worldpos']
            if native['playfield_identity']!=pf or native['local_float32_hex']!=bits.hex():
                raise ValueError('Prior normalized destination disagrees with verified raw packet')
        ids = index.get((pf['instance'], bits), [])
    # Match the native WorldPos local vector to the native placement relative vector,
    # in the same explicit playfield, with identical IEEE754 binary32 bits only.
    # This identifies a catalog placement, never a physical-door/stately link.
    if not prior['inbound_sha256']:
        status = 'RAW_PACKET_MISSING'
    elif decoded is None:
        status = 'CLIENT_FIELD_MISSING'
    elif len(ids)>1:
        status = 'COORDINATE_AMBIGUOUS'
    elif len(ids)==1 and decoded['worldpos']['playfield_identity']['type']==40016:
        status = 'EXACT_PLAYFIELD_COORDINATE_MATCH'
    elif prior['description_name_candidates'] and not ids:
        status = 'NAME_ONLY_AMBIGUOUS'
    else:
        status = 'UNRESOLVED'
    exact = status == 'EXACT_PLAYFIELD_COORDINATE_MATCH'
    return {'resolution_status': status, 'resolved_acgentrance_identity': {'type':0xdac6, 'instance':ids[0]} if exact else None,
        'operational_entrance_key': None, 'exact_local_float32_candidates_diagnostic': ids,
        'number_of_exact_coordinate_candidates': len(ids),
        'captured_position': [coords[a] for a in 'xyz'] if coords else None,
        'coordinate_delta_per_axis': [0.0,0.0,0.0] if ids else None,
        'euclidean_distance': 0.0 if ids else None,
        'tolerance_source': 'EXACT_BINARY32_LOCAL_VECTOR; GameData.dll 0xca52/0xc7d2; Gamecode.dll 0x12231e; N3.dll 0xd415/0x52ad. Zero tolerance, no axis conversion.',
        'coordinate_proof': 'acgentrance-coordinate-model.json',
        'reason': 'Unique exact local AO coordinate and explicit playfield. Placement identity only; not an operational-key or physical-door link.' if exact else
            'No promotion without raw packet verification and one bit-exact local coordinate candidate. Names and terminal identities are not substitutes.'}


def reprocess(rows):
    from reconcile_mission_locations import packet
    from analyze_level2_mission_slider_capture import load_session
    prior_dir = ROOT/'docs/generated/missions/location-reconciliation'
    prior_manifest = json.loads((prior_dir/'source-manifest.json').read_text())
    expected_artifacts = json.loads((prior_dir/'artifact-manifest.json').read_text())
    prior_checks = []
    for name, digest in expected_artifacts['files'].items():
        raw_artifact = (prior_dir/name).read_bytes()
        actual = hashlib.sha256(raw_artifact).hexdigest()
        crlf_hash = hashlib.sha256(raw_artifact.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')).hexdigest() if not name.endswith('.gz') else None
        prior_checks.append({'file':name, 'recorded_sha256':digest, 'actual_sha256':actual, 'matches':actual==digest,
            'lf_to_crlf_matches_recorded':crlf_hash==digest, 'crlf_equivalent_sha256':crlf_hash})
        if actual != digest and crlf_hash != digest and name in ('all-offers.jsonl.gz','level2-offers.jsonl','source-manifest.json'):
            print('PRIOR_ARTIFACT_MISMATCH', prior_checks[-1])
            raise ValueError('PRIOR_CORPUS_ARTIFACT_DRIFT: '+name)
    json_file('prior-artifact-hash-verification.json', prior_checks)
    prior_rows = []
    with gzip.open(prior_dir/'all-offers.jsonl.gz', 'rt', encoding='utf-8') as stream:
        prior_rows = [json.loads(line) for line in stream]
    if len(prior_rows)!=93185 or sum(r['primary_level2'] for r in prior_rows)!=270:
        raise ValueError('FIXED_CORPUS_COUNT_MISMATCH')
    prior_index = {(r['session_id'], r['source_line'], r['offer_index']):r for r in prior_rows}
    if len(prior_index)!=len(prior_rows):
        raise ValueError('DUPLICATE_PRIOR_OFFER_KEY')
    decoded_index, source_coverage, errors = {}, [], []
    observed_keys = set()
    for src in prior_manifest:
        sid = src['session_id']
        retained = ROOT/'docs/reference/missions/modern-capture/level2-slider-discovery/raw'/sid/'events.jsonl'
        path = retained if retained.exists() else pathlib.Path(src['path'])
        if src['primary_level2']:
            load_session(sid, True, 'ACGEntrance offline reconstruction', path_override=path)
        digest = hashlib.sha256()
        counts = Counter()
        with path.open('rb') as stream:
            for number, line in enumerate(stream, 1):
                digest.update(line)
                event = json.loads(line)
                if event['event_type']!='cohort_received':
                    continue
                cohort = event['payload']
                offers = cohort['offers']
                counts['cohorts'] += 1
                counts['offers'] += len(offers)
                if len(offers)==5:
                    counts['five_offer_cohorts'] += 1
                data = packet(cohort['raw_response_packet']) if cohort.get('raw_response_packet') else None
                decoded = None
                if data is not None:
                    counts['offers_with_raw'] += len(offers)
                    try:
                        decoded = decode_cohort(data, offers)['offers']
                        counts['offers_decoded'] += len(offers)
                    except (ValueError, KeyError, struct.error) as error:
                        errors.append({'session_id':sid, 'source_line':number, 'reason':str(error)})
                        counts['offers_decoder_unresolved'] += len(offers)
                for i, offer in enumerate(offers):
                    key = (sid, number, offer['offer_index'])
                    old = prior_index.get(key)
                    if old is None or key in observed_keys:
                        raise ValueError('FIXED_CORPUS_LINK_MISMATCH')
                    observed_keys.add(key)
                    if (hashlib.sha256(data).hexdigest() if data is not None else None) != old['inbound_sha256']:
                        raise ValueError('PRIOR_PACKET_HASH_MISMATCH')
                    if old['mission_identity'] != offer.get('mission_identity'):
                        raise ValueError('MISSION_IDENTITY_LINK_MISMATCH')
                    if decoded is not None:
                        d = decoded[i]
                        d.pop('description_raw_hex')
                        d.pop('title_raw_hex')
                        decoded_index[key] = d
        if digest.hexdigest()!=src['sha256']:
            raise ValueError('CAPTURE_SOURCE_DRIFT: '+sid)
        source_coverage.append({'session_id':sid, 'path_read':str(path), 'prior_source_sha256':src['sha256'], 'counts':dict(counts)})
    if observed_keys!=set(prior_index):
        raise ValueError('INCOMPLETE_SOURCE_COVERAGE')
    index = position_candidates(rows)
    results, totals = [], {'level2':Counter(), 'full':Counter()}
    by_session = {r['session_id']:r for r in source_coverage}
    request_ids, slider_ids = set(), set()
    for old in prior_rows:
        key = (old['session_id'], old['source_line'], old['offer_index'])
        decoded = decoded_index.get(key)
        resolved = resolve_offer(old, decoded, index)
        record = {k:old[k] for k in ('session_id','request_id','cohort_id','offer_index','mission_identity','primary_level2','source_line','inbound_sha256','outbound_sha256','level','slider_state_id','roll_origin','destination','description_name_candidates','description_sha256')}
        record.update(prior_reconciliation_status=old['reconciliation_status'], decoder=decoded,
            prior_source_sha256=by_session[old['session_id']]['prior_source_sha256'],
            evidence_manifest='mission-location-evidence-manifest.json', **resolved)
        results.append(record)
        session_meta = by_session[old['session_id']].setdefault('offer_metadata_coverage',Counter())
        session_meta['with_destination_playfield'] += bool(old['destination'].get('playfield_identity'))
        session_meta['with_coordinates'] += bool(old['destination'].get('coordinates'))
        session_meta['with_catalog_name_candidates_in_description'] += bool(old['description_name_candidates'])
        session_meta['with_slider_metadata'] += old['slider_state_id'] is not None
        session_meta['with_character_level_metadata'] += old['level'] is not None
        session_meta['with_proven_operational_key'] += 0
        session_meta[resolved['resolution_status']] += 1
        scopes = ['full']+(['level2'] if old['primary_level2'] else [])
        if old['primary_level2']:
            request_ids.add(old['request_id'])
            slider_ids.add(old['slider_state_id'])
        for scope in scopes:
            t = totals[scope]
            t['offers']+=1
            t['offers_with_raw_packets']+=old['inbound_sha256'] is not None
            t['offers_with_description_catalog_name_candidates']+=bool(old['description_name_candidates'])
            t['offers_with_proven_exact_destination_name']+=0
            t['offers_with_playfield']+=bool(old['destination'].get('playfield_identity'))
            t['offers_with_coordinates']+=bool(old['destination'].get('coordinates'))
            t['offers_with_operational_key']+=0
            t['offers_with_decoded_worldpos']+=decoded is not None
            t['offers_with_slider_metadata']+=old['slider_state_id'] is not None
            t['offers_with_level_metadata']+=old['level'] is not None
            t['exactly_resolved_offers']+=resolved['resolution_status']=='EXACT_PLAYFIELD_COORDINATE_MATCH'
            t['strongly_resolved_offers']+=0
            t['ambiguous_offers']+=resolved['resolution_status'] in ('COORDINATE_AMBIGUOUS','NAME_ONLY_AMBIGUOUS')
            t['unresolved_offers']+=resolved['resolution_status'] not in ('COORDINATE_AMBIGUOUS','NAME_ONLY_AMBIGUOUS','EXACT_PLAYFIELD_COORDINATE_MATCH')
            t['zero_delta_unique_coordinate_candidates_diagnostic']+=len(resolved['exact_local_float32_candidates_diagnostic'])==1
            t[resolved['resolution_status']]+=1
    if len(request_ids)!=54 or len(slider_ids)!=27:
        raise ValueError('LEVEL2_REQUEST_SLIDER_COVERAGE_MISMATCH')
    results.sort(key=lambda r: (r['session_id'],r['source_line'],r['offer_index']))
    lines_file('mission-location-level2-reconciliation.jsonl', [r for r in results if r['primary_level2']])
    lines_file('mission-location-full-corpus-reconciliation.jsonl.gz', results)
    lines_file('mission-location-unresolved-offers.jsonl.gz', [{k:r[k] for k in ('session_id','request_id','offer_index','source_line','resolution_status','reason','inbound_sha256','evidence_manifest')} for r in results if r['resolved_acgentrance_identity'] is None])
    json_file('mission-location-capture-source-coverage.json', source_coverage)
    json_file('mission-location-decoder-unresolved.json', errors)
    return {k:dict(v) for k,v in totals.items()}


def run(check=False, catalog_only=False):
    global CHECK
    CHECK = check
    GENERATED.clear()
    # Before any derived artifacts, enforce the exact original official fingerprints.
    manifest = json.loads((OUT/'acgentrance-source-manifest.json').read_text())
    for src in manifest['inputs']:
        if sha(src['path'])!=src['sha256']:
            raise ValueError('OFFICIAL_SOURCE_INPUT_DRIFT')
    from acgentrance_evidence import reference_manifest
    reference_manifest(verify=True)
    rows, coverage = catalog_rows()
    from acgentrance_evidence import generate
    generate(json_file)
    comparison = compare_external(rows)
    names = defaultdict(list)
    for r in rows:
        names[r['display_name_exact']].append(r)
    coords = position_candidates(rows)
    summary = {'extractor_version':VERSION, 'total_acgentrances':len(rows),
        'unique_identities':len(rows), 'explicit_playfields':len({r['explicit_playfield_id'] for r in rows}),
        'parent_containers_scanned':len(coverage), 'all_placements_parsed':sum(r['placements'] for r in coverage),
        'low16_explicit_pf_mismatches':[r['identity_instance_uint32'] for r in rows if r['low_16_bits_diagnostic']!=r['explicit_playfield_id']],
        'prefixes':dict(Counter(f"0x{r['identity_instance_uint32'] & 0xc0000000:08X}" for r in rows)),
        'effective_stat_0xBD_resolved':0, 'effective_stat_0xBD_unresolved':len(rows),
        'raw_transform_count':len(rows), 'locally_recovered_name_count':sum(r['display_name_exact'] is not None for r in rows),
        'unique_names':len(names), 'multiple_id_name_groups':sum(len(v)>1 for v in names.values()),
        'same_name_multiple_playfield_groups':sum(len({r['explicit_playfield_id'] for r in v})>1 for v in names.values()),
        'same_name_same_playfield_groups':sum(n>1 for n in Counter((r['display_name_exact'],r['explicit_playfield_id']) for r in rows).values()),
        'coordinate_duplicate_groups':[{'playfield':k[0], 'float32_hex':k[1].hex(), 'identities':v} for k,v in sorted(coords.items()) if len(v)>1],
        'operational_key_collisions':scoped_key_collisions(rows), 'operational_key_collision_coverage':'NOT_EVALUABLE_NO_RESOLVED_KEYS',
        'exact_door_links':0, 'exact_room_links':0, 'exact_building_links':0, 'exact_statel_links':0,
        'pf505_content_components':[r['content_identity_component_diagnostic'] for r in rows if r['explicit_playfield_id']==505],
        'external_catalog_comparison':comparison,
        'LIVE_MISSION_CAPTURE_PERFORMED':'NO', 'RUNTIME_MISSION_LOGIC_CHANGED':'NO',
        'PRODUCTION_DESTINATION_DATA_CHANGED':'NO', 'EXTERNAL_SOURCE_MISREPRESENTED':'NO',
        'SOURCE_INDEPENDENTLY_REPRODUCED':'PARTIAL'}
    if not catalog_only:
        summary['capture_corpora']=reprocess(rows)
    json_file('mission-location-reconstruction-summary.json', summary)
    inputs = {'official_source_manifest_sha256':sha(OUT/'acgentrance-source-manifest.json'),
        'reference_input_manifest_sha256':sha(OUT/'acgentrance-reference-input-manifest.json'),
        'client_module_import_coverage_sha256':sha(OUT/'client-module-import-coverage.json'),
        'repository_provenance_sha256':sha(OUT/'acgentrance-repository-provenance.json'),
        'external_catalog_sha256':sha(CATALOG),
        'previous_capture_source_manifest_sha256':sha(ROOT/'docs/generated/missions/location-reconciliation/source-manifest.json'),
        'previous_capture_artifact_manifest_sha256':sha(ROOT/'docs/generated/missions/location-reconciliation/artifact-manifest.json')}
    native = {p.name:sha(p) for p in sorted(OUT.glob('native-*.txt'))}
    json_file('mission-location-evidence-manifest.json', {'extractor_version':VERSION,
        'command':'Tools\\acgentrance_reconstruction.cmd generate', 'check_command':'Tools\\acgentrance_reconstruction.cmd generate --check',
        'input_manifests':inputs, 'native_evidence_files':native,
        'tool_hashes':{p.name:sha(p) for p in [ROOT/'Tools/acgentrance_reconstruction.py', ROOT/'Tools/acgentrance_artifacts.py', ROOT/'Tools/acgentrance_evidence.py', ROOT/'Tools/acgentrance_mission_decoder.py', ROOT/'Tools/test_acgentrance_reconstruction.py', ROOT/'Tools/acgentrance/ExportAcgEvidence.java']},
        'artifact_sha256':dict(GENERATED), 'ordering':'playfield and full UInt32 identity; offers by session/source-line/offer-index',
        'scope':'Offline evidence only. Crosswalk keys remain null; no runtime data promotion.'})
    print(json.dumps({k:v for k,v in summary.items() if k in ('total_acgentrances','explicit_playfields','locally_recovered_name_count','capture_corpora')}))
    print('ACGENTRANCE_STALE_CHECK=PASS' if check else 'ACGENTRANCE_GENERATION=PASS')
