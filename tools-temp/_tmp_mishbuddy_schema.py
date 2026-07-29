import sqlite3

p = r"C:\Program Files (x86)\MMOToolbox\MishBuddy\ao.db3"
con = sqlite3.connect(p)
cur = con.cursor()
print("Items cols:", [c[1] for c in cur.execute("PRAGMA table_info(Items)")])
print("count", cur.execute("SELECT COUNT(*) FROM Items").fetchone())
print("sample", cur.execute("SELECT * FROM Items LIMIT 3").fetchall())
# search any column text
cols = [c[1] for c in cur.execute("PRAGMA table_info(Items)")]
for col in cols:
    try:
        rows = list(cur.execute("SELECT * FROM Items WHERE CAST(%s AS TEXT) LIKE '%%Grid%%' LIMIT 5" % col))
        if rows:
            print("col", col, "hits", len(rows), rows[0])
    except Exception as e:
        print("err", col, e)

# also plists
print("plists tables")
p2 = r"C:\Program Files (x86)\MMOToolbox\MishBuddy\plists.db3"
con2 = sqlite3.connect(p2)
cur2 = con2.cursor()
for r in cur2.execute("SELECT name FROM sqlite_master WHERE type='table'"):
    print(" ", r[0])
    cols2 = [c[1] for c in cur2.execute("PRAGMA table_info(%s)" % r[0])]
    print("   cols", cols2[:20])
