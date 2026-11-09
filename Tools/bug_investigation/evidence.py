"""Select small, local evidence excerpts without using report-supplied paths."""

from collections import Counter, defaultdict
import os
from pathlib import Path
import re
import stat
import subprocess

SOURCE_PATHS = (
    *(
        f":(glob){root}/**/*.cs" for root in (
            "AORebirth/Server/ZoneEngine_New", "AORebirth/Server/ZoneEngine_New.Tests",
            "AORebirth/Server/ChatEngine",
            "AORebirth/Server/LoginEngine", "AORebirth/Server/WorldEngine",
            "AORebirth/Libraries/Source/AORebirth.Core",
            "AORebirth/Libraries/Source/AORebirth.Database",
            "AORebirth/Libraries/Source/AORebirth.Interfaces",
        )
    ),
    ":(glob)AORebirth/Libraries/Source/AORebirth.Database/**/*.sql",
    ":(glob)docs/reference/captures/**/*.md", "docs/reference/client-dll-function-map/README.md",
    "docs/reference/packets/StripdownDirectRepairCandidates.md",
    "docs/generated/aosharp_capture_inventory.csv",
)
STOP_WORDS = set("""
about after again also and any appears are attached automated before bug can could does
each file first for from game get have how ignore instructions into investigator issue
just like mandatory may member next not one only operating other report rules
server should some than that the their them then there this time two used was were
what when where which while will with would you your
""".split())
PRIVATE_TEXT_TYPES = {".csv", ".json", ".log", ".md", ".tsv", ".txt", ".xml"}
PRIVATE_NAME = re.compile(r"password|credential|token|secret|raw[-_]packets|packets\.hex", re.I)
PRIVATE_LINE = re.compile(r"\b(?:authorization|password|api[_ -]?key|secret|token)\b[\"']?\s*[:=]", re.I)


def keywords(report):
    words = Counter()
    for field, weight in (("title", 5), ("description", 1), ("location", 2)):
        tokens = set(re.findall(r"[a-z][a-z0-9_]{2,}", report[field].lower()))
        for token in tokens - STOP_WORDS:
            words[token] += weight
    return dict(sorted(words.items(), key=lambda item: (-item[1], item[0]))[:18])


def git(source, *args):
    return subprocess.run(
        ["git", "-C", str(source), *args], capture_output=True, timeout=20,
        env={key: value for key, value in os.environ.items() if not key.upper().startswith("GIT_")},
    )


def score(text, terms):
    text = text.lower()
    return sum(weight for word, weight in terms.items() if word in text)


def display_line(line):
    return "[credential-like line redacted]" if PRIVATE_LINE.search(line) else line[:320]


def excerpts(path, lines, terms):
    scored = [(score(line, terms), i) for i, line in enumerate(lines)]
    ranked = sorted((-rank, i) for rank, i in scored if rank)
    indices = dict.fromkeys(
        i for _, index in ranked[:7]
        for i in range(max(0, index - 3), min(len(lines), index + 12))
    )
    return {"path": path, "excerpt": "\n".join(
        f"{i + 1}: {display_line(lines[i])}" for i in indices
    )[:3400]}


def git_matches(source, commit, terms):
    patterns = [arg for word in terms for arg in ("-e", word)]
    search = git(source, "grep", "-I", "-i", "-F", "-n", "-z", "-m", "64", *patterns,
                 commit, "--", *SOURCE_PATHS)
    if search.returncode not in (0, 1):
        raise ValueError("Git evidence search failed.")
    matches = defaultdict(list)
    for row in search.stdout.decode("utf-8", "replace").splitlines():
        parts = row.split("\0", 2)
        if len(parts) == 3 and parts[0].startswith(commit + ":"):
            path = parts[0][len(commit) + 1:]
            matches[path].append(parts[2][:320])
    return matches


def tracked_evidence(source, commit, terms):
    matches = git_matches(source, commit, terms)
    ranked = sorted(matches, key=lambda path: (
        -(score(path, terms) * 2 + score(" ".join(matches[path]), terms)), path,
    ))
    selected = {path: terms for path in ranked[:8]}
    # Follow a few matching method names to their owners across repository layers.
    symbols = {
        name: score(name, terms)
        for path in ranked[:3] if path.endswith(".cs")
        for line in matches[path]
        for name in re.findall(r"\b_[A-Za-z0-9_]+\.([A-Z][A-Za-z0-9]{8,})\s*\(", line)
        if score(name, terms)
    }
    names = sorted(symbols, key=lambda name: (-symbols[name], name))[:6]
    if names:
        related = git_matches(source, commit, names)
        related_owners = sorted(
            (path for path in related if path not in selected and path.endswith(".cs")),
            key=lambda path: (
                -sum(bool(re.search(r"\b(public|private|internal|protected)\b", line))
                     for line in related[path]),
                -len(related[path]), path,
            ),
        )
        selected.update((path, {**terms, **{name.lower(): 20 for name in names}})
                        for path in related_owners[:2])
    result = []
    for path, excerpt_terms in selected.items():
        size = git(source, "cat-file", "-s", f"{commit}:{path}")
        if size.returncode or int(size.stdout) > 1_048_576:
            continue
        blob = git(source, "show", f"{commit}:{path}")
        if blob.returncode == 0:
            lines = blob.stdout.decode("utf-8", "replace").splitlines()
            result.append(excerpts(path, lines, excerpt_terms))
    return result


