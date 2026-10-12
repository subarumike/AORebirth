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

Setup requires no free ports. The fixed game/service TCP ports remain 6996 and
7012 (chat), 7500 (login), 7501 (zone), and 7510 (local account registration).
If one is occupied, Start server reports the conflict before launching services
and leaves its owner untouched.

No Git, Visual Studio, .NET SDK, compilation, SQL commands, or terminal commands
are required on the player's machine. Supported target: Windows 11 x64 with
.NET Framework 4.8 enabled. The package includes the .NET 10 runtime, ASP.NET
Core runtime when required, MySQL binaries, and the Microsoft VC++ prerequisite
installer. Use **Install prerequisites** before **Start server**.

Local configuration and protected passwords are prepared under `data` by setup.
The local database is initialized only by Start server. Accounts and characters
are subsequently stored there. Do not delete `data` when replacing a package. This first
package does not automate schema upgrades of existing databases; retain the existing
installation until an upgrade has been separately accepted. Never run two
copies of the local package on the same fixed game/service ports.
Protected local credentials are tied to the current Windows user and machine;
this first release does not support moving its data to another account or PC.

The package contains the full AORebirth GameData supplied for this release.
`LICENSE`, `NOTICE`, and bundled upstream notices accompany the software.
Do not redistribute components beyond their stated terms.

Local checks passed with port 3308 occupied by a test application: AORebirth chose
another port, prepared its database, and stopped/restarted while that application
kept running. The chosen port was retained after reopening the launcher.
Clean-machine installation and live AO client acceptance are **unverified**
until recorded for the exact ZIP SHA-256. Compilation and package construction
alone do not establish playable acceptance.

## Producing a package

This section is for the package producer. Players do not run these commands.

Use a controlled Windows worktree of the exact public-master source. Keep the
private GameData and official dependency downloads outside tracked source.
The script refuses runtime/build source changes against `PublicMasterSha` and
writes only to ignored `build-verify/windows-local-package` output directories.
It never commits, pushes, starts engines, starts the client, or connects to a
database.

Run from the repository root:

```powershell
Tools\WindowsLocalPackage\build-package.ps1 `
  -PublicMasterSha <exact-public-master-sha> `
  -GameDataRoot <full-authorized-gamedata-folder> `
  -RuntimeArchive <official-dotnet-win-x64.zip> `
  -AspNetRuntimeArchive <official-aspnetcore-win-x64.zip> `
  -MySqlArchive <official-mysql-winx64.zip> `
  -VcRedistributable <official-vc_redist.x64.exe> `
  -DependencyMetadata <release-dependencies.json> `
  -ClientManifest <approved-client.xml>
```

`build-package.cmd` provides the same parameters through Windows PowerShell.
The default first invokes the established
`cmd /d /c tools\build_aorebirth_debug.cmd` workflow. `-SkipCoreBuild` is for a
producer who has already completed that build on the same source; package
publishing still compiles the existing project graph into isolated output
folders. All build/publish failures stop package construction. No fixture,
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
override. Templates come from the exact public commit, never a developer's
live configuration. Runtime `Content`, mission reward catalogs, navigation
configuration, schema tools, and upstream licenses accompany the binaries.

Each package records its authoritative public-master SHA, package-tooling HEAD,
dirty tooling state and source hashes, dependency URLs/checksums, complete
payload inventory, and ZIP SHA-256. `package-manifest.json` hashes every payload
file except itself; `inventory.csv` and `provenance.json` are included. Private
data, credentials, database files, captures, and build outputs must never be
added to Git. Publish the ZIP only through Mike's authorized distribution
channel.

## Acceptance still required

On a clean Windows machine without developer tools, verify extraction to a
path containing spaces, first-run setup with no services or listeners,
explicit Start server with an existing SQL server left running, occupied game-port
handling, prerequisite handling, account
creation, client download/selection, server readiness, Mike's live login and
play check, clean shutdown, restart with the same character, and clear errors
for occupied ports or missing content. Record failures and the exact package
hash. User data must survive replacing binaries. Database upgrades require
their own backup and migration acceptance before being advertised.
