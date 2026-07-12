#!/usr/bin/env python3
import argparse,csv,hashlib,json,re,sys,zipfile
import xml.etree.ElementTree as ET
from collections import Counter,defaultdict
from pathlib import Path
from extract_enemy_catalog import FIELDS

VERSION="1.0.0"; SOURCE_ID="SRC-DYNA-ODT-001"
ALIASES={"blubbags":("blubbag","SINGULAR_PLURAL_VARIANT"),"fleas":("flea","SINGULAR_PLURAL_VARIANT"),"leets":("leet","SINGULAR_PLURAL_VARIANT"),"androids":("android","SINGULAR_PLURAL_VARIANT"),"hounds":("hound","SINGULAR_PLURAL_VARIANT"),"rollerrats":("rollerrat","SINGULAR_PLURAL_VARIANT"),"mantezes":("manteze","SINGULAR_PLURAL_VARIANT"),"mechdogs":("mechdog","SINGULAR_PLURAL_VARIANT"),"anuns":("anun","SINGULAR_PLURAL_VARIANT"),"rhinomen":("rhinoman","SPELLING_VARIANT"),"hammerbeasts":("hammerbeast","EXACT_NORMALIZATION"),"scorpiods":("scorpiod","SINGULAR_PLURAL_VARIANT"),"shadowmutants":("shadowmutant","EXACT_NORMALIZATION"),"snakes":("snake","SINGULAR_PLURAL_VARIANT"),"spiders":("spider","SINGULAR_PLURAL_VARIANT"),"pit lizards":("pit lizard","SINGULAR_PLURAL_VARIANT"),"ninjadroids":("ninja droid","SPELLING_VARIANT"),"bileswarms":("bileswarm","SINGULAR_PLURAL_VARIANT"),"mantises":("mantis","SINGULAR_PLURAL_VARIANT"),"cyborgs":("cyborg","SINGULAR_PLURAL_VARIANT"),"enigmas":("enigma","SINGULAR_PLURAL_VARIANT"),"nighthowler":("nighthowler","EXACT_NORMALIZATION"),"pareet":("reet","DOCUMENTED_ALIAS"),"pareets":("reet","DOCUMENTED_ALIAS"),"hammerbeast":("hammerbeast","POSSIBLE_VARIANT"),"manteze":("manteze","DISTINCT_VARIANT_RETAINED"),"mantis":("mantis","DISTINCT_VARIANT_RETAINED")}
PFIDS={"Aegean":585,"Belial Forest":605,"Newland Desert":565,"Omni Forest":716,"Varmint Woods":600,"Wailing Wastes":551}
def clean(s): return " ".join((s or "").split())
def normalize(s):
 k=re.sub(r"[^a-z0-9]+"," ",clean(s).lower()).strip(); return ALIASES.get(k,(k[:-1] if k.endswith("s") and not k.endswith("ss") else k,"EXACT_NORMALIZATION"))
def parse_level(text):
 t=clean(text); nums=[int(x) for x in re.findall(r"\d+",t)];
 if not nums:return None,None
 return min(nums),max(nums)
def parse(path):
 raw=Path(path).read_bytes(); root=ET.fromstring(zipfile.ZipFile(path).read("content.xml")); ns={"table":"urn:oasis:names:tc:opendocument:xmlns:table:1.0"}; rows=[]; source_row=0
 for tr in root.findall(".//table:table-row",ns):
  cells=[]
  for cell in tr.findall("table:table-cell",ns):
   repeated=int(cell.attrib.get("{urn:oasis:names:tc:opendocument:xmlns:table:1.0}number-columns-repeated","1")); value=clean("".join(cell.itertext())); cells.extend([value]*repeated)
  if not any(cells) or clean(cells[0])=="Monster Type":continue
  source_row+=1
  if len(cells)<5 or not clean(cells[0]): raise ValueError(f"Malformed ODT table row {source_row}: {cells}")
  family,boss,mobs,zone,coord=map(clean,cells[:5]); normalized,relationship=normalize(family); xy=[int(x) for x in re.findall(r"\d+",coord)];
  if len(xy)!=2: raise ValueError(f"Invalid coordinate in source row {source_row}: {coord}")
  boss_nums=[int(x) for x in re.findall(r"\d+",boss)];
  if len(boss_nums)!=1: raise ValueError(f"Invalid boss level in source row {source_row}: {boss}")
  no_mobs=mobs.lower()=="(no mobs)"; mn,mx=(None,None) if no_mobs else parse_level(mobs)
  rows.append({"SourceRowNumber":source_row,"SourceFamilyName":family,"NormalizedFamilyName":normalized,"CanonicalFamilyKey":"family-"+normalized.replace(" ","-"),"AliasOrVariantRelationship":relationship,"Zone":zone,"PlayfieldName":zone,"PlayfieldId":PFIDS.get(zone),"CoordinateX":xy[0],"CoordinateY":xy[1],"CoordinateText":coord,"ApproximateBossLevel":boss_nums[0],"BossLevelDescription":f"Approximate boss level {boss_nums[0]}","MinionMinimumLevel":mn,"MinionMaximumLevel":mx,"MinionLevelDescription":mobs,"BossOnlyCamp":no_mobs,"NoMobs":no_mobs,"CampNotes":"","SourceId":SOURCE_ID,"EvidenceConfidence":"COMMUNITY_DOCUMENTED","SpawnEvidenceType":"DOCUMENTED_STATIC_SPAWN","StaticOrDynamic":"Static","NormalizationNotes":relationship,"UnresolvedFields":[] if zone in PFIDS else ["PlayfieldId"]})
 return rows,{"SourceId":SOURCE_ID,"OriginalFilename":Path(path).name,"OriginalPathAtImport":str(Path(path).resolve()),"Sha256":hashlib.sha256(raw).hexdigest(),"FileSize":len(raw),"SourceTitle":"Dyna Boss List 1","SourceType":"COMMUNITY_MAINTAINED_ODT","ImportDate":"2026-07-12","ParserVersion":VERSION,"RowCount":len(rows)}
