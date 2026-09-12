# Offline Playfields package supply

The pinned `docs/generated/playfields/playfield-package-manifest.json` identifies
4,710 files / 264,151,897 extracted bytes. The archive contains only the
`AORebirth/GameData/Playfields` subtree. Existing tracked root GameData catalogs
and `items.dat` are not regenerated, archived, or replaced by this workflow.

This is **structural-only source material, not runtime activation authority**.
Geometry/placement extraction does not establish an NPC identity, a New-engine
profile bridge, mission logic, loot, combat, or permission to spawn anything.
The official placement corpus and capture-backed runtime gates remain separate.
The supplied installation's client version is deliberately unasserted. The
manifest records metadata raw-resource hashes where they exist, source/build
fingerprints of the inspected exporter, and the exact recorded extraction
invocation. It does not invent missing raw-resource identities or a Git SHA.

## Immutable archive

- File index SHA256: `aa7dca68a698a720c7781039d92b680d7eb62d89577d83e2ab9c1959149efb67`
- ZIP SHA256: `6b3c06c234923d07a8d476b710d650e620aafd0775602c2e38160af8bdc69b26`
- ZIP size: 264,668,325 bytes.
- Local integration artifact: `build-verify/playfield-package/aorebirth-playfields-structural-v1.zip` (ignored; not committed).

ZIP entries are sorted, stored without compression, timestamped identically, and
given fixed regular-file permissions. The generated ZIP32 central-directory
creator-platform byte is normalized because the framework otherwise stamps the
host OS. This normalization touches only new exporter output, never supplied
archives. `export` validates the source tree first and then requires the new
archive to match the pinned archive digest. The framework behavior is documented
in its [ZipArchiveEntry implementation](https://github.com/dotnet/runtime/blob/main/src/libraries/System.IO.Compression/src/System/IO/Compression/ZipArchiveEntry.cs).

## Approved commands

Run from the repository root. On Windows use the `.cmd` wrapper; on Linux invoke
the `.sh` wrapper with `bash`. The tool is offline and has no runtime network,
database, game-client, engine-control, or migration authority.

```text
Tools\manage_playfield_package.cmd --self-test
Tools\manage_playfield_package.cmd validate-current --root AORebirth\GameData\Playfields --manifest docs\generated\playfields\playfield-package-manifest.json
Tools\manage_playfield_package.cmd import --root AORebirth\GameData\Playfields --manifest docs\generated\playfields\playfield-package-manifest.json --archive <explicit-local-archive>
Tools\manage_playfield_package.cmd export --root AORebirth\GameData\Playfields --manifest docs\generated\playfields\playfield-package-manifest.json --archive <new-local-output-archive>
```

`import` requires an absent or empty managed `GameData/Playfields` directory. It
verifies the archive's SHA256/size, rejects traversal, casing conflicts, duplicate,
missing, extra, directory, and symlink archive entries, bounds extracted lengths,
then verifies every staged file before moving the complete tree into place.
Existing files are never merged, overwritten, or pruned. A nonempty target stays
unchanged and causes failure. Only an empty managed directory or a private
importer-created staging directory can be removed. Source/output path chains
must not contain symlinks or Windows reparse points.

The pinned manifest is a trusted repository input; do not accept a replacement
manifest shipped alongside an untrusted archive. `create` is an explicit
maintainer operation for producing a separately reviewed new manifest/archive,
not a normal build step or a workaround for a hash mismatch:

```text
Tools\manage_playfield_package.cmd create --root AORebirth\GameData\Playfields --manifest <new-manifest-output> --archive <new-archive-output> --repository-root . --provenance-file docs\generated\playfields\playfield-package-extraction-provenance.json
```

Update extraction provenance only when a newly authorized offline extraction
actually occurs. `create`/`export` refuse existing outputs, including failed or
partial previous outputs; they do not delete or replace them implicitly.

## Clean Linux exact-SHA acceptance

The governed acceptance checkout is cleaned before each run. Store the approved
archive outside that checkout, for example under the controlled workspace's
`inputs/` directory, and explicitly set `AO_REBIRTH_PLAYFIELD_PACKAGE_ARCHIVE` to
that local file. No implicit external path, network download, or repository
fallback is used. The acceptance wrapper rejects archives inside the checkout
before cleanup, then runs package self-tests and imports against the exact
detached revision's pinned manifest before building. Archive transfer and Linux
acceptance occur only after the matching Windows acceptance gate.

Both platforms' New-engine offline startup checks validate packaged Playfields
bytes against this manifest, not merely the existence of a directory. A package
PASS proves complete byte identity only; it does not waive gameplay/profile
parity or production migration/deployment gates.
