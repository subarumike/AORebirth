import subprocess
p=subprocess.run([r"C:\xampp\mysql\bin\mysql.exe","-u","root","cellao_codex_test","-t"],input="SELECT * FROM teleports WHERE destinationPlayfield=4322 LIMIT 20;",capture_output=True,text=True)
print(p.stdout)
