"""Read-only, pinned evidence tools over the MCP stdio transport."""

import io
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
import time


SERVER = "AORebirth/Server/"
LIBRARY = "AORebirth/Libraries/Source/"
CODE_ROOTS = tuple(SERVER + name for name in (
    "ZoneEngine_New", "ZoneEngine_New.Tests", "LoginEngine", "ChatEngine", "WorldEngine", "WebEngine",
    "AccountBrokerService", "BotService",
)) + tuple(LIBRARY + name for name in (
    "AORebirth.Core", "AORebirth.Database", "AORebirth.Database.Schema", "AORebirth.Interfaces",
    "AORebirth.Enums", "AORebirth.Stats", "AORebirth.Communication", "AORebirth.AccountBroker",
    "AORebirth.BotService", "AORebirth.Modules.Abstractions", "AORebirth.ObjectManager",
    "AORebirth.DungeonGenerator", "AORebirth.World.Collision", "AORebirth.World.Contracts",
    "AORebirth.World.Package", "AORebirth.World.Pathfinding", "AOtomation/AOtomation.Messaging",
)) + ("Tools/MissionDaoValidation", "Tools/ZoneEngineSchemaValidation")
DOC_ROOTS = ("docs/ai", "docs/project", "docs/evidence", "docs/reference", "docs/generated", "docs/reports")
CODE_TYPES = (".cs", ".csproj", ".sql", ".props")
DOC_TYPES = (".md", ".csv", ".json", ".yaml", ".yml", ".txt")
PRIVATE_TYPES = {".json", ".csv", ".tsv", ".md", ".txt", ".log"}
SOURCE_PATTERNS = tuple(
    f":(glob,icase){root}/**/*{suffix}"
    for roots, suffixes in ((CODE_ROOTS, CODE_TYPES), (DOC_ROOTS, DOC_TYPES))
    for root in roots for suffix in suffixes
) + ("AGENTS.md", "AI_START_HERE.md", "Directory.Build.props")
MAX_SOURCE_BYTES = 8 * 1024 * 1024
MAX_PRIVATE_BYTES = 128 * 1024 * 1024
MAX_PRIVATE_SCAN = 160 * 1024 * 1024
MAX_PRIVATE_ENTRIES = 12_000
MAX_PAGE = 80
MAX_REQUEST = 65_536
MAX_CALLS = 80
TOOL_SECONDS = 25
BLOCKED_PATH = re.compile(
    r"(?:^|[._ -])(?:credentials?|secrets?|passwords?|private[-_. ]?keys?|tokens?|"
    r"raw[-_. ]?packets?|packets[-_. ]?hex|auth(?:entication|orization)?|login[-_. ]?packets?|"
    r"video|screen[-_. ]?recording)(?:$|[._ -])", re.I,
)
SECRET_LINE = re.compile(
    r"(?:\b(?:[a-z0-9]+_)*(?:password|passwd|secret|api[_ -]?key|access[_ -]?token|authorization)"
    r"[\"']?\s*[:=]\s*[\"']?[^\s\"']|\b(?:gh[pousr]_|github_pat_|sk-)[a-z0-9_-]{16,}"
    r"|-----BEGIN .*PRIVATE KEY-----|\b(?:raw|payload|packethex)\s*[:=]\s*[0-9a-f ]{64,})", re.I,
)
PROTOCOLS = {"2024-11-05", "2025-03-26", "2025-06-18", "2025-11-25"}


class EvidenceUnavailable(ValueError):
    pass


def git(source, *arguments, timeout=20):
    return subprocess.run(
        ["git", "--no-pager", "--no-replace-objects", "-C", str(source), *arguments], check=True,
        capture_output=True, timeout=timeout,
        env={key: value for key, value in os.environ.items() if not key.upper().startswith("GIT_")},
    ).stdout


def allowed_path(name):
    return all(part and not part.startswith(".") and not BLOCKED_PATH.search(part)
               for part in name.split("/")) and not any(char in name for char in "\\:\0")


def reparse(info):
    return stat.S_ISLNK(info.st_mode) or bool(getattr(info, "st_file_attributes", 0) & 0x400)


def is_under(name, prefix):
    name, prefix = name.casefold(), prefix.casefold()
    return not prefix or name == prefix or name.startswith(prefix + "/")


def allowed_source(name):
    if name in {"AGENTS.md", "AI_START_HERE.md", "Directory.Build.props"}:
        return True
    return (name.lower().endswith(CODE_TYPES) and any(is_under(name, root) for root in CODE_ROOTS)
            or name.lower().endswith(DOC_TYPES) and any(is_under(name, root) for root in DOC_ROOTS))


