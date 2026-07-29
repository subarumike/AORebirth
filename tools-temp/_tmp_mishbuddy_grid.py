import sqlite3

c = sqlite3.connect(r"C:\Program Files (x86)\MMOToolbox\MishBuddy\ao.db3")
cur = c.cursor()
q = """
SELECT Num, Name, QL FROM Items
WHERE Name LIKE '%Grid Armor%'
   OR Name LIKE '%Instruction Disc%Grid%'
   OR Name LIKE '%Instruction Disk%Grid%'
ORDER BY Name
"""
rows = list(cur.execute(q))
print("n", len(rows))
for r in rows:
    print(r)
