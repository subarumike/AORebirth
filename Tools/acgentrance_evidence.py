"""Address-backed interpretation metadata for the offline ACGEntrance extractor."""
import json
import pathlib
import re
import subprocess
from acgentrance_reconstruction import ROOT, OUT, CLIENT, PRIOR, PE, sha


INTERPRETATIONS = {
 ('Gamecode.dll',0x12231e):('PlayfieldAnarchy_t::CreateRDBDynels', 'Copies the stored complete identity for ordinary RDB placements, calls CreateFromTemplate, applies overrides, passes raw Vector3/Quaternion to AddChildDynel, casts Door_t and calls LinkDoorToRooms. Instanced/generated paths can use a separate identity map; do not claim all runtime identities always equal static placements.'),
 ('N3.dll',0x5494):('n3Dynel_t::GetDynel', 'Finds the complete Identity_t in the dynel map; no entrance-component bit extraction.'),
 ('N3.dll',0x3e99):('Identity_t ordered comparison', 'Compares both complete identity DWORDs; unsigned instance comparison preserves the C000 component.'),
 ('Gamecode.dll',0x800cb):('Door_t::LinkDoorToRooms', 'Reads stat 189, mode 2. Dungeon room-link endpoint can auto-register zero and write allocated key back. Non-dungeon path registers only positive values. Room lookup can fail and kill the door.'),
 ('N3.dll',0xd98c):('n3Playfield_t::RegisterEntranceDoor', 'Owner+0x48 holds entries {int key, full Identity_t}; owner+0x4c counter=max(counter,key+1); key zero gets counter++. Append without duplicate rejection.'),
 ('N3.dll',0xca9d):('n3Playfield_t::GetEntranceDoor', 'Linear exact signed-int key comparison, first duplicate wins; missing nonempty key falls back to first entry, empty/null returns zero Identity_t. No safe missing-key resolution by fallback.'),
 ('Gamecode.dll',0x3d90):('Beholder stat-getter thunk', 'Adjusts this+0x34 and jumps through embedded StatHolder vtable+4.'),
 ('Gamecode.dll',0x2e62b):('StatHolder_t getter', 'Reads array[stat] if in range; else 1234567890. Second parameter (mode) is ignored in this implementation; RET 8.'),
 ('Gamecode.dll',0x2e585):('StatHolder_t add/set slot', 'Grows the array with 1234567890 sentinels and writes a non-sentinel value.'),
 ('Gamecode.dll',0x7f113):('Door_t construction defaults', 'Virtual slot+0x44 initializes stat 0xBD to zero; this is not a positive unique registration key.'),
 ('Gamecode.dll',0x152559):('ACGEntrance factory registration', 'DbObject_t::Register(0xDAC6, creator RVA0xD213). Creator calls Door_t constructor RVA0x7F5E0.'),
 ('Gamecode.dll',0xcb232):('QuestAlternativeIIR_t stream reader', 'Reads inherited ACGQuest header, byte offer count (<6), full mission identity, Quest_t through RVA0xABEA7, final offer byte.'),
 ('Gamecode.dll',0xc9f49):('ACGQuestIIR_t inherited header reader', 'Reads version, difficulty, dimension sliders, seed, origin-kind, originator identity. This originator is the request terminal, not a destination.'),
 ('Gamecode.dll',0xabea7):('Quest_t stream reader', 'Accepts versions7..15; reads title/description, rewards, action array via 0xACCDF and versioned trailing fields.'),
 ('Gamecode.dll',0xaccdf):('Quest action array reader', 'Validates positive 0x3F1 count encoding; allocates 0x8C action objects, calls RVA0xACBA0.'),
 ('Gamecode.dll',0xacba0):('QuestAction WorldPos reader', 'Reads action fields then GameData::operator>>(WorldPos_c&) into action+0x6C. No entrance-registry lookup here.'),
 ('GameData.dll',0xca52):('GameData WorldPos_c stream reader', 'Reads Identity_t, two signed int32 offsets, local float32 XYZ. WorldXYZ=float32(localXYZ+float32(intX,0,intZ)); stored separately.'),
 ('GameData.dll',0xc7d2):('WorldPos_c::GetLocalPos', 'Returns this+0x14, the raw local float32 XYZ read from the packet.'),
 ('GameData.dll',0xc7ce):('WorldPos_c::GetWorldPos', 'Returns this+8, computed global-world XYZ, not the raw local packet vector.'),
 ('Gamecode.dll',0x1abc9):('N3Msg_GetQuestWorldPos', 'Accepted quest accessor copies first action WorldPos identity, world vector and local vector; no GetEntranceDoor call.'),
 ('N3.dll',0xd415):('n3Playfield_t::AddChildDynel', 'Calls UpdateWhere(playfield instance, unchanged Vector3, unchanged Quaternion).'),
 ('N3.dll',0x52ad):('n3Dynel_t::UpdateWhere', 'Sets playfield, attaches playfield parent and calls Vehicle_t::SetRelPosRot with the passed vectors unchanged.'),
}


