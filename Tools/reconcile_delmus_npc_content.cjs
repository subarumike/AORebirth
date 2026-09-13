// Deterministic, offline integration of a pinned reviewed content snapshot. No runtime activation.
const fs = require('fs'), path = require('path'), cp = require('child_process'), crypto = require('crypto');
const root = path.resolve(__dirname, '..');
const sourceSha = '8b9d36090986dc3ad89b9545a23ff38016c626ab';
const startingSha = '2dc701f1e114cdd26dbb1e13d89acd4aa3b77a76';
const read = p => JSON.parse(fs.readFileSync(path.join(root,p),'utf8').replace(/^\uFEFF/,''));
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex');
const git = (...args) => cp.execFileSync('git',args,{cwd:root,encoding:'utf8',maxBuffer:30e6});
const check = process.argv.includes('--check');
const outputs = [];
function write(p, value) {
  const text = typeof value === 'string' ? value : JSON.stringify(value,null,2)+'\n';
  outputs.push(p);
  if(check) { if(!fs.existsSync(path.join(root,p)) || fs.readFileSync(path.join(root,p),'utf8') !== text) throw Error('Stale generated artifact: '+p); }
  else { fs.mkdirSync(path.dirname(path.join(root,p)),{recursive:true}); fs.writeFileSync(path.join(root,p),text); }
}
const sourcePath = 'docs/accepted/npc/delmus/NpcTemplate.json';
for (const name of ['NpcTemplate.json','NpcFamilyStatTemplates.json','NpcStatTemplateOverlays.json']) {
  const snapshot = fs.readFileSync(path.join(root,'docs/accepted/npc/delmus',name),'utf8');
  if (snapshot.replace(/\r\n/g,'\n') !== git('show',sourceSha+':AORebirth/GameData/'+name).replace(/^\uFEFF/,'').replace(/\r\n/g,'\n'))
    throw Error('Reviewed source snapshot no longer matches pinned Delmus commit: '+name);
}
const templates = read(sourcePath);
const contractsPath = 'docs/generated/subway_enemy_combat_contracts.json';
const contracts = read(contractsPath);
const inventoryPath = 'docs/generated/capture_backed_npc_combat_inventory.json';
const inventory = read(inventoryPath);
const shardPath = 'docs/generated/playfields/placements/pf_127.json';
const shard = read(shardPath);
const familySource = read('docs/accepted/npc/delmus/NpcFamilyStatTemplates.json');
const overlaySource = read('docs/accepted/npc/delmus/NpcStatTemplateOverlays.json');
const baseline = JSON.parse(git('show',startingSha+':AORebirth/GameData/MobTemplates.json'));
const subway = templates.filter(t => Object.hasOwn(contracts,t.Name));
const weaponRows = [], npcRows = [], outputTemplates = baseline.filter(t=>t.Hash!=='AAAA');
const appearance = t => ({monsterData:t.Stats?.[359]??null,headMesh:t.Stats?.[64]??null,hasHeadMesh:t.HasHeadMesh,textures:t.Textures??null,scale:t.Stats?.[360]??null});
for(const t of templates) {
  const c = contracts[t.Name];
  // Name locates evidence; the exact MonsterData equality is required even for candidate comparison.
  // Neither equality is used to authorize an official hash placement.
  const comparison = c && c.monsterData.length===1 && c.monsterData[0]===t.Stats?.[359] ? c : null;
  const shapes = comparison?.equippedWeaponShapes ?? [];
  const variants = shapes.map(s=>({LowId:s.lowId,HighId:s.highId,Qualities:[s.quality],
    Evidence:s.captures.map(id=>contractsPath+'#'+t.Name+':'+id), Owners:s.owners}));
  const raw = t.Weapons??[];
  const counts = new Map();
  for(const w of raw) {const key=w.LowId+':'+w.HighId;counts.set(key,(counts.get(key)||0)+1);}
  const definitions = raw.map(w=>{
    const matches = shapes.filter(s=>s.lowId===w.LowId && s.highId===w.HighId);
    const classification = counts.get(w.LowId+':'+w.HighId)>1?'TRUE_DUPLICATE':matches.length
      ? raw.length>1?'QL_VARIANT':'VALID_VARIANT':'UNKNOWN';
    return {...w,classification,observedQualities:[...new Set(matches.map(s=>s.quality))].sort((a,b)=>a-b),
      captureEvidence:matches.flatMap(s=>s.captures),selection:matches.length?'EXACT_CAPTURE_QUALITY_ONLY':'BLOCKED',
      namesAndMeshes:'NOT_IN_TEMPLATE_WEAPON_RECORD; NO IDENTITY INFERENCE'};
  });
  const missing = definitions.filter(d=>d.classification==='UNKNOWN').length;
  const overlap = variants.filter((v,i)=>variants.some((w,j)=>i!==j&&v.Qualities[0]===w.Qualities[0]
    && (v.LowId!==w.LowId||v.HighId!==w.HighId))).length;
  const row = {hash:t.Hash,name:t.Name,sourceWeaponDefinitions:raw,definitions,
    capturedAlternatives:variants,unknownDefinitions:missing,overlappingQualityAlternatives:overlap,
    runtimeWeaponSelection:'BLOCKED_PENDING_EXACT_ACTOR_TO_PLACEMENT_LOADOUT',
    note:'Captured endpoint pairs are alternatives across actors/qualities, never inferred simultaneous slots. Single-ID endpoint observations are retained separately.'};
  if(c) weaponRows.push(row);
  const family = t.NpcFamily;
  const familyMissing = !Object.hasOwn(familySource,String(family));
  const profiles = inventory.profiles.filter(p=>p.metadata?.monsterData===t.Stats?.[359]
    && (p.metadata?.capturedRealmId===127 || p.metadata?.name===t.Name));
  if(c) npcRows.push({name:t.Name,hash:t.Hash,templateId:t.TemplateId||null,
    templateIdStatus:t.TemplateId?'SOURCE_SUPPLIED':'MISSING_TEMPLATE_ID',familyId:family,
    appearance:appearance(t),baseStatsSource:sourcePath+'#'+t.Hash,
    individualOverrides:t.Stats,overlayId:t.NpcStatTemplate,overlayValid:t.NpcStatTemplate===0,
    identityStatus:'UNRESOLVED_OFFICIAL_PLACEMENT_BRIDGE',familyStatus:familyMissing?'MISSING_FAMILY':'PARTIAL',
    combatEvidenceProfiles:profiles.map(p=>({profileKey:p.profileKey,status:p.status,variants:p.runtimeReadyVariantCount})),
    weapons:row,combatReady:false,blockers:['MISSING_EXACT_PLACEMENT_TEMPLATE_BRIDGE',
      'MISSING_AUTHORITATIVE_FAMILY_STATS','MISSING_EXACT_ACTOR_TO_PLACEMENT_LOADOUT',
      ...(t.TemplateId?[]:['MISSING_TEMPLATE_ID'])]});
  if(t.Hash!=='AAAA' && baseline.some(b=>b.Hash===t.Hash))continue; // preserve accepted pre-integration template
  const candidate=structuredClone(t);
  candidate.ContentAcceptance={Policy:'BLOCK_SPAWN',IdentityResolved:false,CombatAccepted:false,
    Attackable:false,CombatAiEnabled:false,UnresolvedPlaceholder:t.Hash==='AAAA',Source:sourceSha+':AORebirth/GameData/NpcTemplate.json',
    Blockers:t.Hash==='AAAA'?['UNRESOLVED_IDENTITY_PLACEHOLDER']:['MISSING_EXACT_PLACEMENT_TEMPLATE_BRIDGE','MISSING_AUTHORITATIVE_FAMILY_STATS','MISSING_EXACT_ACTOR_TO_PLACEMENT_LOADOUT']};
  candidate.Attackable=false;
  // NewEngine's Weapons list denotes concurrent slots. Preserve all source alternatives in evidence.
  candidate.Weapons=[];
  candidate.WeaponVariants=variants.map(({Owners,...v})=>v);
  outputTemplates.push(candidate);
}
outputTemplates.sort((a,b)=>a.Hash.localeCompare(b.Hash,'en'));
write('AORebirth/GameData/MobTemplates.json',outputTemplates);
const records=shard.Records;
if(!Array.isArray(records))throw Error('Official placement shard Records missing');
const mappings=records.map(r=>{
  const candidates=templates.filter(t=>t.Hash===r.CanonicalAcgHashText && t.Hash!=='AAAA');
  return {placement:r.OfficialSpawnRecordId,district:r.DistrictIndex,ordinal:r.DistrictRecordOrdinal,
    hash:r.CanonicalAcgHashText,position:[r.PositionX,r.PositionY,r.PositionZ],
    candidateTemplates:candidates.map(t=>({hash:t.Hash,name:t.Name,templateId:t.TemplateId||null})),
    resolvedTemplate:r.ResolvedMobTemplateHash||null,
    resolution:candidates.length>1?'AMBIGUOUS':r.ResolvedMobTemplateHash&&r.MobTemplateEvidenceSource?'RESOLVED':'UNRESOLVED',
    authorization:'BLOCKED',reason:'No new exact authorized identity/behavior/template bridge promoted by this integration.'};
});
const families=[...new Set(subway.map(t=>t.NpcFamily))].sort((a,b)=>a-b).map(id=>({familyId:id,
  npcs:subway.filter(t=>t.NpcFamily===id).map(t=>t.Name),baseFamilyDefined:Object.hasOwn(familySource,String(id)),
  runtimeFamilyDefined:false,overlayDefined:false,individualOverrides:true,
  status:Object.hasOwn(familySource,String(id))?'PARTIAL':'MISSING_FAMILY',
  evidenceGap:'Captured individual effective stats do not establish an accepted reusable family curve. No generic MMO derivation or sample-family substitution promoted.'}));