def private_file(info, allow_hardlinks=False):
    return (stat.S_ISREG(info.st_mode) and not reparse(info) and (allow_hardlinks or info.st_nlink == 1)
            and info.st_size <= MAX_PRIVATE_BYTES)


def remaining(deadline):
    budget = deadline - time.monotonic()
    if budget <= 0:
        raise ValueError("The evidence tool reached its time limit. Narrow the scope or page.")
    return budget


def source_inventory(source, commit):
    entries = git(source, "ls-tree", "-r", "-l", "-z", commit)
    results = {}
    for row in entries.split(b"\0"):
        if not row:
            continue
        metadata, name = row.decode("utf-8").split("\t", 1)
        mode, kind, oid, size = metadata.split()
        if (mode in {"100644", "100755"} and kind == "blob" and int(size) <= MAX_SOURCE_BYTES
                and allowed_path(name) and allowed_source(name)):
            results[name] = oid
    return results


def private_inventory(root, alias, allow_hardlinks):
    results = {}
    examined = 0
    for directory, children, names in os.walk(root):
        current = Path(directory)
        relative = current.relative_to(root)
        checked_children = []
        for name in sorted(children):
            if examined >= MAX_PRIVATE_ENTRIES:
                return results
            examined += 1
            if len(relative.parts) < 7 and allowed_path(name) and not reparse((current / name).lstat()):
                checked_children.append(name)
        children[:] = checked_children
        for name in sorted(names):
            if examined >= MAX_PRIVATE_ENTRIES:
                return results
            examined += 1
            path = current / name
            if path.suffix.lower() not in PRIVATE_TYPES or not allowed_path(name):
                continue
            info = path.lstat()
            if private_file(info, allow_hardlinks):
                results[alias + "/" + path.relative_to(root).as_posix()] = (root, path, allow_hardlinks)
    return results


def page_args(arguments):
    first = arguments.get("first", 1)
    last = arguments.get("last", first + MAX_PAGE - 1) if type(first) is int else None
    if type(first) is not int or type(last) is not int or not (1 <= first <= last <= first + MAX_PAGE - 1):
        raise ValueError("Use a positive inclusive range of at most 80 lines.")
    return first, last


def query_args(arguments, *, required):
    query = arguments.get("query", "")
    prefix = arguments.get("path", "")
    limit = arguments.get("limit", 12)
    if (not isinstance(query, str) or len(query) > 120 or (required and not query.strip())
            or any(ord(char) < 32 for char in query)):
        raise ValueError("Use a literal query of at most 120 characters.")
    if not isinstance(prefix, str) or (prefix and not allowed_path(prefix.rstrip("/"))):
        raise ValueError("Use a relative path or a private evidence alias.")
    if type(limit) is not int or not 1 <= limit <= 40:
        raise ValueError("Use a limit between 1 and 40.")
    return query, prefix.rstrip("/"), limit


