import sqlite3

p = r"C:\Program Files (x86)\MMOToolbox\MishBuddy\ao.db3"
con = sqlite3.connect(p)
cur = con.cursor()
print("tables:")
for r in cur.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name"):
    print(" ", r[0])

# find grid armor / instruction
for q in [
    "%Grid Armor%",
    "%Instruction Disc%",
    "%Instruction Disk%",
]:
    print("search", q)
    # try common schemas
    for table_info in cur.execute("SELECT name FROM sqlite_master WHERE type='table'"):
        t = table_info[0]
        cols = [c[1] for c in cur.execute("PRAGMA table_info(%s)" % t)]
        name_cols = [c for c in cols if "name" in c.lower() or c.lower() in ("itemname", "title")]
        id_cols = [c for c in cols if c.lower() in ("aoid", "lowid", "id", "itemid", "ql")]
        if not name_cols:
            continue
        ncol = name_cols[0]
        try:
            sql = "SELECT * FROM %s WHERE %s LIKE ? LIMIT 5" % (t, ncol)
            rows = list(cur.execute(sql, (q,)))
            if rows:
                print(" hit table", t, "cols", cols[:12], "n", len(rows))
                for row in rows[:3]:
                    print("  ", row[:12])
        except Exception as e:
            pass