const reviewed=git('diff','--name-status',startingSha+'...'+sourceSha).trim().split('\n').map(line=>{
  const [status,p]=line.split('\t');
  let classification='UNRELATED',decision='SKIP',reason='Outside bounded NPC content/stat/cancellation integration.';
  if(/NpcTemplate|MobTemplates|NpcFamilyStatTemplates|NpcStatTemplateOverlays/.test(p)&&p.endsWith('.json')){classification='CONTENT_DATA';decision='RECONCILE';reason='Pinned source snapshot; pending templates blocked; sample curves not combat authority.';}
  else if(/Core\/Mobs\/(NpcFamilyStatTemplates|NpcStatTemplates|MobStatResolver)\.cs$/.test(p)){classification='GENERIC_RUNTIME_MECHANIC';decision='IMPORT_ADAPTED';reason='Stat composition only; remove missing-family fallback.';}
  else if(/Tests\//.test(p)){classification='TEST';reason='Use focused candidate tests; developer tests exercise a different runtime.';}
  else if(/Core\/(Nanos|Movement|Playfield|Entities|GameData|Network)|GameDataPaths|MobTemplates.cs/.test(p)){classification='CONFLICTING';reason='Preserve DAO, admission, accepted movement/arrival and content authority; adapt scoped mechanics only.';}
  else if(/GameData\/|\.g\.cs$/.test(p))classification='GENERATED_DATA';
  return {path:p,status,classification,decision,reason};
});
const evidence=[sourcePath,contractsPath,inventoryPath,shardPath,'docs/evidence/ZONEENGINE_NEW_NPC_ACTIVATION_INVENTORY.json',
  'docs/accepted/combat/enemy_combat_formula_packet_evidence.json'].map(p=>({path:p,sha256:hash(p)}));
const summary={startingSha,delmusSha:sourceSha,templatesFound:templates.length,subwayTemplatesFound:subway.length,
  subwayTemplatesImported:subway.length,placements:mappings.length,resolved:mappings.filter(r=>r.resolution==='RESOLVED').length,
  unresolved:mappings.filter(r=>r.resolution==='UNRESOLVED').length,ambiguous:mappings.filter(r=>r.resolution==='AMBIGUOUS').length,
  families:families.map(f=>f.familyId),familyComplete:0,familyMissing:families.filter(f=>f.status==='MISSING_FAMILY').length,
  weaponDefinitions:weaponRows.reduce((n,r)=>n+r.definitions.length,0),
  trueDuplicates:weaponRows.reduce((n,r)=>n+r.definitions.filter(d=>d.classification==='TRUE_DUPLICATE').length,0),
  knownWeaponVariants:weaponRows.reduce((n,r)=>n+r.definitions.filter(d=>['VALID_VARIANT','QL_VARIANT'].includes(d.classification)).length,0),
  unresolvedWeaponDefinitions:weaponRows.reduce((n,r)=>n+r.unknownDefinitions,0),combatReady:0,combatBlocked:subway.length,
  sourceFilesReviewed:reviewed.length,sourceFilesImported:reviewed.filter(r=>r.decision==='IMPORT_ADAPTED').length,
  sourceFilesReconciled:reviewed.filter(r=>r.decision==='RECONCILE').length,sourceFilesSkipped:reviewed.filter(r=>r.decision==='SKIP').length,
  pink:{AAAA_templateDefinitions:1,AAAA_liveSpawnCount:'NOT_MEASURED',missingVisualAssetCount:'UNPROVEN',otherCount:'UNPROVEN'}};
write('docs/reports/DELMUS_NPC_IMPORT_MATRIX.json',{summary,files:reviewed,evidence});
write('docs/reports/NPC_HASH_RESOLUTION_MATRIX.json',{sourceSha,evidence:shardPath,summary:{resolved:summary.resolved,unresolved:summary.unresolved,ambiguous:summary.ambiguous},placements:mappings});
write('docs/reports/NPC_FAMILY_STAT_COVERAGE.json',{sourceSha,composition:['family','overlay','individual'],sourceFamilyKeys:Object.keys(familySource),sourceOverlayKeys:Object.keys(overlaySource),families});
write('docs/reports/NPC_WEAPON_RECONCILIATION.json',{sourceSha,source:contractsPath,npcs:weaponRows});
write('docs/reports/SUBWAY_NPC_COMBAT_RECONCILIATION.json',{summary,evidence,npcs:npcRows});
const table=['| NPC | Hash | Family | Identity | Family stats | Overlay | Override | Weapon | Combat-ready |','| --- | --- | --- | --- | --- | --- | --- | --- | --- |',
 ...npcRows.map(r=>`| ${r.name} | ${r.hash} | ${r.familyId} | Unresolved placement bridge | Missing | None requested | Source retained; completeness unproven | Exact actor binding pending | NO |`)].join('\n');
write('docs/reports/SUBWAY_NPC_COMBAT_RECONCILIATION.md',`# Subway NPC combat reconciliation\n\n${subway.length} reviewed Subway templates imported as explicitly blocked content. No NPC-specific C# content was introduced.\n\n${table}\n\nThe separate accepted export verifies 322 Subway bindings. Those capture-owned actors are not a proven bridge to these official hash placements. Existing accepted content remains available unchanged.\n\nFamily IDs: ${summary.families.join(', ')}. All lack an accepted reusable family definition. The developer sample families (1, 10001) and overlay (1) are retained under docs/accepted/npc/delmus for review, not installed as arbitrary runtime defaults. Effective NPC snapshots are not silently converted into universal family curves.\n\nWeapons: ${summary.weaponDefinitions} source definitions; ${summary.knownWeaponVariants} matched to captured endpoint pairs; ${summary.unresolvedWeaponDefinitions} remain unknown. Captured quality/owner/capture associations are retained in NPC_WEAPON_RECONCILIATION.json. Multiple pairs are alternative observations, not simultaneous main/offhand weapons. Exact actor-to-placement loadout selection remains required.\n\nSee the JSON companion for every imported field, appearance, evidence profile, variant, source and blocker. Zero templates are claimed combat-accepted.\n`);
console.log(JSON.stringify(summary));
