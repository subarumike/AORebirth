# WebCore Asset Supply

## Decision

WebEngine uses an operator-supplied local ZIP plus an AORebirth-authored,
deterministic compatibility overlay. Neither WebEngine nor any repository
wrapper downloads, updates, or selects WebCore content at runtime. A missing,
modified, differently pinned, or unpatched asset tree is a startup failure, not
a trigger for network access.

## Authoritative pin

| Property | Authority |
| --- | --- |
| Upstream repository | `https://github.com/CellAO/CellAO-WebCore` |
| Exact upstream commit | `765c3850767b63af1cd259bab7f2f7ca3e97adf9` |
| Expected archive root | `CellAO-WebCore-765c3850767b63af1cd259bab7f2f7ca3e97adf9` |
| Archive SHA-256 | `ef297e623040b375e64c543568ca94e44ed7cc59de6fe826ed5e42db95c020ab` |
| Extracted inventory | 7,140 files; 26,648,501 bytes |
| Canonical manifest SHA-256 | `85c1515d274c2e4051013e89ca6d2a355365d5d01df7d621cc060dfa84e38463` |
| Checked-in manifest source | `AORebirth/Config/WebCoreAssets.manifest.xml` |
| Compatibility manifest | `AORebirth/Config/WebCoreCompatibility.manifest.xml` |
| Final installed-tree manifest | `AORebirth/Config/WebCorePatchedAssets.manifest.xml` |
| Compatibility tool | `Tools/webcore_php_compatibility.py` |
| Runtime content root | configured `WebHostRoot`, currently `htdocs`, beside `WebEngine.exe` |

The base manifest binds the untouched upstream identity and file inventory.
The compatibility manifest binds each exact input hash, operation ID, output
hash, and the full final manifest hash. The final manifest binds all 7,140
installed files. The archive and both base/patched runtime trees are local
supply artifacts and never enter Git.

## Why this snapshot

No AORebirth-specific WebCore assets and no pre-existing local WebCore asset
tree were found. The nearest supported compatibility evidence is chronological
and contractual:

- AORebirth commit `73492f145` dated 2014-03-30 changed login password storage
  to PBKDF2-HMAC-SHA1 with 1,111 iterations, a 30-byte salt, and a 30-byte hash.
- CellAO WebCore commit
  `765c3850767b63af1cd259bab7f2f7ca3e97adf9` dated 2014-04-01 uses the matching
  login contract.

That match is the basis for pinning this snapshot instead of a current branch
head. It is narrow compatibility evidence only; it does not establish modern
PHP compatibility, security fitness, licensing, or production readiness.

## Historical boundary

At AORebirth commit `73492f145`, configuration named a mutable GitHub
`master.zip` URL. `Checks.CheckWebCore` downloaded it as `WebCore.zip`, extracted
it into `htdocs`, and was exposed through the manual `checkWebCore` console
command. WebEngine then served those files and passed PHP requests to its PHP
runtime.

The current contract removes the URL configuration and all download behavior.
There is no replacement mutable archive source. Serving and PHP execution remain
separate runtime behavior after local assets have passed validation.

## Offline import

Build WebEngine through the approved wrapper before importing:

```cmd
cmd /d /c tools\build_aorebirth_debug.cmd
```

WebEngine must be fully stopped before import:

```cmd
cmd /d /c stop-web-engine.cmd
```

The import wrapper performs the exact WebEngine stopped-state preflight before
invoking the importer. The importer also holds an exclusive runtime/import
lease across validation, staging, and activation.

Place the exact ZIP at a local path outside the repository, then run the import
from a CMD rooted at the repository:

```cmd
cmd /d /c import-webcore-assets.cmd "C:\local-only\CellAO-WebCore-765c3850767b63af1cd259bab7f2f7ca3e97adf9.zip" 765c3850767b63af1cd259bab7f2f7ca3e97adf9
```