def repository_provenance():
    def git(*args, cwd=ROOT):
        return subprocess.check_output(['git',*args],cwd=cwd).decode('utf-8').strip()
    primary=pathlib.Path(r'C:\Users\Mike\Documents\AORebirth')
    value={'snapshot_stage':'DEDICATED_WORKTREE_PRECOMMIT; not a claim that this inventory was captured before worktree creation',
        'primary_worktree':str(primary),'primary_head':git('rev-parse','HEAD',cwd=primary),
        'primary_status':git('status','--short','--branch',cwd=primary),
        'dedicated_worktree':str(ROOT),'dedicated_branch':git('branch','--show-current'),
        'dedicated_starting_sha':'a9da4fc0dee664e43cebdbf5c0a9f2afe51f1e0c',
        'prior_reconciliation_sha':git('rev-parse','5a802fe6^{commit}'),
        'remote_containing_prior_commit':git('branch','-r','--contains','5a802fe6'),
        'origin_master':git('rev-parse','origin/master'),
        'registered_worktrees':git('worktree','list','--porcelain'),
        'relevant_refs':[line for line in git('for-each-ref','--format=%(refname) %(objectname)').splitlines() if any(word in line.lower() for word in ('mission','ghidra','reconstruction'))]}
    from acgentrance_reconstruction import write_json
    target=OUT/'acgentrance-repository-provenance.json'
    if target.exists():
        raise ValueError('Repository provenance snapshot already exists; preserve it')
    write_json(target,value)
    print('REPOSITORY_PROVENANCE=PASS')


def compact_native(path):
    """Retain address/byte windows, xrefs and calls, not raw decompiler dumps."""
    text=path.read_text(encoding='utf-8')
    if 'Evidence format: COMPACT_ADDRESS_BYTE_WINDOWS_V1' in text:
        return
    private=ROOT/'tools-temp/acgentrance-analysis/full-function-exports'/path.name
    private.parent.mkdir(parents=True,exist_ok=True)
    private.write_bytes(path.read_bytes())
    parts=text.split('## Target RVA ')
    result=[parts[0].rstrip(), 'Evidence format: COMPACT_ADDRESS_BYTE_WINDOWS_V1',
        'Full private analysis export SHA256: '+sha(private),
        'Full decompiler text intentionally excluded. Small functions retain all instructions; larger functions retain calls/branches and nearby address-byte windows.']
    for section in parts[1:]:
        lines=section.splitlines()
        result.extend(['', '## Target RVA '+lines[0]])
        result.extend(line for line in lines if line.startswith(('XREF ','Function:','FUNCTION_UNAVAILABLE')))
        asm=[line for line in lines if re.match(r'^[0-9a-f]{8} [0-9a-f]+ ',line)]
        selected=set()
        if len(asm)<=80:
            selected.update(range(len(asm)))
        else:
            selected.update(range(min(6,len(asm))))
            selected.update(range(max(0,len(asm)-6),len(asm)))
            for i,line in enumerate(asm):
                if 'CALL ' in line or re.search(r' (?:J[A-Z]+|CMP|TEST) ',line) or any(v in line for v in ('0xbd','0xdac6','0x499602d2')):
                    selected.update(range(max(0,i-2),min(len(asm),i+3)))
        result.append('ASSEMBLY ADDRESS/BYTE WINDOWS:')
        prev=-1
        for i in sorted(selected):
            if i>prev+1:
                result.append('[non-selected instructions omitted]')
            result.append(asm[i])
            prev=i
    path.write_text('\n'.join(result)+'\n',encoding='utf-8',newline='\n')