def write(rows,meta,out):
 src=out/"sources"; src.mkdir(parents=True,exist_ok=True); (src/"dyna_boss_list_1.normalized.json").write_text(json.dumps({"metadata":meta,"rows":rows},indent=2,sort_keys=True)+"\n",encoding="utf-8")
 cols=list(rows[0]);
 with (src/"dyna_boss_list_1.normalized.csv").open("w",newline="",encoding="utf-8") as f:w=csv.DictWriter(f,fieldnames=cols);w.writeheader();w.writerows(rows)
 aliases=[]
 for name in sorted({r["SourceFamilyName"] for r in rows},key=str.lower):
  n,rel=normalize(name); aliases.append({"SourceName":name,"NormalizedName":n,"CanonicalFamily":"family-"+n.replace(" ","-"),"RelationshipType":rel,"EvidenceSource":SOURCE_ID,"Confidence":"COMMUNITY_DOCUMENTED","Notes":"Hammerbeast and Manteze relationships intentionally retained as ambiguous/distinct where applicable."})
 with (out/"enemy_aliases.csv").open("w",newline="",encoding="utf-8") as f:w=csv.DictWriter(f,fieldnames=list(aliases[0]));w.writeheader();w.writerows(aliases)
def merge(rows,meta,out):
 cat=json.loads((out/"enemy_catalog.json").read_text(encoding="utf-8")); byfamily=defaultdict(list)
 for r in cat:
  for key in {normalize(r.get("EnemyFamily") or "")[0],normalize(r.get("DisplayName") or "")[0]}:
   if key: byfamily[key].append(r)
 keys={r["CanonicalEnemyKey"] for r in cat}
 for family in sorted({r["NormalizedFamilyName"] for r in rows}):
  if "family-"+family.replace(" ","-") not in keys:
   cat.append({"CanonicalEnemyKey":"family-"+family.replace(" ","-"),"MonsterDataId":None,"TemplateId":None,"DynelType":"COMMUNITY_DOCUMENTED_FAMILY","DisplayName":family.title(),"InternalName":None,"EnemyFamily":family,"EnemyArchetype":"Rubi-Ka dyna family","Faction":None,"Side":None,"ExpansionOrDataset":"Classic Rubi-Ka","IsAttackable":True,"IsHostileByDefault":None,"IsBoss":False,"IsUnique":False,"IsQuestEnemy":False,"IsMissionOnly":False,"IsPetLike":False,"IsTurret":False,"MinimumLevel":None,"MaximumLevel":None,"ExactLevel":None,"LevelSource":SOURCE_ID,"PlayfieldIds":[],"PlayfieldNames":[],"ZoneNames":[],"DungeonNames":[],"SpawnDefinitionIds":[],"SpawnCoordinatesWhenAvailable":[],"WeaponTemplateIds":[],"WeaponNames":[],"WeaponCategories":["UNKNOWN"],"VisibleWeapon":None,"AttackTemplateIds":[],"AttackTypes":[],"DamageTypes":[],"AttackRange":None,"AttackTime":None,"RechargeTime":None,"SpecialAttacks":[],"NanoProgramIds":[],"NanoProgramNames":[],"SourceRecordIds":[],"SourceDatFiles":[],"EvidenceQuality":"COMMUNITY_DOCUMENTED","UnresolvedFields":["RDB mapping","attack category"],"RdbPayloadSize":None,"RdbPayloadHash":None,"RecordClassification":"LIKELY_ENEMY_TYPE","InternetEvidence":SOURCE_ID})
 cat.sort(key=lambda x:x["CanonicalEnemyKey"]);(out/"enemy_catalog.json").write_text(json.dumps(cat,indent=2,sort_keys=True)+"\n",encoding="utf-8")
 with (out/"enemy_catalog.csv").open("w",newline="",encoding="utf-8") as f:
  w=csv.DictWriter(f,fieldnames=FIELDS);w.writeheader()
  for r in cat:w.writerow({k:(json.dumps(r.get(k),sort_keys=True) if isinstance(r.get(k),(list,dict)) else r.get(k)) for k in FIELDS})
 unresolved=[{"CanonicalEnemyKey":r["CanonicalEnemyKey"],"SourceRecordIds":r.get("SourceRecordIds",[]),"UnresolvedFields":r.get("UnresolvedFields",[])} for r in cat if r.get("UnresolvedFields")];(out/"enemy_catalog_unresolved.json").write_text(json.dumps(unresolved,indent=2,sort_keys=True)+"\n",encoding="utf-8")
 sources=json.loads((out/"enemy_catalog_sources.json").read_text(encoding="utf-8"));sources["DynaOdtSource"]=meta;(out/"enemy_catalog_sources.json").write_text(json.dumps(sources,indent=2,sort_keys=True)+"\n",encoding="utf-8")
 ledger=json.loads((out/"enemy_research_progress.json").read_text(encoding="utf-8"));
 for entry in ledger:
  n=normalize(entry.get("candidate_name") or "")[0]; hits=[r for r in rows if r["NormalizedFamilyName"] and r["NormalizedFamilyName"] in n]
  if hits: entry.setdefault("dataset_matches",[]).append(SOURCE_ID);entry.setdefault("web_sources_found",[]).append(SOURCE_ID);entry["fields_resolved"]=sorted(set(entry.get("fields_resolved",[])+["DynaCampEvidence"]))
 (out/"enemy_research_progress.json").write_text(json.dumps(ledger,indent=2,sort_keys=True)+"\n",encoding="utf-8")
 zone=out/"enemy_zone_associations.csv"; existing=[r for r in csv.DictReader(zone.open(encoding="utf-8")) if r.get("Source")!=SOURCE_ID]; cols=list(existing[0]) if existing else ["CanonicalEnemyKey","EnemyName","Zone","PlayfieldId","Source","Confidence","Notes"]
 extra=[]
 for r in rows: extra.append({"CanonicalEnemyKey":r["CanonicalFamilyKey"],"EnemyName":r["NormalizedFamilyName"],"Zone":r["Zone"],"PlayfieldId":r["PlayfieldId"] or "","Source":SOURCE_ID,"Confidence":"COMMUNITY_DOCUMENTED","Notes":f"DOCUMENTED_STATIC_SPAWN {r['CoordinateText']} approximate boss {r['ApproximateBossLevel']}; minions {r['MinionLevelDescription']}"})
 with zone.open("w",newline="",encoding="utf-8") as f:w=csv.DictWriter(f,fieldnames=cols);w.writeheader();w.writerows(existing+extra)
 summary=out/"enemy_catalog_summary.md";base=summary.read_text(encoding="utf-8").split("\n## ODT Dyna Camp Import",1)[0].rstrip();base+=f"\n\n## ODT Dyna Camp Import\n\n- Rows imported: {len(rows)}\n- Exact source family labels: {len({r['SourceFamilyName'] for r in rows})}\n- Normalized families: {len({r['NormalizedFamilyName'] for r in rows})}\n- Zones: {len({r['Zone'] for r in rows})}\n- Coordinates: {sum(bool(r['CoordinateText']) for r in rows)}\n- Approximate boss levels: {sum(r['ApproximateBossLevel'] is not None for r in rows)}\n- Minion-level rows: {sum(r['MinionMinimumLevel'] is not None for r in rows)}\n- Boss-only camps: {sum(r['BossOnlyCamp'] for r in rows)}\n- Confidence: COMMUNITY_DOCUMENTED";summary.write_text(base+"\n",encoding="utf-8")
 print(json.dumps({"rows":len(rows),"families":len({r['SourceFamilyName'] for r in rows}),"zones":len({r['Zone'] for r in rows}),"boss_only":sum(r['BossOnlyCamp'] for r in rows)}))
def main():
 ap=argparse.ArgumentParser();ap.add_argument("--source");ap.add_argument("--output",required=True);a=ap.parse_args();out=Path(a.output)
 if a.source: rows,meta=parse(a.source);write(rows,meta,out)
 else:
  doc=json.loads((out/"sources/dyna_boss_list_1.normalized.json").read_text(encoding="utf-8"));rows,meta=doc["rows"],doc["metadata"]
 merge(rows,meta,out)
if __name__=="__main__":main()
