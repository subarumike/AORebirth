#!/usr/bin/env python3
"""Verify and extract the exact retained AOSharp SDK for the harvester build."""

from __future__ import annotations

import argparse
import hashlib
import zipfile
from pathlib import Path


EXPECTED_SHA256 = "4c2946f10aaa3d92a902be66149a09e4a24ca13bffd8110db37c5def4c578f22"
MEMBERS = ("lib/net48/AOSharp.Common.dll", "lib/net48/AOSharp.Core.dll")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("output_root", type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    package = root / "docs/reference/missions/malis/raw/aosharpsdk.1.0.106.nupkg"
    if sha256(package) != EXPECTED_SHA256:
        raise SystemExit("AOSHARP_SDK_SHA256_MISMATCH")
    output = args.output_root.resolve()
    with zipfile.ZipFile(package) as archive:
        for member in MEMBERS:
            destination = output / member
            destination.parent.mkdir(parents=True, exist_ok=True)
            content = archive.read(member)
            if not destination.exists() or destination.read_bytes() != content:
                destination.write_bytes(content)
    print("MISSION_OFFER_HARVESTER_SDK_PREPARE=PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
