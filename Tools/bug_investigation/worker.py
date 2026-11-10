"""Poll the AORebirth bug feed and record local, read-only investigations."""

import argparse
from contextlib import closing, contextmanager, nullcontext
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import sqlite3
import subprocess
import sys
import tempfile
import time
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode, urlsplit
from urllib.request import HTTPRedirectHandler, ProxyHandler, Request, build_opener

import triage


WORKER_ROOT = Path(__file__).resolve().parents[2]


class WorkerError(Exception):
    pass


class FeedError(WorkerError):
    pass


class InvestigationError(WorkerError):
    pass


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def valid_id(value):
    return type(value) is int and 0 < value <= 2_147_483_647


def report_record(row, token=None):
    if not isinstance(row, dict) or not valid_id(row.get("id")):
        raise FeedError("Invalid report ID.")
    fields = ("title", "description", "location", "frequency", "submitted_at")
    if any(not isinstance(row.get(field), str) for field in fields):
        raise FeedError("Invalid report text fields.")
    if any(row.get(field) is not None and not isinstance(row[field], str)
           for field in ("frequency_other", "attachment")):
        raise FeedError("Invalid optional report fields.")
    report = {field: row[field] for field in fields}
    report.update(id=row["id"], frequency_other=row.get("frequency_other"),
                  has_attachment=bool(row.get("attachment")))
    if token:
        report = {
            field: value.replace(token, "[redacted]") if isinstance(value, str) else value
            for field, value in report.items()
        }
    fingerprint = hashlib.sha256(
        json.dumps([report, row.get("attachment")], sort_keys=True).encode("utf-8")
    ).hexdigest()
    return report, fingerprint


class Feed:
    def __init__(self, url, token_file, synthetic):
        endpoint = urlsplit(url)
        if (not endpoint.hostname or endpoint.username is not None
                or endpoint.path != "/bugs/v1.php" or endpoint.query or endpoint.fragment):
            raise FeedError("Expected a bug feed URL ending in /bugs/v1.php.")
        if synthetic:
            if endpoint.scheme != "http" or endpoint.hostname not in {"127.0.0.1", "::1"}:
                raise FeedError("Synthetic mode requires a loopback HTTP feed.")
        elif endpoint.scheme != "https":
            raise FeedError("The bug feed requires HTTPS.")
        self.url = url
        with token_file.open("r", encoding="ascii") as file:
            self.token = file.read(256).strip()
        if not re.fullmatch(r"[0-9a-fA-F]{64}", self.token):
            raise FeedError("The bearer file must contain a 64-character hex token.")
        self.opener = build_opener(ProxyHandler({}), NoRedirect())

    def get(self, params, *, missing_ok=False):
        request = Request(
            self.url + "?" + urlencode(params),
            headers={"Authorization": "Bearer " + self.token, "Accept": "application/json"},
        )
        try:
            with self.opener.open(request, timeout=20) as response:
                data = response.read(4_194_305)
            if len(data) > 4_194_304:
                raise FeedError("Feed response exceeds the size limit.")
            page = json.loads(data)
        except HTTPError as error:
            error.close()
            if missing_ok and error.code == 404:
                return None
            raise FeedError(f"Feed returned HTTP {error.code}.") from None
        except (URLError, TimeoutError, OSError, ValueError):
            raise FeedError("Feed request failed or returned invalid JSON.") from None
        if not isinstance(page, dict):
            raise FeedError("Invalid feed response.")
        return page

    def pages(self):
        after_id = 0
        limit = 50
        while True:
            page = self.get({"after_id": after_id, "limit": limit})
            rows = page.get("reports")
            if not isinstance(rows, list) or len(rows) > limit or "next_after_id" not in page:
                raise FeedError("Invalid feed page.")
            records = [report_record(row, self.token) for row in rows]
            ids = [report["id"] for report, _ in records]
            if ids != sorted(set(ids)) or (ids and ids[0] <= after_id):
                raise FeedError("Feed report IDs did not advance.")
            cursor = page["next_after_id"]
            if cursor is not None and (not valid_id(cursor) or not ids or cursor != ids[-1]):
                raise FeedError("Invalid feed continuation.")
            yield records
            if cursor is None:
                return
            after_id = cursor

    def detail(self, report_id):
        response = self.get({"id": report_id}, missing_ok=True)
        if response is None:
            return None
        report, fingerprint = report_record(response.get("report"), self.token)
        if report["id"] != report_id:
            raise FeedError("Feed returned the wrong report ID.")
        return report, fingerprint


