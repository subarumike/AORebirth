import pymysql

c = pymysql.connect(host="localhost", user="root", password="", database="cellao_codex_clean")
cur = c.cursor()
cur.execute("SHOW TABLES LIKE 'charactersperks'")
print("TABLE", cur.fetchone())
try:
    cur.execute("SELECT COUNT(*) FROM charactersperks")
    print("COUNT", cur.fetchone()[0])
    cur.execute("SELECT Id, CharacterId, PacketId FROM charactersperks ORDER BY Id DESC LIMIT 40")
    rows = cur.fetchall()
    print("ROWS", len(rows))
    for r in rows:
        print(r)
except Exception as e:
    print("ERR", e)
cur.execute("SELECT Id, Name FROM characters WHERE Id=18")
print("CHAR", cur.fetchone())
c.close()
