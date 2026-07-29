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

run("DESCRIBE items;")
run("SELECT id, LENGTH(customevents) AS evlen FROM items WHERE id IN (245014,245017,245019,245020,245021);")
