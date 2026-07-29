"""Insert Carbonrich rock pipeline tradeskill recipes (AO-Universe nanocrystal guide)."""
from pathlib import Path

# Id1 = tool (kept), Id2 = material (consumed), DeleteFlag=2
# QlRangePercent: engine requires tool QL >= (100-pct)% of material QL
#   75 => tool >= 25% of rock/ore (Jensen, Isotope per AOWiki)
#   50 => tool >= 50% of crystal (Neutron)
JENSEN = list(range(150275, 150282))  # 150275..150281
ROCKS = [150273, 150274]
ISOTOPES = [144783, 144785, 149814, 149819, 149820, 149821, 149822]
ORES = [144767, 144768, 144769, 144770]
NEUTRONS = [144784, 144786, 149815, 149816, 149817, 149818, 149823]
PURES = [144799, 144800]

ORE_RESULT = "144770,144768"  # items.dat: 144770=QL1, 144768=QL255 (AOIDs not sequential)
PURE_RESULT = "144800,144799"  # items.dat: 144800=QL1, 144799=QL255
PROG_RESULT = "144801,144802"  # items.dat: 144801=QL1, 144802=QL255

rows = []

def add(id1, id2, result, ql_range, skills, percents):
    # SkillPerBump/MaxBump unused for non-implant (bump gated by IsImplant)
    rows.append(
        f"({id1},{id2},0,'{result}',{ql_range},2,\"{skills}\",\"{percents}\",\"0\",0,50,50,0)"
        if "," not in skills
        else f"({id1},{id2},0,'{result}',{ql_range},2,\"{skills}\",\"{percents}\",\"0,0\",0,50,50,0)"
    )

for j in JENSEN:
    for r in ROCKS:
        add(j, r, ORE_RESULT, 75, "125", "300")

for iso in ISOTOPES:
    for o in ORES:
        add(iso, o, PURE_RESULT, 75, "125,126", "375,375")

for n in NEUTRONS:
    for p in PURES:
        add(n, p, PROG_RESULT, 50, "125,157", "425,425")

sql = []
sql.append("-- Nanocrystal rock pipeline (Carbonrich Rock -> Program Crystal)")
sql.append("-- Source: AO-Universe nanocrystal-creation + AOWiki tool QL floors")
sql.append("-- Safe to re-run: deletes prior AORebirth rock-pipeline rows first")
sql.append("DELETE FROM tradeskill WHERE Id1 BETWEEN 150275 AND 150281 AND Id2 IN (150273,150274);")
sql.append(
    "DELETE FROM tradeskill WHERE Id1 IN (144783,144785,149814,149819,149820,149821,149822) "
    "AND Id2 BETWEEN 144767 AND 144770;"
)
sql.append(
    "DELETE FROM tradeskill WHERE Id1 IN (144784,144786,149815,149816,149817,149818,149823) "
    "AND Id2 IN (144799,144800);"
)
sql.append("INSERT INTO tradeskill VALUES")
sql.append(",\n".join(rows) + ";")
sql.append(
    "SELECT COUNT(*) AS rock_pipeline_rows FROM tradeskill "
    "WHERE (Id1 BETWEEN 150275 AND 150281 AND Id2 IN (150273,150274)) "
    "OR (Id1 IN (144783,144785,149814,149819,149820,149821,149822) AND Id2 BETWEEN 144767 AND 144770) "
    "OR (Id1 IN (144784,144786,149815,149816,149817,149818,149823) AND Id2 IN (144799,144800));"
)

out = Path(r"tools-temp\_tmp_insert_nano_rock_pipeline.sql")
out.write_text("\n".join(sql) + "\n", encoding="utf-8")
print("wrote", out, "rows", len(rows))
