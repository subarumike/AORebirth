from __future__ import print_function

p = r"AORebirth/Server/ZoneEngine/Core/Missions/MissionInstanceDynelCapture.cs"
t = open(p, encoding="utf-8").read()
doors = open(r"tools-temp/_tmp_doors_1441800.csfrag", encoding="utf-8").read().strip()
t = t.replace(
    "\npublic static readonly string[] Chests_1419349 =\n",
    "\n        public static readonly string[] Chests_1419349 =\n",
    1,
)
marker = "        public static readonly string[] Chests_1441800 ="
if "Doors_1441800" in t and "public static readonly string[] Doors_1441800" in t:
    print("already has Doors_1441800")
elif marker not in t:
    raise SystemExit("marker missing")
else:
    t = t.replace(marker, doors + "\n\n" + marker, 1)
    open(p, "w", encoding="utf-8", newline="\n").write(t)
    print("restored Doors_1441800")
