"""Regression checks for unsigned IDs, exact scans, integrity, and scope coverage."""
import base64
from collections import Counter
import gzip
import json
from pathlib import Path
import unittest

from reconcile_mission_locations import ROOT, hits, packet, sha


class ReconciliationTests(unittest.TestCase):
    def test_both_endian_orders_unaligned_and_boundary_offsets(self):
        value = 3221226127
        data = b'x' + value.to_bytes(4, 'big') + b'y' + value.to_bytes(4, 'little')
        self.assertEqual(hits(data, {value}), [
            dict(offset=1, byte_order='big', location_id=value),
            dict(offset=6, byte_order='little', location_id=value)])
        self.assertEqual(hits(value.to_bytes(4, 'big'), {value})[0]['offset'], 0)
        self.assertEqual(hits(value.to_bytes(4, 'big')[:3], {value}), [])
        self.assertEqual(hits((value+1).to_bytes(4, 'big'), {value}), [])

    def test_corrupt_packet_rejected(self):
        payload = dict(base64=base64.b64encode(b'abc').decode(), byte_length=3, sha256=sha(b'abc'))
        self.assertEqual(packet(payload), b'abc')
        with self.assertRaises(ValueError):
            packet(dict(payload, byte_length=4))
        with self.assertRaises(ValueError):
            packet(dict(payload, sha256=sha(b'abd')))

    def test_catalog_exact_names_unsigned_values_and_source_hash(self):
        folder = ROOT / 'docs/generated/missions/location-reconciliation'
        index = json.loads((folder/'catalog-index.json').read_text())
        source = ROOT/'docs/reference/missions/external-location-catalog/ACGEntrances.json'
        self.assertEqual(sha(source.read_bytes()), 'da64734fd544d93c3ccfb2ae56ad4248c18a101b86fed7e0deadc8f315d6c1c8')
        self.assertEqual(len(index), 2235)
        self.assertEqual(index['3221226127']['names'], ['Workers Flats'])
        self.assertEqual(index['3221226127']['signed'] & 0xffffffff, 3221226127)

    def test_artifact_hashes_and_every_offer(self):
        folder = ROOT/'docs/generated/missions/location-reconciliation'
        manifest = json.loads((folder/'artifact-manifest.json').read_text())
        for name, digest in manifest['files'].items():
            self.assertEqual(sha((folder/name).read_bytes()), digest, name)
        self.assertEqual(sha((ROOT/'Tools/reconcile_mission_locations.py').read_bytes()), manifest['generator_sha256'])
        seen = set()
        primary = []
        total = 0
        with gzip.open(folder/'all-offers.jsonl.gz', 'rt', encoding='utf-8') as stream:
            for line in stream:
                row = json.loads(line)
                key = row['cohort_id'], row['offer_index']
                self.assertNotIn(key, seen)
                seen.add(key)
                total += 1
                self.assertIsNone(row['selected_location_id'])
                for hit in row['chunk_catalog_hits'] + row['packet_catalog_hits']:
                    self.assertTrue(hit['equals_roll_terminal_instance'])
                if row['primary_level2']:
                    primary.append(row)
        self.assertEqual(total, 93185)
        self.assertEqual(len(primary), 270)
        requests = Counter(row['request_id'] for row in primary)
        self.assertEqual(len(requests), 54)
        self.assertEqual(set(requests.values()), {5})
        standalone = [json.loads(line) for line in (folder/'level2-offers.jsonl').read_text().splitlines()]
        self.assertEqual(primary, standalone)


if __name__ == '__main__':
    unittest.main()
