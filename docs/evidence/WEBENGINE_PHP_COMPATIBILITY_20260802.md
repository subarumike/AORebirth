# WebEngine PHP 8.5.9 / WebCore Compatibility Evidence — 2026-08-02

## Scope and starting state

This reconciliation began from synchronized commit
`2d243f2b7082e2b158ddfe5f475c73f0d491d422`. It changes only the optional
WebEngine PHP/WebCore boundary, its deterministic supply tooling, acceptance
integration, and documentation. It does not change supported ZoneEngine or
gameplay behavior, database schema, or persisted data.

The historical bootstrap selected PHP 5.5.10 through mutable runtime download
logic and served a legacy WebCore tree without a maintained-PHP compatibility
authority. That path was already removed by the prior offline WebCore bootstrap
reconciliation. This work closes the remaining maintained runtime and complete
corpus compatibility boundary.

## Official PHP authority

PHP's supported-versions policy and Windows download guidance were reviewed on
2026-08-02. PHP 8.2 was the oldest maintained branch and PHP 8.5 the newest.
The newest maintained branch was selected because the task is to prove the
legacy content against a current runtime, not perpetuate an obsolete bootstrap.

| Field | Exact authority |
| --- | --- |
| Publisher | The PHP Group, official PHP for Windows archive |
| Version/build | PHP 8.5.9, x64, NTS, VS17 |
| Archive | `php-8.5.9-nts-Win32-vs17-x64.zip` |
| Official URL | `https://downloads.php.net/~windows/releases/archives/php-8.5.9-nts-Win32-vs17-x64.zip` |
| Archive bytes | `36,015,210` |
| Archive SHA-256 | `516c2d72231bd035c8a910120834add0ad208098b790b4909b2cbeb93ce135fc` |
| Extracted inventory | 78 files, 6 explicit directories, 101,963,340 file bytes |
| PHP manifest SHA-256 | `dc962aa41501a23d993cf667c546593ef36b122f8002d8ab3fc56d1a888cd735` |
| Approved INI SHA-256 | `912685982dccbc19887cc0062535fd1c5e56f23dc61f18f9a365e83b8c4214d7` |

The ZIP is operator-supplied and retained outside the repository. The complete
runtime tree is imported under ignored build output. Neither the ZIP nor any
PHP binary is tracked in Git. The importer pins the exact manifest bytes and
archive identity, validates every ZIP entry before extraction, rejects unsafe
paths/reparse points/duplicate names/unexpected inventory, verifies file sizes
and hashes while extracting, validates x64 PE headers without execution, and
activates only a fully validated staging tree under an exclusive lease.

The required runtime modules are `PDO`, `pdo_mysql`, `dom`, `session`, `hash`,
`json`, `filter`, and `ctype`. DOM and the core modules are built into this
official runtime; only `php_pdo_mysql.dll` is separately enabled. `mysqli` and
`mcrypt` are not required after the deterministic repairs.

The approved INI disables user/additional INI scanning, displayed startup and
runtime errors, URL fetching/includes, uploads, dynamic loading, and process or
shell execution. It confines filesystem access, logs, temporary files, and
sessions to host-controlled roots; uses strict cookie-only HttpOnly SameSite
sessions; pins UTC; and keeps the legacy response charset ISO-8859-1 aligned
with the repository's latin1 database contract. Secure-only cookies remain off
because the listener is plaintext HTTP.

## Complete WebCore corpus audit

The authority is CellAO WebCore commit
`765c3850767b63af1cd259bab7f2f7ca3e97adf9`.

| Field | Exact authority |
| --- | --- |
| Archive SHA-256 | `ef297e623040b375e64c543568ca94e44ed7cc59de6fe826ed5e42db95c020ab` |
| Base manifest SHA-256 | `85c1515d274c2e4051013e89ca6d2a355365d5d01df7d621cc060dfa84e38463` |
| Base corpus | 7,140 files, 26,648,501 bytes |
| PHP scope | all 25 PHP files |
| Compatibility manifest SHA-256 | `4bd7c613e1f232419737c0f14fcff94cdca3a9f2fa136e0fda9a0a05790ca31a` |
| Final manifest SHA-256 | `f07f6b2ce58fa025e93baa49241dbe71a8d7482a10dfd437b2e1d50c418c45c8` |
| Final corpus | 7,140 files, 26,649,619 bytes |
| Compatibility tool SHA-256 | `e0d68a8d1c5c6577fe5582f0ecfae5f45cdf6394a69a0c638976d4f16f512da7` |