def private_evidence(roots, terms):
    candidates = []
    for root in roots:
        checked = 0
        for current, dirs, files in os.walk(root):
            directory = Path(current)
            dirs[:] = [name for name in sorted(dirs)
                       if not name.startswith(".") and not is_reparse_point(directory / name)]
            for name in sorted(files):
                checked += 1
                if checked > 2000:
                    break
                path = directory / name
                if (path.suffix.lower() not in PRIVATE_TEXT_TYPES or name.startswith(".")
                        or PRIVATE_NAME.search(name)):
                    continue
                try:
                    if (is_reparse_point(path) or not path.is_file()
                            or not path.resolve().is_relative_to(root)):
                        continue
                    with path.open("rb") as stream:
                        text = stream.read(65_537)
                except OSError:
                    continue
                if len(text) > 65_536 or b"\0" in text:
                    continue
                decoded = text.decode("utf-8", "replace")
                rank = score(decoded, terms) + 2 * score(name, terms)
                if rank:
                    candidates.append((rank, path, decoded))
                    candidates.sort(key=lambda item: (-item[0], str(item[1])))
                    del candidates[4:]
            if checked > 2000:
                break
    result = [excerpts(str(path), text.splitlines(), terms) for _, path, text in candidates]
    return [item for item in result if item["excerpt"]]


def is_reparse_point(path):
    try:
        info = path.lstat()
        return stat.S_ISLNK(info.st_mode) or bool(
            getattr(info, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT
        )
    except OSError:
        return True


def collect_evidence(source, source_commit, evidence_dirs, report):
    terms = keywords(report)
    if not terms:
        return []
    return tracked_evidence(source, source_commit, terms) + private_evidence(evidence_dirs, terms)

def source_path_allowed(name):
    for spec in SOURCE_PATHS:
        prefix, marker, suffix = spec.removeprefix(":(glob)").partition("/**/*")
        if (marker and name.startswith(prefix + "/") and name.endswith(suffix)) or name == spec:
            return True
    return False


def requested_file(source, commit, roots, request, known_private):
    match = re.fullmatch(r"(.+):([1-9][0-9]{0,6})(?:-([1-9][0-9]{0,6}))?", request)
    raw, first, last = request, 1, 90
    if match:
        raw, first = match[1], int(match[2])
        last = int(match[3]) if match[3] else first + 24
    if last < first or last - first >= 100:
        return None
    path = Path(raw)
    if path.is_absolute():
        if (path not in known_private or ".." in path.parts
                or not any(path.is_relative_to(root) for root in roots)):
            return None
        resolved = path.resolve()
        if (not any(resolved.is_relative_to(root) for root in roots)
                or path != resolved or not path.is_file() or is_reparse_point(path)
                or path.suffix.lower() not in PRIVATE_TEXT_TYPES
                or PRIVATE_NAME.search(path.name)):
            return None
        with path.open("rb") as stream:
            data = stream.read(1_048_577)
        name = str(path)
    else:
        name = path.as_posix()
        if (not re.fullmatch(r"[A-Za-z0-9_. /-]+", name)
                or any(part in {".", ".."} for part in raw.replace("\\", "/").split("/"))
                or not source_path_allowed(name)
                or PRIVATE_NAME.search(path.name)):
            return None
        entry = git(source, "ls-tree", commit, "--", name)
        if entry.returncode or not re.match(rb"100(?:644|755) blob ", entry.stdout):
            return None
        blob_size = git(source, "cat-file", "-s", f"{commit}:{name}")
        if blob_size.returncode or int(blob_size.stdout) > 1_048_576:
            return None
        blob = git(source, "show", f"{commit}:{name}")
        if blob.returncode:
            return None
        data = blob.stdout
    if len(data) > 1_048_576 or b"\0" in data:
        return None
    lines = data.decode("utf-8", "replace").splitlines()
    if first > len(lines):
        return None
    return {"path": name, "excerpt": "\n".join(
        f"{i + 1}: {display_line(lines[i])}" for i in range(first - 1, min(last, len(lines)))
    )[:8_000]}


def requested_evidence(source, source_commit, evidence_dirs, requests, *, known_paths=()):
    """Resolve bounded requests against the pinned tree and approved local evidence."""
    result = []
    terms = {}
    known_private = {Path(path) for path in known_paths if Path(path).is_absolute()}
    for request in dict.fromkeys(requests[:6]):
        if request.startswith("file:"):
            try:
                item = requested_file(source, source_commit, evidence_dirs, request[5:], known_private)
            except (OSError, ValueError):
                item = None
            if item:
                result.append(item)
        elif request.startswith("search:"):
            term = request[7:].strip()
            if re.fullmatch(r"[A-Za-z0-9_ .-]{4,80}", term):
                terms[term.lower()] = 10
    if terms:
        result.extend(tracked_evidence(source, source_commit, terms)[:4])
        result.extend(private_evidence(evidence_dirs, terms)[:2])
    return result
