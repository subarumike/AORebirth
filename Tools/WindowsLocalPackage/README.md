# AORebirth for local Windows play

Extract the complete ZIP to a writable folder, then double-click `AORebirth.exe`.
Use a folder outside Program Files and outside a synchronized cloud folder.
Keep the entire extracted folder together.

First-time setup verifies the package and prepares local files. It does not
start MySQL or any server, initialize the database, reserve a URL, or require
free network ports. Opening the launcher also starts no services.

Download the fixed AO client from the launcher, or select an existing copy.
Press **Start server** when ready. This checks the game ports and selects a private
database port before starting anything, initializes the database on first start,
and starts the local services. Once ready, create your local account and press **Play**. Create account
and Play require the server to be running; neither starts it implicitly.
**Stop server** saves and stops the processes owned by this installation.
**Open Logs** shows diagnostic files if startup fails.

Your existing SQL server can remain running. AORebirth starts its own bundled
MySQL with separate data and credentials. Start server prefers its saved private
port (3308 by default); if occupied, it selects a free loopback port automatically
and saves that choice. All AORebirth database connections use that selected port.
It does not reuse an existing SQL database or stop another SQL server.

The local SQL administrator login is `root` / `aorebirth`; AORebirth's internal
runtime login is `aorebirth_local` / `aorebirth`. Use host `127.0.0.1`, database
`cellao_codex_clean`, and the port shown by **SQL login** (also saved in
`data/launcher.xml`). That button shows and copies the connection details after
the first successful Start server. It remains available while stopped, but the
server must be running to connect.

When updating an existing installation, keep its `data` folder and press
**Start server** once before using SQL login. That start applies the documented
passwords to this installation's owned private database while preserving its
accounts and characters. No other SQL server is changed.

Setup requires no free ports. The fixed game/service TCP ports are 80 (local
Daily Rewards PHP), 7012 (chat), 7500 (login), 7501 (zone),
7510 (local account registration), and 7600/7601
(WorldEngine registration/status).
If one is occupied, Start server reports the conflict before launching services
and leaves its owner untouched.

No Git, Visual Studio, .NET SDK, compilation, SQL commands, or terminal commands
are required on the player's machine. Supported target: Windows 11 x64 with
.NET Framework 4.8 enabled. The package includes the .NET 10 runtime, ASP.NET
Core runtime when required, MySQL binaries, the pinned Windows PHP runtime,
and the Microsoft VC++ prerequisite installer. Use **Install prerequisites**
before **Start server**.

Local configuration and protected passwords are prepared under `data` by setup.
The local database is initialized only by Start server. Accounts and characters
are subsequently stored there. Before applying required versioned migrations
to a recognized installation's owned local database, Start server takes and
verifies a current database backup. An incompatible or ambiguous schema stops
startup; it is not reset or repaired automatically. Extract updates into a
new folder, stop the old installation, and preserve its original folder and
backups. Copy only its owned `data` folder into the new extraction, using the
same Windows user and machine. Do not overlay the old `GameData` or `server`
folders: the new archive supplies the exact current runtime inventory. Retain
the old installation until the update is accepted. Keep the entire `data`
folder, including its protected World credentials. After a schema migration,
rolling back the old binaries also requires the retained pre-migration database
backup; the launcher never restores it automatically. Never run two
copies of the local package on the same fixed game/service ports.
Protected local credentials are tied to the current Windows user and machine;
moving its data to another account or PC is unsupported.

The package contains the full AORebirth GameData supplied for this release.
`LICENSE`, `NOTICE`, and bundled upstream notices accompany the software.
Do not redistribute components beyond their stated terms.

This package includes LoginEngine, ChatEngine, WorldEngine, and ZoneEngine_New.
Its supplied gameplay configuration controls seasonal event activation; the
producer can explicitly enable Halloween for this archive.
Start server also starts the bundled PHP process on loopback HTTP port 80 for
the supplied Daily Rewards frontend under `web/daily`. The package owns that
process and stops it with the other local services. PHP and the game use this
installation's protected `data/daily-login/claims` directory. No separate PHP
installation is required. Local PHP readiness and the four-engine startup
check do not verify the Daily browser or a claim in the actual AO client.

The current public source supplies 59 Daily frontend files: six site files and
53 cell images. Its reward configuration also references 30 images missing
from that source: `assets/cells/taken.png`, `assets/claim-reward.png`, and
`assets/buttons/btn-01.png` through `btn-28.png`. Those exact files were also
absent from the inspected current Windows frontend and documented local PHP
roots. No replacement artwork is invented. Daily browser operation remains
**unverified**; these inherited image gaps do not change the four core engines.
Clean-machine installation and live AO client acceptance are **unverified**
until recorded for the exact ZIP SHA-256. Compilation and package construction
alone do not establish playable acceptance.

