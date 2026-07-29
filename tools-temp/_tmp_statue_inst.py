import subprocess

def run(sql):
    p = subprocess.run(
        [r"C:\xampp\mysql\bin\mysql.exe", "-u", "root", "cellao_codex_clean", "-e", sql],
        capture_output=True,
        text=True,
    )
    print(p.stdout or "(empty)")
    if p.stderr:
        print("ERR:", p.stderr[:500])

run("SELECT Id, Type, Instance, Playfield, X, Y, Z FROM staticdynels WHERE Instance=14428396;")
run("SELECT Id, Type, Instance, Playfield, X, Y, Z FROM staticdynels WHERE Playfield=4677;")
run("SELECT HEX(stats) FROM staticdynels WHERE Playfield=4677 LIMIT 1;")