The command accepts only a local ZIP plus the explicit exact version. It rejects
a version other than the maintained pin, an archive whose SHA-256 or root does
not match, and any result that does not reproduce the base manifest. While
retaining the exclusive import lease, it runs the hash-pinned compatibility tool
against staging, validates every exact patch input/output, validates the
complete final manifest, and only then activates the patched tree. It performs
no acquisition and has no URL fallback.

The importer rejects URI, UNC, device, reparse-point, and archive-inside-live-
root inputs. The configured live root is confined below the WebEngine directory.
Its XXE-disabled strict manifest parser rejects unsafe or non-normalized paths,
duplicates, case collisions, file/directory collisions, and invalid sizes or
hashes. ZIP validation additionally rejects traversal, mixed slashes, encoded
paths, links/special entries, missing or unexpected content, corrupt input, and
inventory expansion beyond the fixed file-count, per-file, total-size, and
archive-size bounds. Extraction occurs in a unique sibling staging directory;
only a completely validated tree is activated, and an activation failure rolls
the previous valid tree back into place. If cleanup of the prior tree fails only
after activation, import reports non-success, retains the recoverable backup for
manual review, and leaves the fully validated new tree active.

Import is an explicit provisioning action. Normal build, validation, and
startup do not refresh the asset tree.

## Validation

Validate an installed tree directly with:

```cmd
cmd /d /c validate-webcore-assets.cmd
cmd /d /c validate-webcore-php.cmd
```

Run the deterministic asset validator self-test with:

```cmd
cmd /d /c Tools\run_web_engine_security_tests.cmd
```

`/validate-webcore-assets` checks the complete patched tree against the
base/compatibility/final authority chain. `validate-webcore-php.cmd` first
revalidates that tree and the exact PHP runtime, then lints all 25 PHP files.
`/self-test-webcore-assets` exercises the deterministic import contract without
requiring production assets, PHP, a database, or network access.

`start-web-engine.cmd` repeats the installed-tree validation. Its fail-closed
order is:

1. Read-only database preflight.
2. Required `WebEngine.exe` binary.
3. PHP manifest validation.
4. Complete PHP runtime, module, INI, and real-CGI validation.
5. WebCore base-manifest validation.
6. Compatibility and final-manifest validation.
7. Complete patched installed-tree validation.
8. Exact process/port stopped-state preflight.
9. Launch and launched-PID ownership verification.

Failure at any prelaunch step launches no new WebEngine. An already-running
verified process, or a conflicting process owned outside this workflow, is not
terminated by the failed start attempt.

## Security and support boundary

- Do not add an upstream URL, branch name, tag, or automatic updater back to
  configuration or startup.
- Do not treat a successful integrity check as permission to redistribute the
  files. No upstream license file was found, so licensing remains unresolved.
- PHP 8.5.9 executes the approved CGI probe and lints all 25 minimally patched
  PHP files. This proves syntax, runtime identity, modules, and configuration;
  it does not prove database-backed semantics.
- Direct admin, internal, authentication, registration, logout, member, and
  mutation endpoints are disabled at the HTTP route boundary. Public routes are
  limited to `about.php`, `index.php`, `notfound.php`, and `support.php`, plus
  allowlisted static extensions.
- WebEngine remains optional, development-only, and not production-safe because
  no live database compatibility test was authorized, transport is plaintext,
  and upstream licensing is unresolved.
- A future WebCore revision requires a deliberate compatibility, security, and
  license review; a new exact commit and archive hash; a regenerated checked-in
  manifest; and deterministic test updates. Never advance the pin implicitly.
- Directory activation uses same-volume sibling renames and exception rollback.
  Windows does not provide a crash-atomic directory exchange through this
  runtime; power or process loss between the two renames can leave the prior
  validated tree under its `htdocs.backup-*` name. Startup then fails closed.
  An operator must restore that retained tree or repeat the reviewed import
  while WebEngine remains stopped.

## Evidence

See `docs/evidence/WEBCORE_BOOTSTRAP_SECURITY_20260802.md` for the original
offline-bootstrap reconciliation and
`docs/evidence/WEBENGINE_PHP_COMPATIBILITY_20260802.md` for the maintained PHP,
compatibility overlay, lint, and runtime boundary.
