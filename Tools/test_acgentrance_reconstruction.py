"""Offline regression gates: no network, SQL, game client, or new capture."""
import base64
import copy
import gzip
import hashlib
import io
import json
import struct
import unittest

import acgentrance_artifacts as a
from acgentrance_reconstruction import ROOT, OUT, parse_container, parse_content
from acgentrance_mission_decoder import decode_cohort, decode_worldpos, f32


def override(name=b'Example ', pairs=()):
    sections = []
    if pairs:
        sections.append(struct.pack('>III',15,24,1009*(len(pairs)+1))+b''.join(struct.pack('>Ii',*p) for p in pairs))
    if name is not None:
        sections.append(struct.pack('>IIHH',21,33,len(name),0)+name)
    return struct.pack('>4I',102,103,1009,len(sections))+b''.join(sections)+b'\0'*4


def placement(iid=0xc00001f9, pf=505, blob=None):
    if blob is None:
        blob = override()
    raw = struct.pack('<6I3f4f3I',0xdac6,iid,1,0,0,pf,1.25,-2.5,3.75,1,0,0,0,0,41561,len(blob))+blob
    return struct.pack('<II',1,len(raw))+raw


class ReconstructionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.rows = [json.loads(s) for s in (OUT/'acgentrance-records.jsonl').read_text().splitlines()]
        cls.pf = [r for r in cls.rows if r['explicit_playfield_id']==505]
        path=ROOT/'docs/reference/missions/modern-capture/level2-slider-discovery/raw/mission-20260902T014541176Z-36962762-8baab3a4/events.jsonl'
        with path.open(encoding='utf-8') as stream:
            cls.cohort=next(json.loads(s)['payload'] for s in stream if json.loads(s)['event_type']=='cohort_received')
        cls.raw=base64.b64decode(cls.cohort['raw_response_packet']['base64'])

    def test_unsigned_full_identity_and_explicit_pf(self):
        row=parse_container(placement(0xfedcba98,505),(1000026,505),100)[0][0]
        self.assertEqual(row['identity_instance_uint32'],0xfedcba98)
        self.assertEqual(row['identity_instance_signed'],-19088744)
        self.assertEqual(row['identity_instance_hex'],'0xFEDCBA98')
        self.assertEqual(row['identity_type'],0xdac6)
        self.assertEqual(row['explicit_playfield_id'],505)
        self.assertNotEqual(row['low_16_bits_diagnostic'],505)
        self.assertEqual(row['source_record_offset'],108)

    def test_pf505_anchors_all29_and_gaps(self):
        self.assertEqual(len(self.pf),29)
        byid={r['identity_instance_uint32']:r for r in self.pf}
        for iid,offset,name in [(0xc00001f9,0x111f0e0a,'Central Desert Den'),(0xc00101f9,0x111f0ec3,'South Desert Den'),(0xc01201f9,0x111f13d4,'Mantis Hive')]:
            r=byid[iid]
            self.assertEqual((r['source_record_offset'],r['display_name_exact'],r['explicit_playfield_id']),(offset,name,505))
        self.assertEqual([r['content_identity_component_diagnostic'] for r in self.pf],list(range(23))+[24,25,26,29,31,33])

    def test_boundaries_and_truncated_container(self):
        raw=placement()
        for malformed in (b'',raw[:-1],raw+b'\0',struct.pack('<I',999999)+raw[4:]):
            with self.assertRaises(ValueError):
                parse_container(malformed,(1000026,505),0)
        self.assertEqual(parse_container(raw,(1000026,505),0)[1],1)

    def test_unknown_optional_fields_preserved(self):
        rows,_=parse_container(placement(blob=override(name=None)),(1000026,505),0)
        self.assertIsNone(rows[0]['flat_override']['name'])
        malformed=struct.pack('>4I',102,103,1009,1)+struct.pack('>I',99)
        rows,_=parse_container(placement(blob=malformed),(1000026,505),0)
        self.assertIn('Unsupported content section',rows[0]['override_parse_error'])
        self.assertEqual(rows[0]['override_raw_hex'],malformed.hex())

    def test_duplicate_identity_rejected(self):
        with self.assertRaises(ValueError):
            a.unique_ids([self.rows[0],self.rows[0]])

    def test_index_cycle_and_duplicate_detection(self):
        raw=bytearray(300)
        struct.pack_into('<I',raw,72,200)
        struct.pack_into('<I',raw,200,200)
        with self.assertRaisesRegex(ValueError,'cycle'):
            a.audit_index(raw)
        struct.pack_into('<I',raw,200,0)
        struct.pack_into('<h',raw,208,2)
        for off in (228,244):
            struct.pack_into('<II',raw,off,0,100)
            struct.pack_into('>ii',raw,off+8,1000026,505)
        with self.assertRaisesRegex(ValueError,'Duplicate'):
            a.audit_index(raw)

    def test_stat_direct_inherited_override_missing_conflict(self):
        inherited=a.stat_resolution([[189,12]],[])
        self.assertEqual(inherited['stat_0xBD_inherited_value'],12)
        changed=a.stat_resolution([[189,12]],[[189,19]])
        self.assertEqual(changed['stat_0xBD_override_value'],19)
        self.assertIsNone(changed['stat_0xBD_effective_value'])
        missing=a.stat_resolution([],[])
        self.assertFalse(missing['serialized_value_present'])
        self.assertEqual(missing['construction_default_value'],0)
        self.assertEqual(missing['missing_stat_getter_sentinel'],1234567890)
        self.assertEqual(missing['stat_getter_mode'],2)
        conflict=a.stat_resolution([[189,1],[189,2]],[])
        self.assertEqual(conflict['reason'],'CONFLICTING_SERIALIZED_VALUES')

    def test_registry_scope_duplicates_not_global(self):
        rows=[{'registry_owner_instance':'one','operational_entrance_key':7,'identity_instance_uint32':1},
              {'registry_owner_instance':'two','operational_entrance_key':7,'identity_instance_uint32':2}]
        self.assertEqual(a.scoped_key_collisions(rows),[])
        rows[1]['registry_owner_instance']='one'
        self.assertEqual(len(a.scoped_key_collisions(rows)),1)
        rows[1]['registry_owner_instance']=None
        with self.assertRaises(ValueError):
            a.scoped_key_collisions(rows)

    def test_exact_names_preserve_case_space_and_raw_encoding(self):
        raw=b'\xc6nima HQ ? '
        parsed=parse_content(override(raw))
        self.assertEqual(parsed['name'],'Ænima HQ ? ')
        self.assertEqual(parsed['name_raw_hex'],raw.hex())
        self.assertNotEqual(parsed['name'],parsed['name'].strip())
        self.assertEqual(sum(r['name_resolution_path'].startswith('REFERENCED_TEMPLATE') for r in self.rows),6)
        self.assertEqual(len({r['identity_instance_uint32'] for r in self.rows}),2242)

    def test_raw_transform_roundtrip(self):
        for r in self.pf:
            self.assertEqual(struct.pack('<3f4f',*r['raw_position_components'],*r['raw_rotation_components']).hex(),r['raw_transform_bytes'])
            self.assertIsNone(r['raw_scale_components'])

    def test_worldpos_signed_origin_local_world_and_width(self):
        result=decode_worldpos(struct.pack('>IIii3f',40016,505,-100,200,1.25,2.5,3.75))
        self.assertEqual(result['local_position'],[1.25,2.5,3.75])
        self.assertEqual(result['world_position'],[-98.75,2.5,203.75])
        self.assertIsNone(result['operational_entrance_key'])
        with self.assertRaises(ValueError):
            decode_worldpos(b'\0'*27)

    def test_five_offer_raw_boundaries_terminal_not_destination(self):
        d=decode_cohort(self.raw,self.cohort['offers'])
        self.assertEqual(len(d['offers']),5)
        self.assertEqual(d['offers'][0]['offer_start'],51)
        self.assertEqual(d['offers'][-1]['offer_end'],len(self.raw))
        self.assertEqual(d['offers'][0]['request_terminal_identity']['instance'],0xc000028f)
        self.assertEqual(d['offers'][0]['worldpos']['playfield_identity']['instance'],695)
        for prev,nxt in zip(d['offers'],d['offers'][1:]):
            self.assertEqual(prev['offer_end'],nxt['offer_start'])
        with self.assertRaises(ValueError):
            decode_cohort(self.raw[:-1],self.cohort['offers'])

    def test_tampered_coordinate_or_chunk_rejected(self):
        offers=copy.deepcopy(self.cohort['offers'])
        offers[0]['location']['x']+=0.25
        with self.assertRaisesRegex(ValueError,'coordinate'):
            decode_cohort(self.raw,offers)
        offers=copy.deepcopy(self.cohort['offers'])
        offers[0]['unknown_fields']['UnkChunk5Base64']=base64.b64encode(b'\0'*8).decode()
        with self.assertRaisesRegex(ValueError,'origin'):
            decode_cohort(self.raw,offers)

    def test_unique_match_ambiguity_missing_raw_no_nearest(self):
        prior={'destination':{'playfield_identity':{'type':40016,'instance':505},'coordinates':{'x':1.25,'y':-2.5,'z':3.75}},
               'inbound_sha256':'test-only','description_name_candidates':['Example']}
        row=parse_container(placement(),(1000026,505),0)[0][0]
        idx=a.position_candidates([row])
        decoded={'worldpos':{'playfield_identity':{'type':40016,'instance':505},'local_float32_hex':struct.pack('>3f',1.25,-2.5,3.75).hex()}}
        self.assertEqual(a.resolve_offer(prior,decoded,idx)['resolution_status'],'EXACT_PLAYFIELD_COORDINATE_MATCH')
        idx=a.position_candidates([row,{**row,'identity_instance_uint32':0xc00101f9}])
        self.assertEqual(a.resolve_offer(prior,decoded,idx)['resolution_status'],'COORDINATE_AMBIGUOUS')
        prior['destination']['coordinates']['x']+=0.00001
        with self.assertRaises(ValueError):
            a.resolve_offer(prior,decoded,idx)
        decoded['worldpos']['local_float32_hex']=struct.pack('>3f',*(prior['destination']['coordinates'][v] for v in 'xyz')).hex()
        self.assertIsNone(a.resolve_offer(prior,decoded,idx)['resolved_acgentrance_identity'])
        prior['inbound_sha256']=None
        self.assertEqual(a.resolve_offer(prior,None,idx)['resolution_status'],'RAW_PACKET_MISSING')

    def test_exact_corpus_counts_and_no_key_promotion(self):
        summary=json.loads((OUT/'mission-location-reconstruction-summary.json').read_text())
        self.assertEqual(summary['capture_corpora']['level2']['offers'],270)
        self.assertEqual(summary['capture_corpora']['full']['offers'],93185)
        self.assertEqual(summary['capture_corpora']['level2']['exactly_resolved_offers'],270)
        self.assertEqual(summary['capture_corpora']['full']['exactly_resolved_offers'],92830)
        self.assertEqual(summary['capture_corpora']['full']['unresolved_offers'],355)
        self.assertTrue(all(r['operational_entrance_key'] is None for r in self.rows))

    def test_repository_source_paths_are_worktree_independent(self):
        retained=ROOT/'docs/reference/missions/example/events.jsonl'
        self.assertEqual(a.stable_source_path(retained),'docs/reference/missions/example/events.jsonl')
        external=ROOT.parent/'external-capture/events.jsonl'
        self.assertEqual(a.stable_source_path(external),str(external.resolve()))

    def test_stale_artifact_detection_without_writes(self):
        previous=a.CHECK
        a.CHECK=True
        try:
            with self.assertRaisesRegex(ValueError,'STALE_ARTIFACT'):
                a.emit('mission-location-reconstruction-summary.json',b'not current')
        finally:
            a.CHECK=previous


def run():
    output=io.StringIO()
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(ReconstructionTests)
    result=unittest.TextTestRunner(stream=output).run(suite)
    if not result.wasSuccessful():
        print(output.getvalue())
        raise SystemExit(1)
    print(f'ACGENTRANCE_OFFLINE_TESTS=PASS ({result.testsRun} tests)')