def initialize(db):
    db.row_factory = sqlite3.Row
    db.executescript("""
        CREATE TABLE IF NOT EXISTS feed (url TEXT PRIMARY KEY);
        CREATE TABLE IF NOT EXISTS jobs (
            id INTEGER PRIMARY KEY,
            fingerprint TEXT NOT NULL,
            payload TEXT NOT NULL,
            state TEXT NOT NULL CHECK (state IN ('pending', 'done', 'closed')),
            retry_at INTEGER NOT NULL DEFAULT 0,
            failures INTEGER NOT NULL DEFAULT 0,
            result TEXT
        );
        CREATE TABLE IF NOT EXISTS triage (
            id INTEGER PRIMARY KEY,
            fingerprint TEXT NOT NULL,
            source_commit TEXT NOT NULL,
            rounds TEXT NOT NULL
        );
    """)
    triage.initialize(db)


def enqueue(db, report, fingerprint):
    db.execute("""
        INSERT INTO jobs (id, fingerprint, payload, state)
        VALUES (?, ?, ?, 'pending')
        ON CONFLICT(id) DO UPDATE SET fingerprint = excluded.fingerprint,
            payload = excluded.payload, state = 'pending', retry_at = 0, failures = 0,
            result = NULL
        WHERE jobs.fingerprint <> excluded.fingerprint
            OR (jobs.state = 'closed' AND jobs.result IS NULL)
    """, (report["id"], fingerprint, json.dumps(report)))


def codex_environment():
    # The CLI can use its saved ChatGPT login. Inherited API and service tokens
    # are intentionally excluded, including from the desktop launcher.
    names = {
        "PATH", "SYSTEMROOT", "WINDIR", "SYSTEMDRIVE", "COMSPEC", "USERPROFILE",
        "HOMEDRIVE", "HOMEPATH", "HOME", "APPDATA", "LOCALAPPDATA", "PROGRAMDATA",
        "PROGRAMFILES", "PROGRAMFILES(X86)", "TEMP", "TMP", "CODEX_HOME",
    }
    return {key: value for key, value in os.environ.items() if key.upper() in names}


