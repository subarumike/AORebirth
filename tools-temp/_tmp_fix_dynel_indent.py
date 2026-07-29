from __future__ import print_function
import re
p = r"AORebirth/Server/ZoneEngine/Core/Missions/MissionInstanceDynelCapture.cs"
t = open(p, encoding="utf-8").read()
t2 = t.replace(
    "\npublic static readonly string[] Doors_1419349 =\n",
    "\n        public static readonly string[] Doors_1419349 =\n",
)
t2 = t2.replace(
    "\npublic static readonly string[] Chests_1419349 =\n",
    "\n        public static readonly string[] Chests_1419349 =\n",
)
t2 = t2.replace(
    "\npublic static readonly string[] Doors_1441800 =\n",
    "\n        public static readonly string[] Doors_1441800 =\n",
)
if t2 == t:
    print("no indent changes needed or already fixed")
else:
    open(p, "w", encoding="utf-8", newline="\n").write(t2)
    print("fixed dynel indents")
