#!/usr/bin/env python3
import argparse, csv, json, os, re, subprocess, sys
from collections import Counter, defaultdict
from pathlib import Path

VERSION = "1.0.0"
NPC_TEMPLATE_TYPE = 1040023
PLAYFIELD_TYPE = 1000001
ZONE_TYPES = {1000014: "District", 1000029: "Area", 1000026: "Statel"}
FIELDS = ["CanonicalEnemyKey","MonsterDataId","TemplateId","DynelType","DisplayName","InternalName","EnemyFamily","EnemyArchetype","Faction","Side","ExpansionOrDataset","IsAttackable","IsHostileByDefault","IsBoss","IsUnique","IsQuestEnemy","IsMissionOnly","IsPetLike","IsTurret","MinimumLevel","MaximumLevel","ExactLevel","LevelSource","PlayfieldIds","PlayfieldNames","ZoneNames","DungeonNames","SpawnDefinitionIds","SpawnCoordinatesWhenAvailable","WeaponTemplateIds","WeaponNames","WeaponCategories","VisibleWeapon","AttackTemplateIds","AttackTypes","DamageTypes","AttackRange","AttackTime","RechargeTime","SpecialAttacks","NanoProgramIds","NanoProgramNames","SourceRecordIds","SourceDatFiles","EvidenceQuality","UnresolvedFields"]

