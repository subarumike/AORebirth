import subprocess

def run(db, sql):
    p = subprocess.run(
        [r"C:\xampp\mysql\bin\mysql.exe", "-u", "root", db, "-N", "-e", sql],
        capture_output=True,
        text=True,
    )
    print("===", db, "===")
    print(p.stdout or "(empty)")
    if p.stderr:
        print("ERR:", p.stderr[:500])

sql = (
    "SELECT Id, Playfield, TemplateId, X, Y, Z FROM staticdynels "
    "WHERE Id=14428396 OR Id=14428396; "
    "SELECT Id, Playfield, TemplateId FROM staticdynels WHERE Playfield IN (4676,4677) LIMIT 20; "
    "SELECT Playfield, COUNT(*) c FROM staticdynels WHERE Playfield BETWEEN 4676 AND 4699 GROUP BY Playfield; "
    "SELECT Playfield, COUNT(*) c FROM staticdynels WHERE Playfield IN (4310,4311,4312,4313) GROUP BY Playfield;"
)
run("cellao_codex_clean", sql)
run("cellao_codex_test", sql)