def compact_all():
    for path in sorted(OUT.glob('native-*.txt')):
        compact_native(path)
    print('COMPACT_NATIVE_EVIDENCE=PASS')


def reference_manifest(verify=False):
    target = OUT/'acgentrance-reference-input-manifest.json'
    if verify:
        old = json.loads(target.read_text())
        for row in old['files']:
            if row['status']=='HASHED' and sha(row['path'])!=row['sha256']:
                raise ValueError('REFERENCE_INPUT_CHANGED: '+row['path'])
        for row in json.loads((OUT/'client-module-import-coverage.json').read_text()):
            if sha(row['path'])!=row['sha256']:
                raise ValueError('REFERENCE_DLL_CHANGED: '+row['path'])
        print('REFERENCE_INPUTS_BYTE_IDENTICAL=PASS')
        return
    paths = sorted(PRIOR.glob('*.md')) + [CLIENT/'decompile_report/_intermediate/gamecode_objdump_d_intel.txt']
    rows = []
    for p in paths:
        rows.append({'path':str(p), 'filename':p.name, 'size':p.stat().st_size if p.exists() else None,
            'sha256':sha(p) if p.exists() else None, 'status':'HASHED' if p.exists() else 'UNAVAILABLE',
            'role':'PRIOR_REVERSE_ENGINEERING_REFERENCE_ONLY'})
    value = {'files':rows, 'capture_time_note':'Main DLL/database hashes were taken before task analysis. Supplemental reference hashes were recorded during analysis and rechecked before handoff.',
        'source_ghidra_projects':'Existing copied programs were newer than available Ghidra; fresh private imports were used. No source projects analyzed in place.'}
    if target.exists() and json.loads(target.read_text())!=value:
        raise ValueError('Reference baseline differs; do not overwrite')
    from acgentrance_reconstruction import write_json
    write_json(target,value)
    print('REFERENCE_MANIFEST=PASS')