def printable_name(strings):
    candidates = [s.strip() for s in strings if re.search(r"[A-Za-z]", s) and len(s.strip()) <= 160]
    plain = [s for s in candidates if re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9 '\-_.:/()]+", s)]
    return (plain or candidates or [None])[0]

def family(name):
    if not name: return None
    value = re.sub(r"[^a-z0-9]+", " ", name.lower()).strip()
    return value.split()[-1] if value else None

def build_catalog(dump, source_files):
    by_type = {x["type"]: x for x in dump["record_types"]}
    playfields = {r["id"]: printable_name(r["strings"]) for r in by_type.get(PLAYFIELD_TYPE,{"records":[]})["records"]}
    zones = defaultdict(list)
    for tid, label in ZONE_TYPES.items():
        for r in by_type.get(tid,{"records":[]})["records"]:
            zones[r["id"]].append((label, " | ".join(r["strings"])))
    catalog=[]
    for record in by_type.get(NPC_TEMPLATE_TYPE,{"records":[]})["records"]:
        name=printable_name(record["strings"]); fam=family(name)
        pfids=[]; znames=[]
        if fam and len(fam) >= 4:
            pattern=re.compile(r"\b"+re.escape(fam)+r"s?\b", re.I)
            for pfid, entries in zones.items():
                if any(pattern.search(text) for _,text in entries):
                    pfids.append(pfid); znames.extend(text for _,text in entries if pattern.search(text))
        unresolved=[]
        for field in ("MinimumLevel","PlayfieldIds","WeaponTemplateIds","AttackTemplateIds"):
            if field == "PlayfieldIds" and pfids: continue
            unresolved.append(field)
        catalog.append({
            "CanonicalEnemyKey":f"rdb-{NPC_TEMPLATE_TYPE}-{record['id']}","MonsterDataId":record["id"],"TemplateId":record["id"],"DynelType":"NPC_OR_MONSTER_TEMPLATE","DisplayName":name,"InternalName":name,"EnemyFamily":fam,"EnemyArchetype":None,"Faction":None,"Side":None,"ExpansionOrDataset":None,"IsAttackable":None,"IsHostileByDefault":None,"IsBoss":None,"IsUnique":None,"IsQuestEnemy":None,"IsMissionOnly":None,"IsPetLike":None,"IsTurret":bool(name and "turret" in name.lower()),"MinimumLevel":None,"MaximumLevel":None,"ExactLevel":None,"LevelSource":None,"PlayfieldIds":sorted(pfids),"PlayfieldNames":[playfields.get(x) for x in sorted(pfids)],"ZoneNames":sorted(set(znames)),"DungeonNames":[],"SpawnDefinitionIds":[],"SpawnCoordinatesWhenAvailable":[],"WeaponTemplateIds":[],"WeaponNames":[],"WeaponCategories":["NO_REFERENCE_FOUND"],"VisibleWeapon":None,"AttackTemplateIds":[],"AttackTypes":[],"DamageTypes":[],"AttackRange":None,"AttackTime":None,"RechargeTime":None,"SpecialAttacks":[],"NanoProgramIds":[],"NanoProgramNames":[],"SourceRecordIds":[{"RecordType":NPC_TEMPLATE_TYPE,"RecordId":record["id"]}],"SourceDatFiles":source_files,"EvidenceQuality":"RDB_TEMPLATE_NAME" if name else "RAW_IDENTIFIER_ONLY","UnresolvedFields":unresolved})
        catalog[-1]["RdbPayloadSize"]=record.get("size")
        catalog[-1]["RdbPayloadHash"]=record.get("sha256")
        catalog[-1]["RecordClassification"]="INSUFFICIENT_EVIDENCE"
    return sorted(catalog,key=lambda x:x["CanonicalEnemyKey"])

def write_outputs(out, dump, catalog, sources, commit):
    out.mkdir(parents=True,exist_ok=True)
    unresolved=[{"CanonicalEnemyKey":x["CanonicalEnemyKey"],"SourceRecordIds":x["SourceRecordIds"],"UnresolvedFields":x["UnresolvedFields"]} for x in catalog if x["UnresolvedFields"]]
    scanned=sum(x["count"] for x in dump["record_types"])
    source_doc={"ExtractorVersion":VERSION,"ExtractionTimestampUtc":dump.get("source_timestamp_utc"),"RepositoryCommit":commit,"DatFilesRead":sources,"RdbSource":dump["source"],"Parser":"AODB.RdbController.GetRaw plus repository extractor normalization","RecordTypesRead":[{"RecordType":x["type"],"Count":x["count"]} for x in dump["record_types"]],"TotalRecordsScanned":scanned}
    (out/"enemy_catalog.json").write_text(json.dumps(catalog,indent=2,sort_keys=True)+"\n",encoding="utf-8")
    (out/"enemy_catalog_unresolved.json").write_text(json.dumps(unresolved,indent=2,sort_keys=True)+"\n",encoding="utf-8")
    (out/"enemy_catalog_sources.json").write_text(json.dumps(source_doc,indent=2,sort_keys=True)+"\n",encoding="utf-8")
    with (out/"enemy_catalog.csv").open("w",newline="",encoding="utf-8") as f:
        w=csv.DictWriter(f,fieldnames=FIELDS); w.writeheader()
        for row in catalog: w.writerow({k:(json.dumps(row[k],sort_keys=True) if isinstance(row[k],(list,dict)) else row[k]) for k in FIELDS})
    with (out/"enemy_zone_associations.csv").open("w",newline="",encoding="utf-8") as f:
        cols=["CanonicalEnemyKey","EnemyName","Zone","PlayfieldId","Source","Confidence","Notes"]; w=csv.DictWriter(f,fieldnames=cols); w.writeheader()
        for row in catalog:
            for pfid,pfname in zip(row["PlayfieldIds"],row["PlayfieldNames"]): w.writerow({"CanonicalEnemyKey":row["CanonicalEnemyKey"],"EnemyName":row["DisplayName"],"Zone":pfname,"PlayfieldId":pfid,"Source":dump["source"],"Confidence":"INFERRED_CORRELATION","Notes":"Name-token correlation against RDB district/area/statel text"})
    with (out/"enemy_weapon_associations.csv").open("w",newline="",encoding="utf-8") as f:
        csv.DictWriter(f,fieldnames=["CanonicalEnemyKey","EnemyName","Configuration","WeaponCategory","Source","Confidence","Notes"]).writeheader()
    families=Counter(x["EnemyFamily"] or "<missing>" for x in catalog)
    associations=sum(len(x["PlayfieldIds"]) for x in catalog)
    summary=["# Enemy DAT/RDB Catalog Summary","",f"- Total DAT/RDB records scanned: {scanned}",f"- Candidate enemy records: {len(catalog)}",f"- Canonical enemy types: {len(catalog)}","- Variants: 0","- Placed spawn records: 0","- Named bosses: 0","- Unique enemies: 0","- Mission-only enemies: 0","- Enemies with resolved levels: 0",f"- Enemies with resolved playfields: {sum(bool(x['PlayfieldIds']) for x in catalog)}","- Enemies with resolved weapon or attack definitions: 0",f"- Zone associations: {associations}",f"- Unresolved records: {len(unresolved)}","", "## Counts by enemy family",""]
    summary += [f"- {k}: {v}" for k,v in sorted(families.items())]
    summary += ["","## Exclusions","","- No records from RDB type 1040023 were excluded. Other record types are inventoried as non-candidate or unresolved schemas; none are silently treated as enemy templates.","","## Limitations","","- The available repository DAT files contain serialized items, nanos, and playfield statels, not enemy/spawn templates.","- Client RDB type 1040023 exposes template identifiers and names but the available AODB reader exposes unknown layouts only as raw bytes.","- Levels, hostility, boss/mission flags, spawn definitions, and weapon/attack links are therefore preserved as unresolved rather than inferred.","- Zone links are evidence-ranked name-token matches against RDB district/area/statel text and are not exact spawn proof."]
    (out/"enemy_catalog_summary.md").write_text("\n".join(summary)+"\n",encoding="utf-8")
    return scanned,len(unresolved)

def main(argv=None):
    ap=argparse.ArgumentParser(); ap.add_argument("--validate",action="store_true"); ap.add_argument("--build-workbook",action="store_true"); ap.add_argument("--import-dyna-odt"); ap.add_argument("--dump"); ap.add_argument("--output",default="docs/generated/enemy_catalog")
    args=ap.parse_args(argv); root=Path(__file__).resolve().parents[2]; dump_path=Path(args.dump) if args.dump else root/"tools-temp/enemy-catalog-rdb-inventory.json"
    if not args.dump:
        script=root/"tools/enemy_catalog/export_rdb_inventory.ps1"
        subprocess.run(["powershell","-NoProfile","-ExecutionPolicy","Bypass","-File",str(script),"-OutputPath",str(dump_path)],check=True)
    dump=json.loads(dump_path.read_text(encoding="utf-8-sig"))
    dats=sorted(str(p.relative_to(root)).replace("\\","/") for p in (root/"AORebirth/Datafiles").glob("*.dat"))
    commit=subprocess.check_output(["git","rev-parse","HEAD"],cwd=root,text=True).strip()
    catalog=build_catalog(dump,dats+[dump["source"]]); scanned,unresolved=write_outputs(root/args.output,dump,catalog,dats,commit)
    if args.validate:
        names=("enemy_catalog.json","enemy_catalog.csv","enemy_catalog_summary.md","enemy_catalog_unresolved.json","enemy_catalog_sources.json")
        first={n:(root/args.output/n).read_bytes() for n in names}; write_outputs(root/args.output,dump,catalog,dats,commit); second={n:(root/args.output/n).read_bytes() for n in names}
        assert first==second and len({x["CanonicalEnemyKey"] for x in catalog})==len(catalog)
        assert all(x["MinimumLevel"] is None or x["MaximumLevel"] is None or x["MinimumLevel"]<=x["MaximumLevel"] for x in catalog)
    subprocess.run([sys.executable,str(root/"Tools/enemy_catalog/enrich_enemy_catalog.py"),str(root)],cwd=root,check=True)
    dyna=[sys.executable,str(root/"Tools/enemy_catalog/import_dyna_odt.py"),"--output",str(root/args.output)]
    if args.import_dyna_odt: dyna += ["--source",args.import_dyna_odt]
    if args.import_dyna_odt or (root/args.output/"sources/dyna_boss_list_1.normalized.json").exists(): subprocess.run(dyna,cwd=root,check=True)
    if args.build_workbook:
        node=os.environ.get("CODEX_BUNDLED_NODE","node")
        if not os.environ.get("CODEX_NODE_MODULES"):
            raise RuntimeError("CODEX_NODE_MODULES must point to the bundled Codex node_modules directory")
        subprocess.run([node,str(root/"Tools/enemy_catalog/build_workbook.mjs"),str(root)],cwd=root,check=True)
    print(f"enemy catalog: scanned={scanned} candidates={len(catalog)} unresolved={unresolved}")
    return 0
if __name__=="__main__": raise SystemExit(main())
