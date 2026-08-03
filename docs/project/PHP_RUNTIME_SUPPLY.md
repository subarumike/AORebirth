# WebEngine PHP Runtime Supply

## Decision

WebEngine uses one exact, operator-supplied official PHP for Windows archive.
Build, startup, and validation never download PHP and never select a moving
`latest` artifact. PHP binaries, ZIP files, and the installed runtime remain
outside Git.

As of 2026-08-02, PHP 8.2 is the oldest maintained branch and PHP 8.5 is the
newest. AORebirth selects PHP 8.5.9 because the complete pinned WebCore tree can
be minimally repaired and linted on that branch. PHP's official support table
is the branch-lifecycle authority. PHP's Windows guidance recommends x64 and a
Non-Thread-Safe build for CGI/FastCGI-style execution and requires the matching
Visual C++ runtime.

## Authoritative pin

| Property | Authority |
| --- | --- |
| Version | `8.5.9` |
| Official artifact | `php-8.5.9-nts-Win32-vs17-x64.zip` |
| Official URL | `https://downloads.php.net/~windows/releases/archives/php-8.5.9-nts-Win32-vs17-x64.zip` |
| Archive size | `36,015,210` bytes |
| Archive SHA-256 | `516c2d72231bd035c8a910120834add0ad208098b790b4909b2cbeb93ce135fc` |
| Build identity | Windows x64, NTS, VS17 |
| Inventory | 78 files, 6 explicit ZIP directories, 101,963,340 uncompressed file bytes |
| Manifest | `AORebirth/Config/PhpRuntime.manifest.xml` |
| Manifest SHA-256 | `dc962aa41501a23d993cf667c546593ef36b122f8002d8ab3fc56d1a888cd735` |
| Approved INI | `AORebirth/Config/WebEngine.php.ini` |
| Approved INI SHA-256 | `912685982dccbc19887cc0062535fd1c5e56f23dc61f18f9a365e83b8c4214d7` |
| Runtime target | `AORebirth/Built/Debug/php` |

The complete manifest records every official archive file and explicit
directory with size and SHA-256. It separately binds the AORebirth-authored
`php.ini`. The installed tree must equal the official inventory plus exactly
one `php.ini`; unexpected files, missing files, hash drift, case collisions,
links, reparse points, non-x64 PE images, and network/URI paths fail closed.

## Required capabilities and configuration

The audited WebCore PHP inventory requires `PDO`, `pdo_mysql`, `dom`, `session`,
`hash`, `json`, `filter`, and `ctype`. The official build provides DOM and the
core modules internally; `php_pdo_mysql.dll` is the only separately enabled
extension. Neither `mysqli` nor `mcrypt` is required.

The approved INI disables displayed errors, URL includes/fetching, uploads,
user INI files, and process/shell functions; constrains `open_basedir`; limits
execution, input, memory, and output; disables supplemental INI scanning; and
places logs, temporary files, and sessions under the host-controlled
`WebEngineData` directory.
Sessions use strict mode, cookies only, HttpOnly, and SameSite=Lax. Secure-only
cookies remain off because the historical WebEngine listener is plaintext;
that transport limitation is one reason WebEngine remains development-only.
PHP 8.5's maintained session-ID generator and entropy defaults are retained;
the configuration does not reduce them. Uploads are disabled even though
bounded upload/POST limits remain explicit.

The importer never broadens Windows ACLs. Operators must keep the manifest,
approved INI, compatibility tool, and installed runtime writable only by the
deployment administrator; only the separate `WebEngineData` log/tmp/session
directories are intended to be writable by the WebEngine service identity.

`cgi.force_redirect=On` is paired with a host contract that always supplies
`REDIRECT_STATUS=200`. Runtime validation executes a real CGI probe to prove
that pair before startup. HTTP output remains ISO-8859-1 to match the imported
legacy WebCore corpus; the WebCore PDO DSN explicitly
uses `charset=latin1` to preserve the repository database contract.

## Offline import

Install the x64 Visual C++ 2015-2022 Redistributable if the host does not
already provide it. Obtain the exact official ZIP independently, keep it
outside the repository, stop WebEngine, build, and import:

```cmd
cmd /d /c stop-web-engine.cmd
cmd /d /c tools\build_aorebirth_debug.cmd
cmd /d /c import-php-runtime.cmd "C:\local-only\php-8.5.9-nts-Win32-vs17-x64.zip" 8.5.9
cmd /d /c validate-php-runtime.cmd
```

The importer validates the checked-in manifest and INI, exact requested
version, local archive name/size/hash, safe ZIP inventory, x64 PE identity, and
complete installed tree. It holds `PhpRuntime.runtime.lock` with Windows
share-deny semantics across staging, activation, rollback, and final
validation. A failed import preserves the prior runtime when rollback is
possible. Ordinary startup never imports or edits PHP.

The C# process independently revalidates the manifest, INI hash and required
directives, complete installed inventory, PE architecture, exact PHP/NTS/SAPI
identity, loaded INI and empty additional-scan result, required modules, and
the real CGI redirect-status contract. It retains the same runtime lease for
the process lifetime and repeats validation immediately before opening the
listener.

## Update policy

A PHP update is a reviewed dependency change. Re-audit the complete pinned
WebCore inventory, select an official supported artifact, replace the full
runtime manifest, re-evaluate the INI and required modules, execute all fixture
tests and real-runtime lint, update evidence, and pass the complete mandatory
gate twice from one unchanged clean commit. Never advance the pin implicitly.

## Support boundary

The exact runtime, CGI contract, extensions, INI, compatibility overlay, and
all 25 PHP files are locally validated without a database. No valid MySQL
credential was available, so database-backed login/data semantics and persisted
data compatibility are not proven. WebEngine is development-only and is not
production-safe.

Official references:

- `https://www.php.net/supported-versions.php`
- `https://www.php.net/downloads.php?os=windows&osvariant=windows-downloads&version=8.5`
- `https://www.php.net/manual/en/install.windows.manual.php`
