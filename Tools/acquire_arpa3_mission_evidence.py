#!/usr/bin/env python3
"""Acquire the public ARPA3/ClickSaver mission-evidence snapshot.

This is deliberately a live-only acquisition tool.  It archives raw response
bytes and response metadata, but it never calls the Rollability CGI backend:
both arpa3.net and javierarpa.com publish robots.txt rules that disallow
``/cgi-bin``.  Offline normalization and tests consume only the checked-in
snapshot produced by this tool.
"""

from __future__ import annotations

import argparse
import hashlib
import html
import json
from pathlib import Path
import re
import sys
import time
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.parse import urlparse
from urllib.request import Request, urlopen


TOOL_VERSION = "1.0.0"
REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_ROOT = REPOSITORY_ROOT / "docs" / "reference" / "missions"
USER_AGENT = "AORebirth mission evidence collector/1.0 (+offline archival)"
ARPA_CRAWL_DELAY_SECONDS = 4.0

CLICK_SAVER_SOURCE_COMMIT = "38f9347aca020ce2dd0e2e0b752829fc582b1532"

PUBLIC_ARTIFACTS = (
    {
        "id": "arpa3-robots",
        "role": "ROBOTS_POLICY",
        "url": "https://arpa3.net/robots.txt",
        "path": "arpa3/raw/robots.txt",
    },
    {
        "id": "javierarpa-robots",
        "role": "ROBOTS_POLICY",
        "url": "https://javierarpa.com/robots.txt",
        "path": "arpa3/raw/javierarpa-robots.txt",
    },
    {
        "id": "rollability-page",
        "role": "ROLLABILITY_UI_HTML",
        "url": "https://arpa3.net/ao/rollability.html",
        "path": "arpa3/raw/rollability.html",
    },
    {
        "id": "rollability-about",
        "role": "ROLLABILITY_METHODOLOGY_HTML",
        "url": "https://arpa3.net/ao/rollability-about.html",
        "path": "arpa3/raw/rollability-about.html",
    },
    {
        "id": "clicksaver-page",
        "role": "CLICKSAVER_HISTORY_HTML",
        "url": "https://arpa3.net/ao/clicksaver.shtml",
        "path": "arpa3/raw/clicksaver.shtml",
    },
    {
        "id": "clicksaver-databases-page",
        "role": "CLICKSAVER_DATABASE_DOWNLOADS_HTML",
        "url": "https://arpa3.net/ao/clicksaver-premade-local-databases.html",
        "path": "arpa3/raw/clicksaver-premade-local-databases.html",
    },
    {
        "id": "clicksaver-database-creation-page",
        "role": "CLICKSAVER_DATABASE_FORMAT_HISTORY_HTML",
        "url": "https://arpa3.net/ao/clicksaver-local-database-creation.html",
        "path": "arpa3/raw/clicksaver-local-database-creation.html",
    },
    {
        "id": "rollability-javascript",
        "role": "ROLLABILITY_UI_JAVASCRIPT",
        "url": "https://arpa3.net/ao/isitrollable.js",
        "path": "arpa3/raw/isitrollable.js",
        "referer": "https://arpa3.net/ao/rollability.html",
    },
    {
        "id": "clicksaver-3.1.0-package",
        "role": "CLICKSAVER_BINARY_AND_TINY_CDB_ARCHIVE",
        "url": "http://arpa3.net/ao/dl/cs310-v2-temp/cs310-v2.zip",
        "path": "clicksaver/raw/cs310-v2.zip",
    },
    {
        "id": "clicksaver-source-commit",
        "role": "CLICKSAVER_SOURCE_ARCHIVE",
        "url": (
            "https://github.com/pzychotic/ClickSaver/archive/"
            f"{CLICK_SAVER_SOURCE_COMMIT}.zip"
        ),
        "path": f"clicksaver/raw/clicksaver-source-{CLICK_SAVER_SOURCE_COMMIT}.zip",
    },
)