def run_codex(args, schema, prompt, context, *, explore=False, public_only=False):
    with tempfile.TemporaryDirectory(prefix="aor-bug-codex-") as directory:
        scratch = Path(directory)
        schema_path = scratch / "schema.json"
        output_path = scratch / "result.json"
        audit_path = scratch / "source-reads.jsonl"
        servers = "mcp_servers={}"
        if explore:
            config = [
                sys.executable, "-B", str(Path(__file__).with_name("explore.py").resolve()),
                str(args.source), context["source_commit"], str(audit_path),
                *([] if public_only else map(str, args.evidence_dir)),
            ]
            servers = (
                'mcp_servers={source={required=true,startup_timeout_sec=20,tool_timeout_sec=35,'
                'default_tools_approval_mode="approve",'
                f"command={json.dumps(config[0], ensure_ascii=False)},"
                f"args={json.dumps(config[1:], ensure_ascii=False)}"
                "}}"
            )
        schema_path.write_text(json.dumps(schema), encoding="utf-8")
        command = [
            args.codex, "-a", "never", "exec", "--skip-git-repo-check",
            "--sandbox", "read-only", "--ignore-user-config", "--ephemeral",
            "--disable", "shell_tool", "--disable", "hooks", "--disable", "shell_snapshot",
            "--disable", "skill_search", "--disable", "view_image",
            "--disable", "apps", "--disable", "plugins", "--disable", "remote_plugin",
            "-c", 'web_search="disabled"', "-c", servers,
            "--output-schema", str(schema_path), "--output-last-message", str(output_path),
            prompt,
        ]
        try:
            completed = subprocess.run(
                command, cwd=scratch, env=codex_environment(), input=json.dumps(context),
                text=True, encoding="utf-8", stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL, timeout=args.timeout, check=False,
            )
            if completed.returncode:
                raise InvestigationError(f"Codex exited with code {completed.returncode}.")
            if output_path.stat().st_size > 65_536:
                raise InvestigationError("Codex result exceeds the size limit.")
            payload = json.loads(output_path.read_text(encoding="utf-8"))
            if not explore:
                return payload
            reads = []
            if audit_path.is_file():
                reads = [json.loads(line) for line in audit_path.read_text(encoding="utf-8").splitlines()]
            return payload, [item for item in reads if item["tool"] == "read" or item["private"]]
        except subprocess.TimeoutExpired:
            raise InvestigationError("Codex investigation timed out.") from None
        except (OSError, ValueError):
            raise InvestigationError("Codex did not produce a readable JSON result.") from None

def investigate(db, args, report, fingerprint, previous):
    try:
        commit = args.repair_base or subprocess.check_output(
            ["git", "-C", str(args.source), "rev-parse", "--verify", "HEAD"],
            env=git_environment(), stderr=subprocess.DEVNULL, text=True, timeout=15,
        ).strip()

        def invoke(schema, prompt, context, **options):
            return run_codex(args, schema, prompt, context, explore="symptom" in context, **options)

        return triage.investigate(db, (report["id"], fingerprint, commit), report, previous, invoke,
                                  workers=args.symptom_workers)
    except (OSError, subprocess.SubprocessError, ValueError, TypeError, KeyError) as error:
        raise InvestigationError("Could not verify the source investigation.") from error


def process_pending(db, feed, args, *, selected=None):
    failed = False
    selected_id = selected[0]["id"] if selected else None
    while True:
        job = db.execute("""
            SELECT id, result FROM jobs WHERE state = 'pending' AND retry_at <= ?
            AND (? IS NULL OR id = ?)
            ORDER BY retry_at, id LIMIT 1
        """, (int(time.time()), selected_id, selected_id)).fetchone()
        if job is None:
            return failed
        report_id = job["id"]
        record = selected if selected is not None else feed.detail(report_id)
        if record is None:
            with db:
                db.execute("UPDATE jobs SET state = 'closed' WHERE id = ?", (report_id,))
            continue
        report, fingerprint = record
        with db:
            enqueue(db, report, fingerprint)
        try:
            print(f"Investigating bug {report_id}.", flush=True)
            saved = db.execute("SELECT result FROM jobs WHERE id = ?", (report_id,)).fetchone()[0]
            previous = json.loads(saved) if saved else None
            result = investigate(db, args, report, fingerprint, previous)
        except InvestigationError as error:
            with db:
                failures = db.execute("SELECT failures FROM jobs WHERE id = ?", (report_id,)).fetchone()[0]
                retry_at = int(time.time()) + min(300, 15 * (2 ** min(failures, 5)))
                db.execute(
                    "UPDATE jobs SET failures = failures + 1, retry_at = ? WHERE id = ?",
                    (retry_at, report_id),
                )
            print(f"Bug {report_id} remains queued. {error}", file=sys.stderr, flush=True)
            failed = True
            continue
        incomplete = result["classification"] == triage.INCOMPLETE
        with db:
            db.execute("UPDATE jobs SET state = ?, retry_at = ?, result = ? WHERE id = ?",
                       ("pending" if incomplete else "done", 2**63 - 1 if incomplete else 0,
                        json.dumps(result), report_id))
        print(f"Bug {report_id}: {result['classification']}", flush=True)


