# Current Task

Connect the existing generated mission and quest-dungeon paths directly to
`Utility.GameData.Missions.MissionDestinationCatalog` on
`codex/mission-destination-integration`, starting at public master
`bddea56ca2d47c5eb64ed53f6e9efd279ff4f694`.

PROVEN: the clean baseline builds; its full ZoneEngine_New test project has six
pre-existing CS0246 missing-type compile errors. The integrated runtime also
builds, and the full test project still reports those same six errors.

PROVEN: the explicitly approved DAO exception changes only generated-offer
entrance validation from `<= 0` to `== 0`. All 74 focused disposable-MySQL
checks passed, including exact high-bit identity persistence/readback and
unchanged positive-only owner, offer, quest, key and item contracts.
`DAO_CODE_CHANGED=YES_SCOPED_ENTRANCE_IDENTITY_FIX`; no schema change.

Exact captured condition selection and WorldPos projection are implemented;
the old 140-location population and separate entrance catalog are retired.
All 40 focused catalog/generation/acceptance/key/dungeon checks passed using
the configured full GameData root. Exact-identity comparison against all
2,242 current Dynels records proved raw rotation WXYZ maps to engine XYZW.
The matching constructor correction passed the final approved build and all
40 focused checks, with zero failures or skips. Implementation and validation
are complete on this feature branch. The final default/full test compilation
reports exactly the same six baseline errors. Live AO client acceptance remains
UNVERIFIED; merging and deployment are not part of this slice.

Scope and evidence: `docs/evidence/MISSION_DESTINATION_INTEGRATION.md`.

Objective, reward, Kill Person fencing, operational BD/key reconstruction,
Linux work, deployment, and AO client automation are outside this slice.
