import importlib.util
import json
import unittest
import subprocess
import re
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('retained_guard',ROOT/'Tools/retained_legacy_guard.py')
guard=importlib.util.module_from_spec(spec);spec.loader.exec_module(guard)
inventory=json.loads((ROOT/'docs/reports/NEWENGINE_RETAINED_LEGACY_IMPLEMENTATION_AUDIT.json').read_text())['known_retained_files']

class MutationTests(unittest.TestCase):
    def test_copied_locality_method_under_a_new_class_name_fails(self):
        audit=json.loads((ROOT/'docs/reports/NEWENGINE_RETAINED_LEGACY_IMPLEMENTATION_AUDIT.json').read_text())
        row=next(r for r in audit['retired_method_fingerprints'] if 'LocalityPolicy' in r['source_path'])
        source=subprocess.check_output(['git','show','4159a00c0e7c59f3139fa7ce3054d722f001e6a6:'+row['source_path']],cwd=ROOT).decode('utf-8-sig')
        source=source.replace('LocalityPolicy','NewSettingsResolver').replace('settings','options')
        self.assertTrue(any('near-copy' in r for r in guard.inspect_source('NewSettingsResolver.cs',source,[row])))
    def test_reintroduced_retained_path_fails(self):
        self.assertIn('retired inventory path',guard.inspect_source(inventory[0]['relocated_path'],'class Reintroduced {}',inventory))
    def test_embedded_content_table_fails(self):
        source='class Rules { static readonly int[] Rewards = { 1,2,3,4,5,6,7,8,9,10,11,12 }; }'
        self.assertTrue(any('literal table' in x for x in guard.inspect_source('NewName.cs',source,inventory)))
    def test_copied_gameplay_namespace_fails(self):
        self.assertIn('retired gameplay namespace',guard.inspect_source('NewName.cs','namespace ZoneEngine.Core.Missions { class Renamed {} }',inventory))
    def test_protocol_boilerplate_without_content_passes(self):
        self.assertEqual([],guard.inspect_source('Serializer.g.cs','namespace Protocol; class Serializer { public int Read(int value) => value; }',inventory))
    def test_renamed_copied_implementation_fails_after_namespace_and_identifier_changes(self):
        row=next(r for r in inventory if r['relocated_path'].endswith('/ChatCommandText.cs'))
        source=subprocess.check_output(['git','show','4159a00c0e7c59f3139fa7ce3054d722f001e6a6:'+row['relocated_path']],cwd=ROOT).decode('utf-8-sig')
        source=re.sub(r'namespace\s+[\w.]+', 'namespace Rewritten',source)
        source=source.replace('ChatCommandText','CleanParser').replace('message','requestPayload')
        self.assertTrue(any('near-copy implementation' in reason for reason in guard.inspect_source('CleanParser.cs',source,inventory)))

if __name__=='__main__':unittest.main()