class Explorer:
    def __init__(self, source, commit, audit, roots):
        if not re.fullmatch(r"[0-9a-f]{40}|[0-9a-f]{64}", commit):
            raise ValueError("Use a pinned full Git commit ID.")
        self.source = source.resolve(strict=True)
        if Path(os.fsdecode(git(self.source, "rev-parse", "--show-toplevel").strip())).resolve() != self.source:
            raise ValueError("Use a Git checkout root.")
        if git(self.source, "cat-file", "-t", commit).strip() != b"commit":
            raise ValueError("The pinned object must be a commit.")
        self.commit = commit
        self.audit = audit
        self.public = source_inventory(self.source, commit)
        self.private = {}
        self.aliases = []
        common_git_dir = Path(os.fsdecode(git(
            self.source, "rev-parse", "--path-format=absolute", "--git-common-dir",
        ).strip()))
        canonical_roots = {self.source / "GameData"}
        if common_git_dir.name == ".git":
            canonical_roots.add(common_git_dir.parent / "GameData")
        for index, directory in enumerate(roots, start=1):
            root = directory.resolve(strict=True)
            owned_evidence_root = (root.parent == self.source
                                   and root.name.casefold() in {"gamedata", "captures"})
            if (not root.is_dir() or root != directory.absolute() or reparse(root.lstat())
                    or root.drive.startswith("\\\\")
                    or (not owned_evidence_root and any(part.startswith(".") for part in root.parts[1:]))
                    or self.source.is_relative_to(root)):
                raise ValueError("Private evidence roots must be approved real directories.")
            alias = f"evidence-{index}"
            self.aliases.append(alias)
            # Canonical GameData is installed with regular hard-linked files. Other evidence roots
            # may contain operator exports and do not get that exception.
            self.private.update(private_inventory(root, alias, root in canonical_roots))

    def record(self, tool, name, first, last, *, private=False):
        self.audit.write(json.dumps({"tool": tool, "path": name, "first": first, "last": last,
                                     "private": private or name in self.private}) + "\n")
        self.audit.flush()

    def open(self, name, deadline):
        if name in self.public:
            stream = io.BytesIO(git(self.source, "cat-file", "blob", self.public[name],
                                    timeout=remaining(deadline)))
        elif name in self.private:
            root, path, allow_hardlinks = self.private[name]
            info = path.lstat()
            if (not private_file(info, allow_hardlinks)
                    or path.resolve(strict=True) != path or not path.is_relative_to(root)):
                raise EvidenceUnavailable("The approved evidence file changed or is unavailable.")
            stream = path.open("rb")
        else:
            raise EvidenceUnavailable("The path is outside the evidence index. Use discover first.")
        header = stream.read(4096)
        stream.seek(0)
        if b"\0" in header or b"PRIVATE KEY-----" in header:
            stream.close()
            raise EvidenceUnavailable("Binary or key material is excluded from evidence.")
        return stream

    def discover(self, arguments):
        query, prefix, limit = query_args(arguments, required=False)
        names = sorted(name for name in (*self.public, *self.private)
                       if is_under(name, prefix) and query.lower() in name.lower())
        for alias in self.aliases:
            if is_under(prefix, alias) or is_under(alias, prefix):
                self.record("discover", alias, 0, 0, private=True)
        return {"paths": names[:limit], "more": len(names) > limit, "private_roots": self.aliases}

    def search(self, arguments):
        query, prefix, limit = query_args(arguments, required=True)
        deadline = time.monotonic() + TOOL_SECONDS
        needle = query.encode("utf-8").lower()
        paths = []
        if not any(is_under(prefix, alias) for alias in self.aliases):
            try:
                rows = git(self.source, "grep", "-l", "-z", "-I", "-i", "-F", "-e", query,
                           self.commit, "--", *SOURCE_PATTERNS, timeout=remaining(deadline))
            except subprocess.CalledProcessError as error:
                if error.returncode != 1:
                    raise
                rows = b""
            tree_prefix = self.commit + ":"
            paths = [row.removeprefix(tree_prefix) for row in rows.decode("utf-8").split("\0")
                     if row.startswith(tree_prefix)]
            paths = [name for name in paths if name in self.public and is_under(name, prefix)]
        elif prefix:
            self.record("search", prefix, 0, 0, private=True)
            paths = [name for name in self.private if is_under(name, prefix)]

        results = []
        scanned = 0
        paths.sort(key=lambda name: (query.lower() not in Path(name).stem.lower(), name))
        for name in paths:
            remaining(deadline)
            if scanned >= MAX_PRIVATE_SCAN or len(results) >= limit:
                break
            matches = []
            try:
                with self.open(name, deadline) as stream:
                    for index, line in enumerate(stream, start=1):
                        if index % 1024 == 0:
                            remaining(deadline)
                        scanned += len(line)
                        if needle in line.lower() and not SECRET_LINE.search(line.decode("utf-8", "replace")):
                            matches.append(index)
                        if len(matches) >= 6 or scanned >= MAX_PRIVATE_SCAN:
                            break
            except (OSError, EvidenceUnavailable):
                continue
            if matches:
                results.append({"path": name, "lines": matches})
                self.record("search", name, matches[0], matches[-1])
        return {"matches": results, "bounded": scanned >= MAX_PRIVATE_SCAN or len(results) >= limit}

    def read(self, arguments):
        name = arguments.get("path")
        if not isinstance(name, str):
            raise ValueError("Choose an exact path returned by discover or search.")
        first, last = page_args(arguments)
        deadline = time.monotonic() + TOOL_SECONDS
        excerpt = []
        span = None
        next_line = None
        with self.open(name, deadline) as stream:
            for index, raw in enumerate(stream, start=1):
                if index % 1024 == 0:
                    remaining(deadline)
                if index < first:
                    continue
                if index > last:
                    next_line = index
                    break
                line = raw.decode("utf-8", "replace").rstrip("\r\n")
                if b"\0" in raw or SECRET_LINE.search(line):
                    line = "[redacted]"
                    visible = False
                elif len(line) > 500:
                    line = line[:500] + " [line truncated]"
                    visible = False
                else:
                    visible = True
                if visible and span is None:
                    span = index
                if not visible and span is not None:
                    self.record("read", name, span, index - 1)
                    span = None
                excerpt.append(f"{index}: {line}")
        if span is not None:
            self.record("read", name, span, first + len(excerpt) - 1)
        elif name in self.private:
            self.record("read_scope", name, 0, 0)
        return {"path": name, "excerpt": "\n".join(excerpt), "next": next_line}