The deterministic scanner covered the entire PHP inventory. Base findings were
9 `mysql_*` calls, 1 `mcrypt_*` call, 1 `get_magic_quotes_gpc` call, 1 short
opening tag, and 4 string `sizeof` calls. PHP 8.5.9 lint additionally proved one
unmatched closing parenthesis. Counts were zero for `create_function`, curly
brace offsets, dynamic properties, `each`, `ereg`, `eval`, file uploads,
`mysqli_*`, `register_globals`, `safe_mode`, `session_register`, shell
execution, `split`, and user-controlled `unserialize`.

Exactly seven source paths are transformed, each with pinned input/output hash:

1. `engine.php`: replace legacy MySQL operations with PDO/prepared execution
   and `mcrypt_create_iv` with `random_bytes`, preserving stored format.
2. `process-login.php`: use the shared PDO prepared login query, remove obsolete
   magic-quotes cleaning, and regenerate the session identifier after verified
   authentication.
3. `includes/config.php`: require four host-supplied DB environment values,
   preserve latin1 through an explicit PDO DSN, use exception mode, and make
   authorization redirects terminate immediately.
4. `register.php`: replace four string `sizeof` calls with `strlen`.
5. `notfound.php`: replace the short opening tag and HTML-encode every
   server-derived output.
6. `includes/header.php`: HTML-encode error, message, and session-name output.
7. `includes/data/playfields.php`: remove only the unmatched closing
   parenthesis proven by PHP 8.5.9 lint.

Generated output is never edited by hand. The compatibility generator verifies
the exact base manifest and patch-tool hash, applies the seven transforms to a
staging tree, rejects all residual banned compatibility tokens, verifies the
complete final manifest, and activates it under the WebCore import lease. A
future base or tool change fails closed until all checked-in authorities are
deliberately regenerated together.

## Security and runtime boundary

Only `about.php`, `index.php`, `notfound.php`, and `support.php` are reachable as
PHP. Administrative/include paths plus authentication, registration, logout,
member, login-processing, engine, and all other PHP routes are denied. Static
serving is restricted to the approved CSS/image/JavaScript extensions. Request
normalization decodes once and reapplies the same allow/deny rules, rejecting
traversal, malformed or multiply encoded escapes, alternate data streams,
unsupported methods/extensions, and ambiguous paths without case-sensitive
bypasses.

PHP executes only through the validated absolute `php-cgi.exe`, exact approved
INI, script-directory working directory, empty stdin at the GET-only CGI
boundary, bounded stdout/stderr, complete CGI variables, fixed timeout, strict
response parsing, and a cleared minimal child environment. POST is rejected;
HEAD is normalized to GET for correct headers and its response body is
suppressed. Unrelated parent secrets are not inherited. PHP and
WebCore mutable state are outside the served root. CGI failures are contained
to the request and return a fixed 500 without terminating WebEngine or exposing
diagnostics. Socket sends complete the declared response or fail; partial sends
cannot silently truncate a response.

Direct process startup validates the non-secret structure of the configured DB
connection string without connecting. The approved start wrapper performs the
authorized DB preflight first, then validates the binary, PHP manifest/runtime,
WebCore base/compatibility/final manifests, process/port ownership, and only
then launches. Inside the process, PHP and WebCore leases are acquired in that
order, both complete trees are revalidated, and only then can the listener be
created. Constructor or prerequisite failure propagates and leaves no listener.

## Evidence limits and classification

No valid WebCore database credential was available. This work made no live
database connection, changed no credential or schema, started no AO client or
capture tool, and started no WebEngine or game engine. Database query parity,
live non-ASCII round trips, and end-to-end authenticated sessions remain
blocked pending an authorized disposable credential and controlled live test.

The pinned upstream WebCore snapshot contains no discovered license file.
Integrity validation does not grant redistribution rights. The plaintext HTTP
listener also prevents production-safe secure-cookie transport. WebEngine is
therefore classified as development-only: ready for authorized credential-
backed verification, not production deployment or redistribution.

## Validation record

Final focused totals, complete gate results, final commit SHA, clean-tree proof,
and engine/port state are recorded after the unchanged clean commit is tested.
