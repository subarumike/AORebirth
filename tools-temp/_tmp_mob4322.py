import subprocess
p=subprocess.run([r"C:\xampp\mysql\bin\mysql.exe","-u","root","cellao_codex_test","-t"],input="SELECT Playfield, AVG(X), AVG(Y), AVG(Z), COUNT(*) FROM mobspawns WHERE Playfield=4322 GROUP BY Playfield;",capture_output=True,text=True)
print(p.stdout or p.stderr)
