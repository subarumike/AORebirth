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

run("DESCRIBE teleports;")
run("SELECT COUNT(*) AS cnt FROM teleports;")
run("SELECT * FROM teleports WHERE destplayfield=4322 LIMIT 20;")
run("SELECT * FROM teleports WHERE sourceplayfield BETWEEN 4676 AND 4699 LIMIT 30;")
