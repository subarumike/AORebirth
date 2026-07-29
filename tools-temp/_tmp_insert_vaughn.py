# -*- coding: utf-8 -*-
from pathlib import Path

spawn = Path(r"AORebirth/Server/ZoneEngine/Core/Playfields/AreteLandingSpawn.cs")
frag = Path(r"tools-temp/_tmp_vaughn_area_spawn.csfrag").read_text(encoding="utf-8")
vaughn = r'''            new AreteNpc
            {
                // Capture 20260721-loralei Vaughn Hammond 78E0FC73 (finish dossier + loralei SCFU)
                CaptureInstance = unchecked((int)0x78E0FC73),
                Name = "Vaughn Hammond",
                Level = 25, Health = 724, MonsterData = 281855, Scale = 100, VisualFlags = 31, HeadMesh = 0, RunSpeed = 86,
                NpcFamily = 137, LosHeight = 0, CharacterFlags = 277352961, AppearanceValue = 1576,
                Side = 0, Breed = 1, Gender = 1, Race = 1, Fatness = 1, MovementMode = 3,
                X = 3369.26465f, Y = 18.1111526f, Z = 828.5384f,
                Hx = 0.0f, Hy = 0.7086759f, Hz = 0.0f, Hw = 0.70553416f,
                Textures = new[] { new[] { 0, 286229 }, new[] { 1, 286227 }, new[] { 2, 286228 }, new[] { 3, 286226 }, new[] { 4, 286225 } },
                Meshes = new[] { new[] { 0, 265793, 286562, 2 }, new[] { 1, 264698, 0, 2 }, new[] { 3, 286446, 0, 0 } },
            },
'''
text = spawn.read_text(encoding="utf-8")
if "Vaughn Hammond" in text:
    raise SystemExit("Vaughn already present")
marker = """                Meshes = new[] { new[] { 0, 40271, 0, 4 }, new[] { 1, 35542, 0, 2 } },
            },
        };"""
if marker not in text:
    raise SystemExit("marker missing")
insert = (
    """                Meshes = new[] { new[] { 0, 40271, 0, 4 }, new[] { 1, 35542, 0, 2 } },
            },
"""
    + vaughn
    + frag
    + "        };"
)
text = text.replace(marker, insert, 1)
spawn.write_text(text, encoding="utf-8")
print("inserted Vaughn +", frag.count("new AreteNpc"), "pad NPCs")