def generate(json_file):
    functions = {}
    for path in sorted(OUT.glob('native-*.txt')):
        text = path.read_text()
        module = re.search(r'^Program: (.+)$',text,re.M)[1]
        digest = re.search(r'^Executable SHA256: (.+)$',text,re.M)[1]
        if digest!=sha(CLIENT/module):
            raise ValueError('Native evidence binary SHA mismatch')
        pe = PE(CLIENT/module)
        exports = {int(e['rva'],16):e['name'] for e in pe.exports()}
        imports = {pe.base+r['iat_rva']:r['name'] for r in pe.imports()}
        for section in text.split('## Target RVA ')[1:]:
            rva = int(section.splitlines()[0],16)
            key = (module,rva)
            if key in functions:
                continue
            name_match = re.search(r'^Function: (.+) @ ([0-9a-f]+)$',section,re.M)
            if name_match is None:
                continue
            proposed, explanation = INTERPRETATIONS.get(key,(name_match[1],'Targeted function retained as supporting evidence; no additional semantic claim.'))
            funcs = sorted(set(re.findall(r'CALL (0x[0-9a-f]+)',section)))
            imported = sorted({imports[int(addr,16)] for addr in re.findall(r'CALL dword ptr \[(0x[0-9a-f]+)\]',section) if int(addr,16) in imports})
            functions[key] = {'module':module,'binary_sha256':digest,'image_base':f'0x{pe.base:08X}',
                'rva':f'0x{rva:08X}','absolute_address':f'0x{pe.base+rva:08X}',
                'proposed_function_name':proposed,'original_export_name':exports.get(rva),
                'ghidra_function_name':name_match[1], 'callers_and_references':re.findall(r'^XREF (.+)$',section,re.M),
                'direct_callee_addresses':funcs,'imported_callees':imported,
                'structure_offsets_and_pseudocode':explanation,
                'evidence_classification':'PROVEN_FROM_MODERN_CLIENT_CODE',
                'source_evidence_path':path.name,'source_evidence_sha256':sha(path),
                'ghidra_project':f'tools-temp/acgentrance-analysis/ghidra/AcgFresh{module.replace(".","")}.gpr',
                'program':module,'tool':'Ghidra 12.1.3 PUBLIC; fresh matching binary import; private project'}
    json_file('acgentrance-ghidra-function-map.json',list(functions[k] for k in sorted(functions)))
    json_file('acgentrance-record-layout.json',{
        'parent_resource_type':1000026, 'container':'LE uint32 record count; repeated LE uint32 byte_length followed by exactly byte_length bytes',
        'record_length_rule':'64 + override_length; reject truncation, mismatched count, nonfinite transform and unparsed container tail',
        'fields':[{'offset':o,'width':w,'format':f,'name':n} for o,w,f,n in [
            (0,4,'LE uint32','identity_type'),(4,4,'LE uint32','identity_instance'),(8,4,'LE uint32','unknown/version'),
            (12,8,'raw 2 DWORDs','coordinate ownership identity'),(20,4,'LE uint32','explicit_playfield_id'),
            (24,12,'LE IEEE754 binary32 XYZ','placement position'),(36,16,'LE 4 binary32','Quaternion_t raw order'),
            (52,4,'LE DWORD','unknown; not assigned template-identity semantics'),(56,4,'LE uint32','template resource instance'),
            (60,4,'LE uint32','override byte length'),(64,None,'BE structured flat content','override data')]],
        'template_resource_type':1000020,'template':'LE type DWORD and section count; sections 15 stats,21 name,20 animation/sound,22 actions',
        'override':'BE header three DWORDs, section count at12; 15 stats,21 name,2 events; all payload boundaries checked',
        'counts':'(n+1)*1009 for encoded section element counts', 'name':'section21 subtype33; uint16 name length and description length; exact bytes; no terminator assumed',
        'trailer':'empty or one zero DWORD only', 'scale':'Not present in the extracted placement structure; null',
        'reader_reused':'Tools/export_pf1931_dungeon_geometry.py::RdbReader',
        'repository_structural_support':['Tools/Algorithman/Extractor Serializer/PlayfieldParser.cs','Tools/Algorithman/Extractor Serializer/Structs/PFCoordHeading.cs','Tools/Algorithman/Extractor Serializer/NewParser.cs'],
        'native_support':'Gamecode.dll RVA0x12231E,0x2B458,0x2E7D9; acgentrance-ghidra-function-map.json'})
    json_file('acgentrance-registry-scope.json',{
        'owner':'n3Playfield_t loaded object instance','vector_pointer_offset':'0x48','counter_offset':'0x4C','entry_stride':12,
        'entry_fields':['int32 operational key','UInt32 Identity_t.type','UInt32 Identity_t.instance'],
        'constructor_rva':'N3.dll 0xE09B','cleanup_rva':'N3.dll 0xE1FF',
        'registration_rva':'N3.dll 0xD98C','lookup_rva':'N3.dll 0xCA9D',
        'registration_calls':'Gamecode.dll Door_t::LinkDoorToRooms 0x800CB (dungeon boundary and positive non-dungeon stat paths)',
        'lookup_calls':'No static local-code xrefs in N3 and no GetEntranceDoor imports in inspected client DLL inventory. Dynamic lookup/uninspected executables not excluded.',
        'lifetime':'Constructor initializes pointer null and counter1; field allocated by scene setup and freed on destruction. Not a global key namespace.',
        'duplicate_keys':'Appended, not rejected; lookup first equal key wins',
        'absent_key':'First identity if nonempty; zero identity if empty/null; forbidden as evidence-resolution fallback',
        'static_crosswalk_limit':'No loaded owner instance, no proven per-placement final positive key. Null crosswalk is retained rather than deriving keys from identity bits.',
        'symbolic_name':{'status':'SYMBOLIC_NAME_SUPPORTED','name':'ExitInstance',
            'support':['AORebirth/Libraries/Source/AORebirth.Enums/StatIds.cs:792','AORebirth/Libraries/Source/AORebirth.Stats/StatNamesDefaults.cs:261'],
            'client_symbol_proof':None}})
    json_file('acgentrance-coordinate-model.json',{
        'matching_rule':'SAME_EXPLICIT_PLAYFIELD_AND_ALL_THREE_IDENTICAL_LOCAL_IEEE754_BINARY32_COMPONENTS',
        'axis_conversion':'Identity XYZ; no AO-to-Godot transform; no coordinate permutation, rounding or nearest-neighbor radius',
        'units':'AO client local coordinate units on both sides; no physical-meter claim required or made',
        'placement_path':['Gamecode.dll CreateRDBDynels RVA0x12231E passes Vector3_t','N3.dll AddChildDynel RVA0xD415','N3.dll UpdateWhere RVA0x52AD -> Vehicle_t::SetRelPosRot'],
        'mission_path':['Gamecode.dll QuestAlternative parser RVA0xCB232','Quest_t reader RVA0xABEA7','action-array RVA0xACCDF','action reader RVA0xACBA0 -> action+0x6C WorldPos_c','GameData.dll reader RVA0xCA52 local vector at WorldPos+0x14','GameData.dll GetLocalPos RVA0xC7D2'],
        'world_conversion':'The separate world vector adds the two signed packet integer offsets to local X/Z and stores float32. Never compare this world vector directly to the local placement vector.',
        'quantization':'Packet local vector is raw binary32; placement local vector is raw binary32; matching tolerance is exactly zero bits',
        'corroboration':'All 93185 normalized offer locations match one local placement exactly; all 92830 raw-backed records are independently byte-verified. 355 normalized-only rows remain unpromoted.',
        'semantic_boundary':'An exact match identifies the client catalog placement advertised as mission destination. It does not establish an interaction radius, room link, physical collision plane, statel index, or positive operational entrance key.',
        'rotation':'Raw four-component Quaternion_t retained. Full rotation convention/scale is not needed for XYZ match and is not invented.',
        'evidence_classification':['PROVEN_FROM_MODERN_CLIENT_CODE','PROVEN_FROM_MODERN_CLIENT_RESOURCE','SUPPORTED_BY_EXISTING_CAPTURE']})
    json_file('mission-offer-destination-field-map.json',{
        'packet_type':'QuestAlternative 0x5C436609','byte_order':'big endian','opcode_absolute_packet_offset':16,
        'parser_module':'Gamecode.dll','parser_rva':'0xCB232','offer_reader_rva':'0xABEA7',
        'decoder_scope':'Exact captured-schema-assisted verification. Opaque AOSharp spans must match byte-for-byte; not a general independent Quest_t implementation.',
        'per_offer_boundaries':'First offer at51; subsequent starts follow parsed previous offer end. Variable description, rewards and captured opaque sections; count byte at50.',
        'fields':[
            {'name':'request_terminal_identity','offset':42,'width':8,'wire':'UInt32 type,UInt32 instance','object':'ACGQuestIIR_t+0x3C/+0x40','classification':'PROVEN_TERMINAL_IDENTITY'},
            {'name':'destination_playfield','offset':'D (recorded per offer)','width':8,'wire':'UInt32 type,UInt32 instance','object':'QuestAction+0x6C, WorldPos+0','classification':'PROVEN_DESTINATION_PLAYFIELD'},
            {'name':'world_origin_xz','offset':'D+8','width':8,'wire':'2 signed int32','object':'temporary integers, used to compute WorldPos+8 world vector','classification':'PROVEN_DESTINATION_COORDINATE','prior_name':'UnkChunk5Base64; NOT stat 0xBD'},
            {'name':'local_position_xyz','offset':'D+16','width':12,'wire':'3 IEEE754 binary32','object':'QuestAction+0x80, WorldPos+0x14','classification':'PROVEN_DESTINATION_COORDINATE'},
            {'name':'description','offset':'per-offer +60','width':'preceding BE uint32 length','wire':'byte string; raw retained by original capture','classification':'CANDIDATE_DESTINATION_FIELD','note':'May contain generic words, approximate coordinate text or a location name; not an identity resolver'},
            {'name':'operational_entrance_key','offset':None,'width':None,'wire':None,'classification':'UNKNOWN'},
            {'name':'complete_acgentrance_identity','offset':None,'width':None,'wire':None,'classification':'UNKNOWN'}],
        'destination_accessor':'Gamecode.dll N3Msg_GetQuestWorldPos RVA0x1ABC9 (accepted quest accessor); selection list RVA0x2065E',
        'GetEntranceDoor_mission_call':'Not found in recovered action/WorldPos/accessor paths; import/xref negative scope retained separately. Not a proof about arbitrary dynamic calls.',
        'name_encoding':'Resource bytes preserved as Latin-1 identity mapping; raw bytes authoritative. Packet strings not used as assignment keys.'})
