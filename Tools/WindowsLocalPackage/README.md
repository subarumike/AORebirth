# AORebirth for local Windows play

Extract the complete ZIP to a writable folder, then double-click `AORebirth.exe`.
Use a folder outside Program Files and outside a synchronized cloud folder.
Keep the entire extracted folder together.

First-run setup prepares a private local database and lets you create an account.
Download the fixed AO client from the launcher, or select an existing copy of
that client. Play starts the bundled server and then opens the client. Stop
Server saves and stops the processes owned by this installation. Open Logs
shows diagnostic files if startup fails.

No Git, Visual Studio, .NET SDK, compilation, SQL commands, or terminal commands
are required on the player's machine. Supported target: Windows 11 x64 with
.NET Framework 4.8 enabled. The package includes the .NET 10 runtime, ASP.NET
Core runtime when required, MySQL binaries, and the Microsoft VC++ prerequisite
installer. The launcher reports missing prerequisites before starting setup.

Accounts, characters, passwords, and the local database are created under
`data` at first run. Do not delete `data` when replacing a package. This first
package does not automate upgrades of existing databases; retain the existing
installation until an upgrade has been separately accepted. Never run two
copies of the local package on the same default ports.
Protected local credentials are tied to the current Windows user and machine;
this first release does not support moving its data to another account or PC.

The package contains the full AORebirth GameData supplied for this release.
`LICENSE`, `NOTICE`, and bundled upstream notices accompany the software.
Do not redistribute components beyond their stated terms.

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
path containing spaces, first-run setup, prerequisite handling, account
creation, client download/selection, server readiness, Mike's live login and
play check, clean shutdown, restart with the same character, and clear errors
for occupied ports or missing content. Record failures and the exact package
hash. User data must survive replacing binaries. Database upgrades require
their own backup and migration acceptance before being advertised.
