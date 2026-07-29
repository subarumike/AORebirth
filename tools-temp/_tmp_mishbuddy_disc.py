import sqlite3

c = sqlite3.connect(r"C:\Program Files (x86)\MMOToolbox\MishBuddy\ao.db3")
cur = c.cursor()
for q in ["%Instruction Disc%", "%Instruction Disk%", "%Summon Grid Armor%"]:
    rows = list(cur.execute("SELECT Num, Name, QL FROM Items WHERE Name LIKE ? ORDER BY Name", (q,)))
    print("q", q, "n", len(rows))
    for r in rows[:30]:
        print(" ", r)
