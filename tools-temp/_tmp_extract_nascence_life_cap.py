# -*- coding: utf-8 -*-
"""Extract Drake SCFU + Chimera/Yuttos ExtTex from capture 20260723-221330."""
from __future__ import annotations

import csv
from collections import defaultdict
from pathlib import Path

CAP = Path(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260723-221330"
)
OUT = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_nascence_life_extract.txt")
ENC = "utf-8-sig"

TARGETS = {
    "7963A853": "Scientist Drake Rodriguez",
    "798E09BC": "Barking Chimera",
    "798C1F0D": "Yuttos Nascence Geosurvey Dog",
    "787B5401": "Joshua Falker",
    "78CCD541": "Prince Creehan",
    "798C1F89": "Swift Silvertail",
}


def main() -> None:
    lines: list[str] = []
    path = CAP / "scfu-appearance.csv"
    with path.open(encoding=ENC, newline="") as fh:
        rows = list(csv.DictReader(fh))
    lines.append(f"scfu-appearance columns: {list(rows[0].keys()) if rows else []}")
    lines.append(f"row count: {len(rows)}")

    by_id: dict[str, list[dict]] = defaultdict(list)
    by_name: dict[str, list[dict]] = defaultdict(list)
    for r in rows:
        ident = (r.get("Identity") or r.get("identity") or "").upper()
        name = (r.get("Name") or r.get("name") or "").strip()
        for tid in TARGETS:
            if tid in ident:
                by_id[tid].append(r)
        if name:
            by_name[name].append(r)

    for tid, label in TARGETS.items():
        lines.append("")
        lines.append("=" * 78)
        lines.append(f"{label} ({tid}) rows={len(by_id[tid])}")
        lines.append("=" * 78)
        for i, r in enumerate(by_id[tid]):
            lines.append(f"--- row {i} ---")
            for k in (
                "Identity",
                "Name",
                "Level",
                "MonsterData",
                "HeadMesh",
                "FlagsNumeric",
                "Flags2",
                "CharacterFlags",
                "Textures",
                "TextureOverrides",
                "Meshes",
                "ExtendedTextureData",
                "ExtendedTextures",
                "ExtTex",
                "ExtTexHex",
                "ExtTexBytes",
                "Playfield",
            ):
                if k in r and r[k] not in (None, ""):
                    val = r[k]
                    if len(str(val)) > 500:
                        lines.append(f"  {k}: len={len(val)} head={val[:120]}...")
                        lines.append(f"  {k}_FULL={val}")
                    else:
                        lines.append(f"  {k}: {val}")
            # dump all keys if unknown
            extra = {k: v for k, v in r.items() if v not in (None, "")}
            lines.append(f"  ALL_NONEMPTY_KEYS={sorted(extra.keys())}")

    # Also find any Barking Chimera / Yuttos with ExtTex regardless of id
    lines.append("")
    lines.append("=" * 78)
    lines.append("All Barking Chimera / Yuttos rows with ExtTex-like fields")
    lines.append("=" * 78)
    for name in ("Barking Chimera", "Yuttos Nascence Geosurvey Dog"):
        for r in by_name.get(name, []):
            extras = {
                k: v
                for k, v in r.items()
                if v
                and (
                    "ext" in k.lower()
                    or "texture" in k.lower()
                    or k
                    in (
                        "HeadMesh",
                        "Textures",
                        "Meshes",
                        "FlagsNumeric",
                        "CharacterFlags",
                        "Identity",
                        "Name",
                        "MonsterData",
                    )
                )
            }
            if any("ext" in k.lower() for k in extras) or extras.get("TextureOverrides"):
                lines.append(str(extras))

    # Print header names containing Ext/Tex
    if rows:
        lines.append("")
        lines.append("Columns containing Ext or Tex or Mesh or Flag or Head:")
        for c in rows[0].keys():
            cl = c.lower()
            if any(x in cl for x in ("ext", "tex", "mesh", "flag", "head", "name", "ident", "monster")):
                lines.append(f"  {c}")

    OUT.write_text("\n".join(lines), encoding="utf-8")
    print(f"Wrote {OUT} ({len(lines)} lines)")


if __name__ == "__main__":
    main()