PATH_SCHEMA = {"type": "string", "description": "Exact path or prefix, using an evidence-N alias for private files."}
QUERY_PROPERTIES = {"query": {"type": "string"}, "path": PATH_SCHEMA,
                    "limit": {"type": "integer", "minimum": 1, "maximum": 40}}
TOOLS = [
    {"name": "search", "description": "Find a literal symbol or phrase. Search the pinned Git tree by default, or an explicit private evidence alias. Returns matching paths and line numbers.",
     "inputSchema": {"type": "object", "properties": QUERY_PROPERTIES, "required": ["query"], "additionalProperties": False}},
    {"name": "discover", "description": "List approved file paths by name substring and optional path prefix. Also lists available private root aliases.",
     "inputSchema": {"type": "object", "properties": QUERY_PROPERTIES, "additionalProperties": False}},
    {"name": "read", "description": "Read up to 80 numbered lines from an exact discovered path. Use next to continue sequentially.",
     "inputSchema": {"type": "object", "properties": {"path": PATH_SCHEMA, "first": {"type": "integer"},
                                                        "last": {"type": "integer"}},
                     "required": ["path"], "additionalProperties": False}},
]
for tool in TOOLS:
    tool["annotations"] = {"readOnlyHint": True, "destructiveHint": False, "openWorldHint": False}


def send(request_id, *, result=None, error=None):
    message = {"jsonrpc": "2.0", "id": request_id}
    message["error" if error else "result"] = error if error else result
    sys.stdout.buffer.write((json.dumps(message, ensure_ascii=False) + "\n").encode("utf-8"))
    sys.stdout.buffer.flush()


def serve(explorer):
    stream = sys.stdin.buffer
    methods = {"discover": explorer.discover, "search": explorer.search, "read": explorer.read}
    calls = 0
    while line := stream.readline(MAX_REQUEST + 1):
        if len(line) > MAX_REQUEST:
            while line and not line.endswith(b"\n"):
                line = stream.readline(MAX_REQUEST + 1)
            send(None, error={"code": -32600, "message": "Request too large"})
            continue
        try:
            request = json.loads(line)
        except (UnicodeError, ValueError):
            send(None, error={"code": -32700, "message": "Invalid JSON"})
            continue
        if not isinstance(request, dict) or request.get("jsonrpc") != "2.0":
            send(None, error={"code": -32600, "message": "Invalid request"})
            continue
        if "id" not in request:
            continue
        request_id = request["id"]
        method = request.get("method")
        params = request.get("params", {})
        if not isinstance(params, dict):
            send(request_id, error={"code": -32602, "message": "Invalid parameters"})
        elif method == "initialize":
            version = params.get("protocolVersion")
            send(request_id, result={"protocolVersion": version if isinstance(version, str) and version in PROTOCOLS else "2025-06-18",
                                     "capabilities": {"tools": {}}, "serverInfo": {"name": "aorebirth-evidence", "version": "1.0.0"}})
        elif method == "ping":
            send(request_id, result={})
        elif method == "tools/list":
            send(request_id, result={"tools": TOOLS})
        elif method == "tools/call":
            calls += 1
            if calls > MAX_CALLS:
                send(request_id, error={"code": -32000, "message": "Evidence tool call limit reached"})
                continue
            name, args = params.get("name"), params.get("arguments", {})
            if not isinstance(name, str) or name not in methods or not isinstance(args, dict):
                send(request_id, error={"code": -32602, "message": "Invalid tool or arguments"})
                continue
            failed = False
            try:
                result = methods[name](args)
            except ValueError as error:
                result = {"error": str(error)}
                failed = True
            except (OSError, subprocess.SubprocessError):
                result = {"error": "Evidence could not be read within the configured bounds."}
                failed = True
            send(request_id, result={"content": [{"type": "text", "text": json.dumps(result, ensure_ascii=False)}],
                                     "isError": failed})
        else:
            send(request_id, error={"code": -32601, "message": "Method not found"})


def main():
    if not 4 <= len(sys.argv) <= 10:
        raise SystemExit("Usage: explore.py <source> <commit> <audit-file> [<evidence-root> ...]")
    source, commit, audit_path, *roots = sys.argv[1:]
    try:
        path = Path(audit_path).absolute()
        if path.resolve() != path or (path.exists() and not private_file(path.lstat())):
            raise ValueError("The audit file must be a direct regular file.")
        with path.open("a", encoding="utf-8") as audit:
            serve(Explorer(Path(source), commit, audit, [Path(root) for root in roots]))
    except (OSError, ValueError, subprocess.SubprocessError):
        raise SystemExit("Could not initialize the pinned evidence explorer.") from None


if __name__ == "__main__":
    main()
