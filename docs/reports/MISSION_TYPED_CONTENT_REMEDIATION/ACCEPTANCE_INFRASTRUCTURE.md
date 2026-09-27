# Acceptance infrastructure reconciliation

Base mission candidate: `9fb4e17c6d43a35f286cec59abee8483fd3d8e62`.

The public-master merge changed 25 runtime source files and removed two recorded
files after the previous architecture receipt was generated. Regenerated
`docs/reports/NEWENGINE_CONTENT_ARCHITECTURE_GUARD.json` using the existing
`cmd /d /c Tools\run_newengine_content_architecture_guard.cmd --write` mechanism,
then verified it using `--check`: 714 runtime source files, zero violations,
zero unresolved project inputs. No guard logic or runtime source changed.

Delmus's public commit `4f2812c210138779ac2ecf42485f40fe4c92ba4d` moved item
names into `items.dat` and removed `MySqlItemNameRepository`. The connected
fixture now calls the current production `ItemTemplateCatalog(IGameData, logger)`
constructor. Its existing catalog-root proxy selects the same explicitly
configured GameData. No production shim or alternate data source was added.

The frozen acceptance dataset documented in CONNECTED_FOLLOWUP.md is a staged
snapshot, selected through the existing `--runtime-gamedata` fixture option and
`AO_REBIRTH_GAMEDATA_PATH`; it is not automatically synchronized by a build.
The offline mission normalizer generates mission content only. Re-running it
is neither necessary nor appropriate for an unrelated public startup catalog.

Added only `AlienXp.json` to the existing frozen snapshot using the exact Git
blob `4f2812c210138779ac2ecf42485f40fe4c92ba4d:AORebirth/GameData/AlienXp.json`.
The public file is already tracked; the full frozen dataset remains ignored.
Its staged bytes are 1441 bytes, SHA-256
`803440621b06ba9eb52127daf85e008f6b346fb845d81c47ad33d648d9426c16`.
No private GameData was read or copied for this repair. All previous 7254 files
retain their exact size and SHA-256. The staged snapshot now has 7255 files.

For future acceptance staging, retain the existing normalized snapshot and
include this committed public catalog at its root before invoking the existing
connected wrapper. Preserve MissionOffers.json and Layouts.json; do not restore
RollBodies.json, RollTemplate.json, historical packet properties, or captures.
The fixture's existing fail-closed normalized-input check remains unchanged.

Targeted validation: Windows build gate PASS; architecture check PASS; connected
fixture compilation PASS; offline `--validate-startup` with the frozen root PASS;
packet-free scan PASS. No mission implementation, GameData root-selection logic,
public GameData, test assertions, Linux files, or capture evidence changed.
Final exact-SHA acceptance receipts are recorded separately under build-verify.
