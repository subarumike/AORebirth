#!/usr/bin/env python3
"""Create deterministic manifests for the tracked cleanup candidate trees."""

import csv
import hashlib
import os
import subprocess
import sys


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), os.pardir))
EVIDENCE = os.path.join(ROOT, "docs", "evidence")
CHECKPOINT_PREFIX = "refs/codex/turn-diffs/"

TOOL_CLASSIFICATIONS = {
    "AOSharpCaptureAnalyzer": ("maintained generator/analyzer", "retain; authoritative combat generation"),
    "AOSharpCaptureProtocol": ("maintained generator/analyzer", "retain; shared capture protocol"),
    "AOSharpLiveCapture": ("maintained generator/analyzer", "retain; approved capture workflow"),
    "AOSharpLiveInjector": ("maintained generator/analyzer", "retain; approved capture workflow"),
    "AOSharpMissionCaptureAnalyzer": ("maintained generator/analyzer", "retain; approved mission workflow"),
    "PerkActionExtract": ("maintained generator/analyzer", "retain"),
    "PlayfieldLifecycleTraceRunner": ("maintained generator/analyzer", "retain"),
    "current-client-data-verification": ("maintained generator/analyzer", "retain"),
    "live-data-collector": ("maintained generator/analyzer", "retain"),
    "ao-client-rdb-hints": ("durable evidence/reference", "retain"),
    "ao-dir-quarantine": ("durable evidence/reference", "retain"),
    "ao-dll-function-map": ("durable evidence/reference", "retain"),
    "db-backups": ("durable evidence/reference", "retain; database safety evidence"),
    "external": ("third-party source/reference", "retain; referenced by historical evidence"),
    "fusion-logs": ("durable evidence/reference", "retain"),
    "gmi-local-web": ("durable evidence/reference", "retain"),
    "icc-rk-local-web": ("durable evidence/reference", "retain"),
    "live-combat-chase-observations": ("durable evidence/reference", "retain"),
    "live-loot-observations": ("durable evidence/reference", "retain"),
    "live-pcaps": ("durable evidence/reference", "retain"),
    "live-quest-observations": ("durable evidence/reference", "retain"),
    "mysql-restore": ("durable evidence/reference", "retain; database recovery tooling"),
    "recovered-flint": ("durable evidence/reference", "retain"),
    "recovered-flint-extra": ("durable evidence/reference", "retain"),
    "recovered-flint-extra2": ("durable evidence/reference", "retain"),
    "screen-captures": ("durable evidence/reference", "retain"),
    "vendor-captures": ("durable evidence/reference", "retain"),
    "worktree-snapshots": ("durable evidence/reference", "retain; preserved snapshots"),
    "arete-analysis": ("reproducible generated output", "retain pending documented relocation"),
    "arete-framework-validation": ("reproducible generated output", "retain pending documented relocation"),
    "enemy-movement-replay": ("reproducible generated output", "retain pending documented relocation"),
    "enemy-spawn-coverage": ("reproducible generated output", "retain pending documented relocation"),
    "mob-loot-coverage": ("reproducible generated output", "retain pending documented relocation"),
    "sql-staging": ("reproducible generated output", "retain pending database review"),
    "CellAOCombatSmokeTests": ("one-off test/probe", "retain pending migration to maintained tests"),
    "EasyHookSmokeInjector": ("one-off test/probe", "retain pending capture-tool consolidation"),
    "EasyHookSmokePayload": ("one-off test/probe", "retain pending capture-tool consolidation"),
    "AOSharpLiveCapture moj": ("one-off duplicate/probe", "remove after manifest"),
    "AOSharpLiveCapture2": ("one-off duplicate/probe", "remove after manifest"),
    "AOSharpMissionCaptureAnalyzer proba": ("one-off duplicate/probe", "remove after manifest"),
    "_aosharp_decompile": ("one-off decompilation", "remove after manifest"),
    "ze-full-decompile": ("one-off decompilation", "remove after manifest"),
    "_tmp_cap_002423_assets": ("temporary generated output", "remove after manifest"),
    "_tmp_cap_181214_assets": ("temporary generated output", "remove after manifest"),
    "_tmp_cap_224228_assets": ("temporary generated output", "remove after manifest"),
    "_tmp_itemcheck": ("temporary generated output", "remove after manifest"),
    "_tmp_mail_recovery": ("temporary recovery probe", "retain; unique-work review required"),
    "_tmp_mission_shapes_assets": ("temporary generated output", "remove after manifest"),
}


