import MySQLdb

conn = MySQLdb.connect(host="localhost", user="root", passwd="", db="cellao_codex_clean")
cur = conn.cursor()

ids = [150274, 150273, 144770, 144769, 144768, 144767, 144800, 144799, 144802, 144801, 150281, 150275, 149822, 144785, 149823, 144786]
print("=== rock/tool/crystal id hits in tradeskill ===")
for i in ids:
    cur.execute(
        "SELECT COUNT(*) FROM tradeskill WHERE Id1=%s OR Id2=%s OR ResultIds LIKE %s OR ResultIds LIKE %s",
        (i, i, f"%{i}%", f"{i},%"),
    )
    print(i, cur.fetchone()[0])

print("\n=== total tradeskill rows ===")
cur.execute("SELECT COUNT(*) FROM tradeskill")
print(cur.fetchone()[0])

print("\n=== sample NP+CL 400 recipes ===")
cur.execute(
    "SELECT Id1,Id2,ResultIds,QlRangePercent,DeleteFlag,Skill,SkillPercent,MaxBump "
    "FROM tradeskill WHERE Skill LIKE '%%160%%' AND SkillPercent LIKE '%%400%%' LIMIT 5"
)
for row in cur.fetchall():
    print(row)

print("\n=== ME 300 recipes (rock extractor style) ===")
cur.execute(
    "SELECT Id1,Id2,ResultIds,QlRangePercent,DeleteFlag,Skill,SkillPercent,SkillPerBump,MaxBump "
    "FROM tradeskill WHERE Skill='125' AND SkillPercent='300' LIMIT 10"
)
rows = cur.fetchall()
print("count sample", len(rows))
for row in rows:
    print(row)
cur.execute("SELECT COUNT(*) FROM tradeskill WHERE Skill='125' AND SkillPercent='300'")
print("total ME/300", cur.fetchone()[0])

print("\n=== ME+EE 375 ===")
cur.execute("SELECT COUNT(*) FROM tradeskill WHERE Skill LIKE '%%125%%' AND Skill LIKE '%%126%%' AND SkillPercent LIKE '%%375%%'")
print(cur.fetchone()[0])

print("\n=== final 470/450 style ===")
cur.execute("SELECT COUNT(*) FROM tradeskill WHERE SkillPercent LIKE '%%470%%'")
print(cur.fetchone()[0])

print("\n=== itemnames for rock chain ===")
for name in ("Carbonrich Rock", "Carbonrich Ore", "Pure Carbon Crystal", "Program Crystal", "Jensen Personal Ore Extractor"):
    cur.execute("SELECT AOID, Name FROM itemnames WHERE Name=%s LIMIT 5", (name,))
    print(name, cur.fetchall())

conn.close()
