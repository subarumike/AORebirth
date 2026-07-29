from pathlib import Path

# Any tradeskill mentioning Program Crystal result 144801/144802 or rock tools
needles = ["144801", "144802", "144799", "144800", "150273", "150274", "150275", "150281"]
p = Path(r"AORebirth\Libraries\Source\AORebirth.Database\SqlTables\tradeskill.sql")
counts = {n: 0 for n in needles}
examples = {n: [] for n in needles}
with p.open("r", encoding="utf-8", errors="replace") as f:
    for line in f:
        if not line.startswith("INSERT"):
            continue
        for n in needles:
            if n in line:
                counts[n] += 1
                if len(examples[n]) < 3:
                    examples[n].append(line.strip()[:200])
for n in needles:
    print(n, counts[n])
    for e in examples[n]:
        print(" ", e)