@contextmanager
def exclusive_sweep(path):
    with path.with_suffix(path.suffix + ".lock").open("a+b") as handle:
        handle.seek(0)
        try:
            if os.name == "nt":
                import msvcrt
                msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError:
            raise WorkerError("Another worker is processing this queue.") from None
        yield


def run(db, args):
    from repair import RepairError, prepare, process_repairs

    def propose(*arguments):
        try:
            if args.plan["mode"] == "aorebirth":
                proposal, _ = run_codex(*arguments, explore=True, public_only=True)
                return proposal
            return run_codex(*arguments)
        except InvestigationError as error:
            raise RepairError(str(error)) from None

    feed = Feed(args.api_url, args.token_file, args.synthetic)
    with db:
        db.execute("BEGIN IMMEDIATE")
        saved = db.execute("SELECT url FROM feed").fetchone()
        if saved is None:
            db.execute("INSERT INTO feed VALUES (?)", (feed.url,))
        elif saved[0] != feed.url:
            raise WorkerError("This queue belongs to a different feed URL.")
    while True:
        try:
            with exclusive_sweep(args.db):
                selected = None
                if args.report_id is not None:
                    selected = feed.detail(args.report_id)
                    if selected is None:
                        raise FeedError(f"Report {args.report_id} is not open or accessible.")
                args.repair_base = prepare(args) if args.repair_plan else None
                if selected is not None:
                    with db:
                        enqueue(db, *selected)
                else:
                    # Sweep from zero to recover reopened or out-of-order reports.
                    for records in feed.pages():
                        with db:
                            for report, fingerprint in records:
                                enqueue(db, report, fingerprint)
                failed = process_pending(db, feed, args, selected=selected)
                if args.repair_plan:
                    failed = process_repairs(
                        db, args, feed, propose, selected_id=args.report_id,
                    ) or failed
        except FeedError as error:
            print(error, file=sys.stderr, flush=True)
            failed = True
        if args.poll_seconds == 0:
            return int(failed)
        time.sleep(args.poll_seconds)


def positive(value):
    number = int(value)
    if number < 1:
        raise argparse.ArgumentTypeError("Expected a positive integer.")
    return number


