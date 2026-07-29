import subprocess

def run(sql):
    p = subprocess.run(
        [r"C:\xampp\mysql\bin\mysql.exe", "-u", "root", "cellao_codex_test", "-t"],
        input=sql,
        capture_output=True,
        text=True,
    )
    print(p.stdout)
    if p.stderr:
        print("ERR:", p.stderr)

run("DESCRIBE proxydestinations;")
run("SELECT * FROM proxydestinations WHERE playfield=4322 OR destinationplayfield=4322 LIMIT 20;")
