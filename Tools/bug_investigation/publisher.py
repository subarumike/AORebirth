"""Publish an exact validated commit as a GitHub draft."""

import json
import os
from pathlib import Path
import re
import subprocess
import tempfile


class PublishError(RuntimeError):
    pass


def call(root, executable, *args):
    env = {key: value for key, value in os.environ.items() if not key.upper().startswith("GIT_")}
    env.update(GIT_TERMINAL_PROMPT="0", GH_PROMPT_DISABLED="1")
    try:
        return subprocess.run(
            [executable, *args], cwd=root, env=env, stdin=subprocess.DEVNULL,
            capture_output=True, text=True, encoding="utf-8", timeout=90, check=False,
        )
    except (OSError, subprocess.SubprocessError):
        raise PublishError(f"{executable} could not complete publication.") from None


def git(root, *args):
    result = call(root, "git", "-c", f"core.hooksPath={os.devnull}", *args)
    if result.returncode:
        raise PublishError(f"Git {args[0]} failed during publication.")
    return result.stdout.strip()


def existing_draft(root, repository, base, branch, sha):
    owner = repository.split("/")[0]
    response = call(
        root, "gh", "api", "--hostname", "github.com", "--method", "GET",
        f"repos/{repository}/pulls", "-f", "state=all", "-f", f"head={owner}:{branch}",
        "-f", f"base={base}",
    )
    if response.returncode:
        raise PublishError("Could not verify existing GitHub pull requests.")
    try:
        pulls = json.loads(response.stdout)
        if not pulls:
            return None
        if len(pulls) == 1:
            pull = pulls[0]
            if (pull["state"] == "open" and pull["draft"] is True
                    and pull["head"]["sha"] == sha and pull["head"]["ref"] == branch
                    and pull["head"]["repo"]["full_name"].lower() == repository.lower()
                    and pull["base"]["ref"] == base
                    and pull["base"]["repo"]["full_name"].lower() == repository.lower()
                    and pull["html_url"].lower().startswith(f"https://github.com/{repository.lower()}/pull/")):
                return pull["html_url"]
    except (ValueError, TypeError, KeyError):
        raise PublishError("Unexpected GitHub pull request response.") from None
    raise PublishError("A conflicting pull request already exists for the repair branch.")


def publish(worktree: Path, repository: str, base: str, branch: str, expected_sha: str,
            title: str, body: str) -> str:
    if (not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository)
            or not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]*", base)
            or not re.fullmatch(r"codex/bugfix-[1-9][0-9]*", branch)
            or not re.fullmatch(r"[0-9a-f]{40}", expected_sha)):
        raise PublishError("Invalid GitHub publication target.")
    expected = {
        f"https://github.com/{repository.lower()}.git", f"https://github.com/{repository.lower()}",
        f"git@github.com:{repository.lower()}.git", f"git@github.com:{repository.lower()}",
    }
    for flags in (("--all",), ("--push", "--all")):
        urls = git(worktree, "remote", "get-url", *flags, "origin").splitlines()
        if len(urls) != 1 or urls[0].lower() not in expected:
            raise PublishError("Origin differs from the approved GitHub repository.")
    if git(worktree, "rev-parse", "HEAD") != expected_sha:
        raise PublishError("The worktree does not point at the validated commit.")
    ref = f"refs/heads/{branch}"
    head = git(worktree, "ls-remote", "--refs", "origin", ref)
    if head and head != f"{expected_sha}\t{ref}":
        raise PublishError("The remote repair branch contains another commit.")
    found = existing_draft(worktree, repository, base, branch, expected_sha)
    if found:
        if not head:
            raise PublishError("An existing draft has no matching remote branch.")
        return found
    if not head:
        # An empty lease permits creation only. No pre-existing ref can be replaced.
        git(worktree, "push", "--no-follow-tags", "--recurse-submodules=no",
            f"--force-with-lease={ref}:", "origin", f"{expected_sha}:{ref}")
    if git(worktree, "ls-remote", "--refs", "origin", ref) != f"{expected_sha}\t{ref}":
        raise PublishError("The remote branch does not match the validated commit.")
    with tempfile.TemporaryDirectory(prefix="aor-draft-pr-") as directory:
        body_file = Path(directory) / "body.md"
        body_file.write_text(body, encoding="utf-8")
        try:
            call(worktree, "gh", "pr", "create", "--repo", repository, "--base", base,
                 "--head", branch, "--draft", "--title", title, "--body-file", str(body_file))
        except PublishError:
            pass  # The remote may have accepted an interrupted request.
    found = existing_draft(worktree, repository, base, branch, expected_sha)
    if found is None:
        raise PublishError("The validated branch is pushed, but no draft PR was verified.")
    return found