def git_files(prefix):
    result = subprocess.run(
        ["git", "ls-files", "-z", "--", prefix],
        cwd=ROOT,
        check=True,
        stdout=subprocess.PIPE,
    )
    return [path.decode("utf-8") for path in result.stdout.split(b"\0") if path]


def digest(path):
    value = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


def write_tool_inventory():
    files = git_files("tools-temp")
    grouped = {}
    for relative in files:
        parts = relative.replace("\\", "/").split("/")
        if len(parts) > 2:
            group = parts[1]
        else:
            filename = parts[-1]
            extension = os.path.splitext(filename)[1].lower()
            if filename.startswith("_"):
                group = "(root temporary probes)"
            elif extension in (".dll", ".exe", ".pdb"):
                group = "(root third-party binaries)"
            elif extension in (".cmd", ".bat", ".ps1", ".py", ".cs"):
                group = "(root helper scripts)"
            elif extension == ".md":
                group = "(root documentation)"
            else:
                group = "(root durable reference data)"
        grouped.setdefault(group, []).append(relative)

    root_classifications = {
        "(root temporary probes)": ("one-off temporary probe/output", "remove after manifest"),
        "(root third-party binaries)": ("third-party binary/tool", "remove after manifest"),
        "(root helper scripts)": ("maintained and one-off helper scripts", "retain pending workflow consolidation"),
        "(root documentation)": ("durable documentation", "retain"),
        "(root durable reference data)": ("durable evidence/reference", "retain"),
    }

    inventory_path = os.path.join(EVIDENCE, "baseline_cleanup_tool_inventory_20260801.csv")
    with open(inventory_path, "w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(("group", "tracked_files", "tracked_bytes", "classification", "disposition"))
        for group in sorted(grouped):
            classification, disposition = TOOL_CLASSIFICATIONS.get(
                group, root_classifications.get(group, ("durable evidence/reference", "retain pending relocation review"))
            )
            size = sum(os.path.getsize(os.path.join(ROOT, path)) for path in grouped[group])
            writer.writerow((group, len(grouped[group]), size, classification, disposition))

    removal_groups = {
        name for name, (_, disposition) in TOOL_CLASSIFICATIONS.items() if disposition == "remove after manifest"
    }
    removal_groups.update(
        name for name, (_, disposition) in root_classifications.items() if disposition == "remove after manifest"
    )
    removal_path = os.path.join(EVIDENCE, "baseline_cleanup_tool_removals_20260801.csv")
    with open(removal_path, "w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(("path", "size_bytes", "sha256"))
        for relative in sorted(files):
            parts = relative.replace("\\", "/").split("/")
            if len(parts) > 2:
                group = parts[1]
            else:
                filename = parts[-1]
                extension = os.path.splitext(filename)[1].lower()
                if filename.startswith("_"):
                    group = "(root temporary probes)"
                elif extension in (".dll", ".exe", ".pdb"):
                    group = "(root third-party binaries)"
                else:
                    group = ""
            if group in removal_groups:
                absolute = os.path.join(ROOT, relative)
                writer.writerow((relative.replace("\\", "/"), os.path.getsize(absolute), digest(absolute)))


def write_cursor_inventory():
    cursor_files = git_files("AORebirth/Cursor")
    inventory_path = os.path.join(EVIDENCE, "baseline_cleanup_cursor_inventory_20260801.csv")
    with open(inventory_path, "w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(("cursor_path", "production_path", "status", "cursor_sha256", "production_sha256"))
        for cursor_relative in sorted(cursor_files):
            production_relative = cursor_relative.replace("AORebirth/Cursor/", "AORebirth/", 1)
            cursor_path = os.path.join(ROOT, cursor_relative)
            production_path = os.path.join(ROOT, production_relative)
            cursor_hash = digest(cursor_path)
            if not os.path.isfile(production_path):
                writer.writerow((cursor_relative, production_relative, "no production counterpart", cursor_hash, ""))
                continue
            production_hash = digest(production_path)
            status = "identical" if cursor_hash == production_hash else "divergent"
            writer.writerow((cursor_relative, production_relative, status, cursor_hash, production_hash))


def remove_manifested_tools():
    removal_path = os.path.join(EVIDENCE, "baseline_cleanup_tool_removals_20260801.csv")
    if not os.path.isfile(removal_path):
        raise RuntimeError("generate the cleanup manifests before removal")

    paths = []
    with open(removal_path, newline="", encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            relative = row["path"]
            normalized = relative.replace("\\", "/")
            if not normalized.startswith("tools-temp/") or normalized == "tools-temp/":
                raise RuntimeError("unsafe cleanup path: {0}".format(relative))
            absolute = os.path.abspath(os.path.join(ROOT, relative))
            if os.path.commonpath((ROOT, absolute)) != ROOT:
                raise RuntimeError("cleanup path escapes repository: {0}".format(relative))
            if not os.path.isfile(absolute) or digest(absolute) != row["sha256"]:
                raise RuntimeError("cleanup candidate changed after manifest: {0}".format(relative))
            paths.append(relative)

    for offset in range(0, len(paths), 100):
        subprocess.run(["git", "rm", "--"] + paths[offset : offset + 100], cwd=ROOT, check=True)
    print("Manifested temporary tools removed: PASS ({0} files)".format(len(paths)))


def write_checkpoint_inventory():
    result = subprocess.run(
        ["git", "for-each-ref", "--format=%(refname)%09%(objectname)", CHECKPOINT_PREFIX],
        cwd=ROOT,
        check=True,
        stdout=subprocess.PIPE,
        text=True,
    )
    path = os.path.join(EVIDENCE, "baseline_cleanup_codex_turn_diff_refs_20260801.csv")
    lines = sorted(value for value in result.stdout.splitlines() if value)
    with open(path, "w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(("refname", "object_id"))
        for line in lines:
            writer.writerow(line.split("\t", 1))
    print("Codex checkpoint-ref manifest: PASS ({0} refs)".format(len(lines)))


def remove_checkpoint_refs():
    path = os.path.join(EVIDENCE, "baseline_cleanup_codex_turn_diff_refs_20260801.csv")
    if not os.path.isfile(path):
        raise RuntimeError("generate the checkpoint-ref manifest before removal")
    removed = 0
    with open(path, newline="", encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            refname = row["refname"]
            object_id = row["object_id"]
            if not refname.startswith(CHECKPOINT_PREFIX):
                raise RuntimeError("unsafe checkpoint ref: {0}".format(refname))
            current = subprocess.run(
                ["git", "rev-parse", "--verify", refname],
                cwd=ROOT,
                check=True,
                stdout=subprocess.PIPE,
                text=True,
            ).stdout.strip()
            if current != object_id:
                raise RuntimeError("checkpoint ref changed after manifest: {0}".format(refname))
            subprocess.run(["git", "update-ref", "-d", refname, object_id], cwd=ROOT, check=True)
            removed += 1
    print("Manifested Codex checkpoint refs removed: PASS ({0} refs)".format(removed))


def main():
    if sys.argv[1:] == ["generate"]:
        os.makedirs(EVIDENCE, exist_ok=True)
        write_tool_inventory()
        write_cursor_inventory()
        print("Cleanup tree manifests: PASS")
    elif sys.argv[1:] == ["remove-tools"]:
        remove_manifested_tools()
    elif sys.argv[1:] == ["generate-checkpoint-refs"]:
        os.makedirs(EVIDENCE, exist_ok=True)
        write_checkpoint_inventory()
    elif sys.argv[1:] == ["remove-checkpoint-refs"]:
        remove_checkpoint_refs()
    else:
        raise SystemExit(
            "usage: audit_cleanup_trees.py "
            "generate|remove-tools|generate-checkpoint-refs|remove-checkpoint-refs"
        )


if __name__ == "__main__":
    main()
