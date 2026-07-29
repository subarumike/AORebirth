import subprocess

def run(db, sql):
    p = subprocess.run(
        [r"C:\xampp\mysql\bin\mysql.exe", "-u", "root", db, "-e", sql],
        capture_output=True,
        text=True,
    )
    print("===", db, "===")
    print(p.stdout or "(empty)")
    if p.stderr:
        print("ERR:", p.stderr[:800])

run("cellao_codex_clean", "DESCRIBE staticdynels;")
run("cellao_codex_clean", "SELECT * FROM staticdynels WHERE Id=14428396\\G")
run("cellao_codex_clean", "SELECT Playfield, COUNT(*) c FROM staticdynels WHERE Playfield BETWEEN 4676 AND 4699 GROUP BY Playfield;")
run("cellao_codex_clean", "SELECT Id, Playfield, Type FROM staticdynels WHERE Playfield=4677 LIMIT 15;")
