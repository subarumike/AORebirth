#!/usr/bin/env python3
"""Fail when repository-visible configuration contains likely plaintext secrets."""

from __future__ import print_function

import os
import re
import subprocess
import sys


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), os.pardir))
ALLOWED_VALUES = {
    "",
    "changeme",
    "example",
    "not-set",
    "offline",
    "placeholder",
    "replace-me",
    "set-in-environment",
    "stage7_offline",
    "stage8-placeholder",
}
PATTERNS = (
    (
        "connection-string password",
        re.compile(
            r"(?i)(?:server|host)\s*=\s*[^;\r\n]+;[^\r\n]*(?:pwd|password)\s*=\s*([^;\s<\"')]+)"
        ),
    ),
    ("XML password", re.compile(r"(?i)<(?:password|mysqlpassword)>\s*([^<]+?)\s*</")),
)


def repository_paths():
    completed = subprocess.run(
        ["git", "ls-files", "-co", "--exclude-standard", "-z"],
        cwd=ROOT,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=True,
    )
    for raw_path in completed.stdout.split(b"\0"):
        if raw_path:
            yield raw_path.decode("utf-8", "surrogateescape")


def normalized(value):
    return value.strip().strip('"\'').lower()


def main():
    findings = []
    for relative_path in repository_paths():
        absolute_path = os.path.join(ROOT, relative_path)
        try:
            if os.path.getsize(absolute_path) > 5 * 1024 * 1024:
                continue
        except (IOError, OSError):
            continue
        try:
            with open(absolute_path, "rb") as handle:
                content = handle.read()
        except (IOError, OSError):
            continue
        if b"\0" in content:
            continue
        text = content.decode("utf-8", "replace")
        for line_number, line in enumerate(text.splitlines(), 1):
            for label, pattern in PATTERNS:
                for match in pattern.finditer(line):
                    value = normalized(match.group(1))
                    if (
                        value not in ALLOWED_VALUES
                        and not value.startswith("replace")
                        and not value.startswith("${")
                        and not value.startswith("%")
                    ):
                        findings.append((relative_path, line_number, label))

    if findings:
        for path, line_number, label in findings:
            print("potential secret: {0}:{1} ({2})".format(path, line_number, label))
        print("Secret scan: FAIL ({0} potential plaintext value(s))".format(len(findings)))
        return 1

    print("Secret scan: PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