## Producing a package

This section is for the package producer. Players do not run these commands.

First run the documented governed source/content intake and Windows build.
Reuse that successful build's measured binaries; this producer never publishes
or rebuilds the product engines. Check upstream source/content again through
the existing `prepare-build` operation before producing the archive. Its
prepared receipt must match the successful build's exact source, content-dump
SHA and complete runtime-admission manifest. Changed inputs require their own
Windows build. Keep private GameData and official dependency downloads outside
tracked source. The packaging branch may contain only standalone tooling,
focused tests and documentation beyond the measured public SHA. Local
connection XML is never copied; templates come from the exact public commit.
The script refuses runtime/build source changes against `PublicMasterSha` and
writes only to ignored `build-verify/windows-local-package` output directories.
It never commits, pushes, starts engines, starts the client, or connects to a
database.

Run from the repository root:

```cmd
cmd /d /c Tools\WindowsLocalPackage\build-package.cmd ^
  -PublicMasterSha <exact-public-master-sha> ^
  -BuildReceipt "<own-governed-build-attempt.json>" ^
  -ContentIntakeReceipt "<fresh-own-prepared.json>" ^
  -GameDataRoot "<canonical-windows-gamedata-folder>" ^
  -GameplayConfig "<selected-gameplay.json>" ^
  -EnableHalloween ^
  -RuntimeArchive "<official-dotnet-win-x64.zip>" ^
  -PhpArchive "<exact-pinned-official-php-win-x64.zip>" ^
  -AspNetRuntimeArchive "<official-aspnetcore-win-x64.zip>" ^
  -MySqlArchive "<official-mysql-winx64.zip>" ^
  -VcRedistributable "<official-vc_redist.x64.exe>" ^
  -DependencyMetadata "<release-dependencies.json>" ^
  -ClientManifest "<approved-client.xml>"
```

`build-package.cmd` selects Windows PowerShell and its own standard modules.
It verifies all eight engine executable/assembly hashes in the build receipt,
copies each existing `.deps.json` runtime dependency closure into its isolated
engine directory, and reuses the current build's database migration/preflight
tools. Only the standalone setup helper, launcher and local account broker
are compiled. The SDK setup-helper build uses the existing development-only
`run-feature-validation` operation; the maintained Visual Studio MSBuild
mechanism builds the .NET Framework tools. No product-engine publish occurs.
All build failures stop construction. No fixture,
unit, regression, or live-client tests run automatically.
Before compression, the freshly staged launcher runs its read-only
`--verify-package` mode against the complete inventory and configuration.
A nonzero exit stops packaging before a ZIP is created. This verification
does not initialize a database, start a server, or launch the client.

The producer requires the documented Windows build prerequisites: Visual
Studio MSBuild, .NET 10 SDK, .NET Framework targeting packs, and restored
packages.config dependencies. These are not included in the player download.

The dependency metadata is a local JSON file. Record values from the official
Microsoft .NET release metadata and MySQL download page, not guessed URLs or
unverified mirrors:

```json
{
  "runtime": {
    "version": "<version>",
    "sourceUrl": "<official-https-runtime-archive-url>",
    "sha512": "<official-sha512>"
  },
  "aspnetRuntime": {
    "version": "<version>",
    "sourceUrl": "<official-https-aspnet-runtime-archive-url>",
    "sha512": "<official-sha512>"
  },
  "mysql": {
    "version": "<version>",
    "sourceUrl": "<official-https-mysql-archive-url>",
    "checksumAlgorithm": "<official-checksum-algorithm>",
    "checksum": "<official-checksum>"
  },
  "vcRedist": {
    "version": "<version>",
    "sourceUrl": "<official-https-microsoft-download-url>",
    "checksumAlgorithm": "SHA256",
    "checksum": "<verified-sha256>"
  }
}
```

MySQL's published archive checksum is verified as supplied, and a separate
SHA-256 is recorded for the package inventory. The VC++ executable additionally
requires a valid Microsoft Authenticode signature. Downloaded archives are
checked before extraction, and archive paths cannot escape staging.

Obtain `PhpArchive` independently from the official URL in the exact public
source's [PHP runtime supply pin](../../docs/project/PHP_RUNTIME_SUPPLY.md).
Keep it outside Git and retain its official filename. The producer derives
PHP's URL, version, architecture, size, and SHA-256 from that public commit's
`AORebirth/Config/PhpRuntime.manifest.xml`; no duplicate `php` entry is needed
in the local dependency JSON. The complete pinned runtime, its license and
redistribution notices go under `php/`. The exact public manifest is copied
to `config/PhpRuntime.manifest.xml`, and the finished inventory must match all
official PHP files. The producer never downloads or imports PHP into the
developer's WebEngine runtime.

