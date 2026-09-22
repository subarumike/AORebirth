"""Require identical placement manifest bytes and source identity across platforms."""
import argparse
import hashlib
import json
from pathlib import Path
import re


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON key: " + key)
        result[key] = value
    return result


def parity_digest(native_bytes, expected_sha, expected_windows_digest):
    if not re.fullmatch(r"[0-9a-f]{40}", expected_sha):
        raise ValueError("Invalid source identity")
    if not re.fullmatch(r"[0-9a-f]{64}", expected_windows_digest):
        raise ValueError("Invalid accepted Windows digest")
    manifest = json.loads(native_bytes, object_pairs_hook=unique_object)
    if manifest.get("SourceSHA") != expected_sha:
        raise ValueError("Native manifest does not match accepted source")
    digest = hashlib.sha256(native_bytes).hexdigest()
    if digest != expected_windows_digest:
        raise ValueError("Placement content differs from the accepted Windows manifest")
    return digest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--expected-sha", required=True)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--windows-digest", required=True)
    args = parser.parse_args()
    print(parity_digest(args.manifest.read_bytes(), args.expected_sha, args.windows_digest))


if __name__ == "__main__":
    main()