MEDIAFIRE_ARTIFACTS = (
    {
        "id": "clicksaver-all-cdb-no-icons",
        "role": "CLICKSAVER_ALL_CDB_18_8_0_ARCHIVE",
        "url": (
            "https://www.mediafire.com/download/4tdx3vm9n4t9gm7/"
            "cs3-all-noicons-localdb-18-8-0.zip"
        ),
        "landing_path": "clicksaver/raw/cs3-all-noicons-localdb-18-8-0.mediafire.html",
        "path": "clicksaver/raw/cs3-all-noicons-localdb-18-8-0.zip",
    },
    {
        "id": "clicksaver-aodatabase-bdb",
        "role": "CLICKSAVER_AODATABASE_BDB_18_8_0_ARCHIVE",
        "url": (
            "https://www.mediafire.com/download/drjqk656a56jvjg/"
            "cs23-24-localdb-18-8-0.zip"
        ),
        "landing_path": "clicksaver/raw/cs23-24-localdb-18-8-0.mediafire.html",
        "path": "clicksaver/raw/cs23-24-localdb-18-8-0.zip",
    },
)


class AcquisitionError(RuntimeError):
    pass


class ConservativeFetcher:
    def __init__(self) -> None:
        self._last_request_by_site: dict[str, float] = {}

    @staticmethod
    def _site(url: str) -> str:
        hostname = (urlparse(url).hostname or "").lower()
        if hostname in {"arpa3.net", "www.arpa3.net", "javierarpa.com", "www.javierarpa.com"}:
            return "arpa3-family"
        return hostname

    def _delay(self, url: str) -> None:
        site = self._site(url)
        delay = ARPA_CRAWL_DELAY_SECONDS if site == "arpa3-family" else 0.5
        previous = self._last_request_by_site.get(site)
        if previous is not None:
            remaining = delay - (time.monotonic() - previous)
            if remaining > 0:
                time.sleep(remaining)

    def get(self, url: str, *, referer: str | None = None) -> dict[str, Any]:
        parsed = urlparse(url)
        if parsed.path.startswith("/cgi-bin"):
            raise AcquisitionError(f"CGI acquisition is forbidden by policy: {url}")

        self._delay(url)
        headers = {
            "Accept": "*/*",
            "Accept-Encoding": "identity",
            "User-Agent": USER_AGENT,
        }
        if referer:
            headers["Referer"] = referer
        request = Request(url, headers=headers, method="GET")
        try:
            with urlopen(request, timeout=120) as response:
                body = response.read()
                result = {
                    "requested_url": url,
                    "final_url": response.geturl(),
                    "http_status": response.status,
                    "response_headers": dict(response.headers.items()),
                    "request_headers": headers,
                    "body": body,
                }
        except (HTTPError, URLError, TimeoutError, OSError) as error:
            raise AcquisitionError(f"GET failed for {url}: {error}") from error
        finally:
            self._last_request_by_site[self._site(url)] = time.monotonic()
        return result


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def canonical_json(value: Any) -> bytes:
    return (json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n").encode("utf-8")


def write_artifact(root: Path, definition: dict[str, str], response: dict[str, Any], retrieved_at: str) -> dict[str, Any]:
    relative = Path(definition["path"])
    target = root / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    body = response["body"]
    target.write_bytes(body)
    return {
        "ArtifactId": definition["id"],
        "ByteLength": len(body),
        "FinalUrl": response["final_url"],
        "HttpStatus": response["http_status"],
        "RelativePath": relative.as_posix(),
        "RequestHeaders": response["request_headers"],
        "RequestedUrl": response["requested_url"],
        "ResponseHeaders": response["response_headers"],
        "RetrievedAtUtc": retrieved_at,
        "Role": definition["role"],
        "Sha256": sha256_bytes(body),
    }


def resolve_mediafire_download(landing_html: bytes) -> str:
    text = landing_html.decode("utf-8", errors="replace")
    candidates = re.findall(
        r'https://download[^"\'<> ]+?\.zip',
        html.unescape(text),
        flags=re.IGNORECASE,
    )
    if not candidates:
        raise AcquisitionError("MediaFire landing page did not expose a ZIP download URL")
    return candidates[0]


def acquire(output_root: Path, retrieved_at: str) -> None:
    fetcher = ConservativeFetcher()
    artifacts: list[dict[str, Any]] = []

    for definition in PUBLIC_ARTIFACTS:
        response = fetcher.get(definition["url"], referer=definition.get("referer"))
        artifacts.append(write_artifact(output_root, definition, response, retrieved_at))

    for definition in MEDIAFIRE_ARTIFACTS:
        landing = fetcher.get(definition["url"])
        landing_definition = {
            "id": definition["id"] + "-landing",
            "role": "DOWNLOAD_LANDING_HTML",
            "path": definition["landing_path"],
        }
        artifacts.append(write_artifact(output_root, landing_definition, landing, retrieved_at))
        resolved_url = resolve_mediafire_download(landing["body"])
        archive = fetcher.get(resolved_url, referer=definition["url"])
        archive["requested_url"] = definition["url"]
        archive["request_headers"]["Resolved-Download-Url"] = resolved_url
        artifacts.append(write_artifact(output_root, definition, archive, retrieved_at))

    manifest = {
        "AcquisitionPolicy": {
            "BackendBulkExtractionAttempted": False,
            "CgiPathsRequested": False,
            "ExternalHistoricalFilesAreRuntimeDependencies": False,
            "RobotsCrawlDelaySeconds": ARPA_CRAWL_DELAY_SECONDS,
            "RobotsDisallowCgi": True,
        },
        "Artifacts": artifacts,
        "ClickSaverSourceCommit": CLICK_SAVER_SOURCE_COMMIT,
        "ParserToolVersion": TOOL_VERSION,
        "RetrievedAtUtc": retrieved_at,
        "SchemaVersion": 1,
    }
    manifest_path = output_root / "source-manifest.json"
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_bytes(canonical_json(manifest))


def check(output_root: Path) -> None:
    manifest_path = output_root / "source-manifest.json"
    if not manifest_path.is_file():
        raise AcquisitionError(f"Source manifest is missing: {manifest_path}")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("SchemaVersion") != 1:
        raise AcquisitionError("Source manifest schema drift")
    if manifest.get("ParserToolVersion") != TOOL_VERSION:
        raise AcquisitionError("Source manifest tool version drift")
    if manifest.get("ClickSaverSourceCommit") != CLICK_SAVER_SOURCE_COMMIT:
        raise AcquisitionError("ClickSaver source commit drift")

    artifacts = manifest.get("Artifacts")
    if not isinstance(artifacts, list) or not artifacts:
        raise AcquisitionError("Source manifest has no artifacts")
    seen: set[str] = set()
    for artifact in artifacts:
        identity = artifact.get("ArtifactId")
        if not isinstance(identity, str) or not identity or identity in seen:
            raise AcquisitionError("Source manifest artifact identities are invalid")
        seen.add(identity)
        relative = artifact.get("RelativePath")
        if not isinstance(relative, str):
            raise AcquisitionError(f"Artifact path is invalid: {identity}")
        path = output_root / Path(relative)
        payload = path.read_bytes()
        if len(payload) != artifact.get("ByteLength"):
            raise AcquisitionError(f"Artifact byte length drift: {identity}")
        if sha256_bytes(payload) != artifact.get("Sha256"):
            raise AcquisitionError(f"Artifact SHA-256 drift: {identity}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-root", type=Path, default=DEFAULT_ROOT)
    action = parser.add_mutually_exclusive_group(required=True)
    action.add_argument("--write", action="store_true", help="perform live acquisition")
    action.add_argument("--check", action="store_true", help="validate the checked-in snapshot offline")
    parser.add_argument(
        "--retrieved-at",
        help="UTC acquisition timestamp used in the manifest (required with --write)",
    )
    args = parser.parse_args()

    if args.write:
        if not args.retrieved_at or not re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z", args.retrieved_at):
            raise AcquisitionError("--write requires canonical --retrieved-at YYYY-MM-DDTHH:MM:SSZ")
        acquire(args.output_root, args.retrieved_at)
    else:
        check(args.output_root)
    print("ARPA3_MISSION_EVIDENCE_ACQUISITION=PASS")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (AcquisitionError, OSError, ValueError, json.JSONDecodeError) as error:
        print(f"ARPA3 mission evidence acquisition failed: {error}", file=sys.stderr)
        raise SystemExit(1)