The approved client manifest is supplied separately as `config/client.xml`:

```xml
<Client downloadUrl="https://approved-fixed-client-download"
        executable="relative-client-executable.exe"
        executableSha256="verified-executable-sha256"
        archiveLength="verified-download-length-in-bytes"
        archiveSha256="verified-complete-archive-sha256-when-available"
        arguments="approved local connection arguments" />
```

The URL must use the approved `ao-rebirth.com` HTTPS host. The executable hash
and archive length are required. Include `archiveSha256` only when the full
download's digest has been verified; omit that attribute otherwise. An optional
`etag` pins the website's response metadata in addition to the download length.
`executable` is the approved executable's leaf filename or its relative path
inside the selected client folder. For an existing installation, select that
exact folder; the launcher verifies the executable digest before saving it.

The package has one shared `GameData` folder selected with the existing
`AO_REBIRTH_GAMEDATA_PATH` setting. Engine dependency folders remain isolated
because Login/Chat and Zone compile different shared-library graphs. Local
configuration uses `AO_REBIRTH_CONFIG_PATH` and the existing MySQL environment
override. Connection templates come from the exact public commit; the explicit
`GameplayConfig` input supplies the selected gameplay settings. `-EnableHalloween`
sets only `events.halloween.enabled` to true; omit it to preserve the input's
event setting. Both input and packaged configuration digests are recorded.
Runtime `Content`, mission reward catalogs, navigation
configuration, schema tools, and upstream licenses accompany the binaries.
The 59 tracked Daily frontend files are exported byte-for-byte from the same
public Git commit into `web/daily`, with their Git blob identities and SHA-256
digests in provenance. The local router and restrictive PHP INI come from the
maintained packaging source. The producer copies no working website tree,
documentation, cache or research. Missing referenced images are measured from
the supplied public rewards configuration and recorded separately.

Only files listed in the build's governed runtime-admission manifest are copied
into GameData. Each path, size and digest is checked during staging. The exact
manifest accompanies the archive as `runtime-content-manifest.json`; the
launcher verifies that it matches the complete packaged GameData inventory.
Excluded content, research and evidence cannot enter through a broad GameData
directory copy.

Each package records its authoritative public-master SHA, content-dump SHA,
Windows build-receipt digest, complete GameData manifest digest, package-tooling HEAD,
dirty tooling state and source hashes, dependency URLs/checksums, complete
payload inventory, and ZIP SHA-256. Existing archives remain untouched; each
run creates a separate ignored output directory. Lossless fast compression
produces the complete standalone ZIP. `package-run.json` beside the ZIP records
measured phase timings and verified archive/binary reuse decisions.
`package-manifest.json` hashes every payload
file except itself; `inventory.csv` and `provenance.json` are included. Private
data, credentials, database files, captures, and build outputs must never be
added to Git. Publish the ZIP only through Mike's authorized distribution
channel.

## Acceptance still required

For the producer's packaged startup/shutdown check, use a fresh extraction
without an initialized `data` directory:

```cmd
cmd /d /c Tools\WindowsLocalPackage\verify-startup.cmd -PackageRoot "<fresh-extracted-AORebirth-folder>"
```

This waiter launches the packaged `--verify-startup` mode, waits on its actual
Windows process handle, and returns its real exit code. Do not use CMD
`start /wait` as evidence of a WinExe's verification result. The check starts
the installation's local database, four backend engines, and loopback PHP,
verifies their readiness, and stops them; it never starts the AO client. It allows 20 minutes
for the existing 15-minute startup budget and bounded shutdown. Logs and
`result.json` are retained outside the extraction under ignored
`build-verify/windows-local-package-startup`.

A nonzero verifier result remains a failure. At the full 20-minute deadline,
the waiter stops only its own launcher process, then invokes the existing
`--stop-owned` controller to recover and stop positively identified services.
That emergency cleanup has a separate five-minute bound. The final result is
timeout exit code 124 even if cleanup succeeds; failed cleanup is reported
explicitly. No process is stopped by name or port alone.

On a clean Windows machine without developer tools, verify extraction to a
path containing spaces, first-run setup with no services or listeners,
explicit Start server with an existing SQL server left running, occupied game-port
handling, prerequisite handling, account
creation, client download/selection, server readiness, Mike's live login and
play check, clean shutdown, restart with the same character, and clear errors
for occupied ports or missing content. Record failures and the exact package
hash. User data must survive replacing binaries. Database upgrades require
their own backup and migration acceptance before being advertised.
