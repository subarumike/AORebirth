#!/usr/bin/env python3
"""Inventory and remove the explicitly approved disposable baseline artifacts."""

from __future__ import print_function

import argparse
import csv
import datetime
import hashlib
import os
import shutil


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), os.pardir))
MANIFEST = os.path.join(ROOT, "docs", "evidence", "baseline_cleanup_storage_manifest_20260801.csv")
TARGETS = (
    "diagnostics",
    os.path.join("tools-temp", "ProcDump"),
    os.path.join("AORebirth", "Server", "ZoneEngine", "Content", "Captured", "Arete", "movement"),
    "additional captures notprocessed.odt",
)
HASH_LIMIT = 10 * 1024 * 1024


def checked_path(relative_path):
    absolute_path = os.path.abspath(os.path.join(ROOT, relative_path))
    if os.path.commonpath((ROOT, absolute_path)) != ROOT or absolute_path == ROOT:
        raise RuntimeError("unsafe cleanup target: {0}".format(relative_path))
    return absolute_path


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def rows_for(relative_path):
    absolute_path = checked_path(relative_path)
    if not os.path.exists(absolute_path):
        return
    paths = []
    if os.path.isdir(absolute_path):
        for directory, _, filenames in os.walk(absolute_path):
            for filename in filenames:
                paths.append(os.path.join(directory, filename))
    else:
        paths.append(absolute_path)

    for path in sorted(paths):
        size = os.path.getsize(path)
        modified = (
            datetime.datetime.fromtimestamp(os.path.getmtime(path), datetime.timezone.utc)
            .replace(microsecond=0)
            .isoformat()
            .replace("+00:00", "Z")
        )
        digest = sha256(path) if size <= HASH_LIMIT else "OMITTED_FILE_OVER_10_MIB"
        yield (os.path.relpath(path, ROOT).replace("\\", "/"), size, modified, digest)


def inventory():
    with open(MANIFEST, "w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(("path", "size_bytes", "modified_utc", "sha256"))
        for target in TARGETS:
            for row in rows_for(target):
                writer.writerow(row)
    print("Cleanup inventory: PASS")


def remove_targets():
    if not os.path.isfile(MANIFEST):
        raise RuntimeError("inventory must be generated before deletion")
    for relative_path in TARGETS:
        absolute_path = checked_path(relative_path)
        if os.path.isdir(absolute_path):
            shutil.rmtree(absolute_path)
        elif os.path.isfile(absolute_path):
            os.remove(absolute_path)
    print("Disposable cleanup: PASS")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=("inventory", "delete"))
    args = parser.parse_args()
    if args.action == "inventory":
        inventory()
    else:
        remove_targets()


if __name__ == "__main__":
    main()
