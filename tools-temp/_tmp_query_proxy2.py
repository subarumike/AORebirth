import subprocess

def run(sql):
    p = subprocess.run(
        [r"C:\xampp\mysql\bin\mysql.exe", "-u", "root", "cellao_codex_test", "-t"],
        input=sql,
        capture_output=True,
        text=True,
    )
    print(p.stdout)

run("SELECT * FROM proxydestinations WHERE Playfield=4322;")
run("SELECT * FROM proxydestinations WHERE Playfield IN (4320,4321,4310,4311,4312);")