def git_environment():
    return {key: value for key, value in os.environ.items() if not key.upper().startswith("GIT_")}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--db", required=True, type=Path, help="Private local SQLite file.")
    commands = parser.add_subparsers(dest="command", required=True)
    polling = commands.add_parser("run")
    polling.add_argument("--api-url", required=True)
    polling.add_argument("--token-file", required=True, type=Path)
    polling.add_argument("--source", type=Path, default=WORKER_ROOT)
    polling.add_argument("--evidence-dir", type=Path, action="append", default=[])
    polling.add_argument("--synthetic", action="store_true", help="Allow only a loopback HTTP fixture.")
    polling.add_argument("--repair-plan", type=Path, help="Trusted local JSON plan enabling draft repairs.")
    polling.add_argument("--report-id", type=positive, help="Retrieve and process only this open report.")
    polling.add_argument("--timeout", type=positive, default=180, help="Codex timeout in seconds.")
    polling.add_argument("--symptom-workers", type=positive, choices=range(1, 4), default=1,
                         help="Concurrent read-only symptom investigations within one report.")
    polling.add_argument("--poll-seconds", type=int, default=0, help="Zero runs one sweep.")
    commands.add_parser("show").add_argument("id", type=positive)
    requeue = commands.add_parser("requeue")
    requeue.add_argument("id", type=positive)
    requeue.add_argument("--symptom-id", type=positive, help="Revisit only this tracked symptom.")
    args = parser.parse_args()
    args.db = args.db.resolve()
    if args.command != "run" and not args.db.is_file():
        raise WorkerError("Queue database does not exist.")
    if args.command == "run":
        if args.poll_seconds < 0:
            parser.error("--poll-seconds must be nonnegative.")
        if args.report_id is not None and (not valid_id(args.report_id) or args.poll_seconds):
            parser.error("--report-id requires a valid website ID and a single sweep.")
        args.source = args.source.resolve(strict=True)
        if not (args.source / "AORebirth/Server/ZoneEngine_New").is_dir():
            raise WorkerError("The source must be an AORebirth checkout with ZoneEngine_New.")
        root = subprocess.check_output(
            ["git", "-C", str(args.source), "rev-parse", "--show-toplevel"],
            env=git_environment(), stderr=subprocess.DEVNULL, text=True, timeout=15,
        ).strip()
        if Path(root).resolve() != args.source:
            raise WorkerError("The source path must be the Git checkout root.")
        args.evidence_dir = [path.resolve(strict=True) for path in args.evidence_dir]
        if any(not path.is_dir() for path in args.evidence_dir):
            raise WorkerError("Each evidence path must be an approved directory.")
        for name in ("GameData", "Captures"):
            if any(path.name.casefold() == name.casefold() for path in args.evidence_dir):
                continue
            candidate = args.source / name
            if candidate.is_dir() and candidate.resolve().is_relative_to(args.source):
                args.evidence_dir.append(candidate.resolve())
        if len(args.evidence_dir) > 6:
            raise WorkerError("Specify at most six approved evidence directories.")
        args.token_file = args.token_file.resolve(strict=True)
        if args.repair_plan:
            args.repair_plan = args.repair_plan.resolve(strict=True)
        roots = [args.source, WORKER_ROOT, *args.evidence_dir]
        private_paths = [args.token_file, args.db]
        if args.repair_plan:
            private_paths.append(args.repair_plan)
        if any(path.is_relative_to(root) for path in private_paths for root in roots):
            raise WorkerError("Keep the queue, bearer file, and repair plan outside source and evidence folders.")
        if len(set(private_paths)) != len(private_paths):
            raise WorkerError("The bearer file, queue, and repair plan must be separate.")
        args.codex = shutil.which("codex")
        if args.codex is None:
            raise WorkerError("Codex CLI is not on PATH.")
        args.db.parent.mkdir(parents=True, exist_ok=True)

    from repair import initialize_repairs, read_repair, read_symptom_repairs

    with closing(sqlite3.connect(args.db, timeout=3)) as db:
        initialize(db)
        initialize_repairs(db)
        if args.command == "run":
            return run(db, args)
        lock = exclusive_sweep(args.db) if args.command == "requeue" else nullcontext()
        with lock, db:
            job = db.execute("SELECT state, failures, result FROM jobs WHERE id = ?", (args.id,)).fetchone()
            if job is None:
                raise WorkerError("Unknown report ID.")
            if args.command == "requeue":
                matched = triage.requeue(db, args.id, args.symptom_id)
                if args.symptom_id is not None and not matched:
                    raise WorkerError("Unknown symptom for the current report and source.")
                db.execute("UPDATE jobs SET state = 'pending', retry_at = 0, failures = 0 WHERE id = ?",
                           (args.id,))
                print(f"Requeued bug {args.id}.")
            else:
                print(json.dumps({
                    "id": args.id, "state": job["state"], "failures": job["failures"],
                    "result": json.loads(job["result"]) if job["result"] else None,
                    "repair": read_repair(db, args.id),
                    "symptom_repairs": read_symptom_repairs(db, args.id),
                    "investigation_progress": triage.progress(db, args.id),
                }, indent=2))
    return 0


if __name__ == "__main__":
    os.umask(0o077)
    try:
        sys.exit(main())
    except (WorkerError, OSError, RuntimeError, sqlite3.Error, subprocess.SubprocessError, ValueError) as error:
        print(f"Bug worker: {error}", file=sys.stderr)
        sys.exit(1)
    except KeyboardInterrupt:
        sys.exit(130)
