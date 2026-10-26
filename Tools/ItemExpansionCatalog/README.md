# Item expansion classification inventory

Offline catalog generation only. No engine, client, database, deployment, loot
table or inventory behavior is changed. Private item records and generated lists
must stay in the selected private GameData root, outside public Git history.

From the product checkout, run `cmd /d /c Tools\generate-item-expansion-catalog.cmd --gamedata-root "<private GameData root>"`.
This compiles only this standalone tool using the existing extractor's tool-build
pattern, then writes `Loot/ExpansionClassification/` under that explicit root.
It does not build or start the product engines or prepare a release.

Run the same command with `--verify` to independently regenerate and compare
every output byte without accepting a replacement. Run the wrapper with
`--self-test` for the bounded in-memory classification/coverage checks requested
for this tool. Two real-data invocations suffice: generation and verification.
Successful independent verification writes `Loot/ExpansionClassification.verify.json`
beside the catalog with the actual result and hashes of all compared files.

The linked existing `ItemsDatReader` and `DatItemTemplate` definitions read the
original database. `HashItemCatalog` supplies explicit family links. There is
no second item reader, inferred name pairing or replacement item database.

Explicit positive item `User Expansion` requirements are accepted expansion
association evidence under Mike's classification rule. An item's requirement
for Shadowlands, Alien Invasion, Lost Eden or Legacy of the Xan can place it in
that primary file; supported Notum Wars and PMV Obsidian Edition requirements
have their own files. Multiple verified associations belong in
`MultipleExpansions.json`, with every association and its original evidence
preserved. This establishes requirement-backed association, not original
release-date history.

The classifier evaluates positive Expansion BitAnd requirements and equality to
a nonzero known mask in known item
actions, including ToUse, ToWear and ToWield, with target selectors and complete
boolean structure. OR branches can establish multiple association alternatives
only when every possible branch requires a known expansion; an alternative
without an expansion requirement leaves the result Unknown. Negative expansion
checks, unsupported values and conflicting evidence do not invent positive
associations. A multi-bit overlap test preserves possible named associations
and its ANY semantics rather than claiming all bits are required. Explicitly
named preorder bits map to their parent expansion while retaining the original
mask; Mail alone is not an expansion assignment. Direct account-style/stat expansion values,
playfield context and spell conditions remain raw evidence rather than being
automatically promoted to item classifications. Zero or missing requirements
never mean Rubi-Ka. Names, template ID ranges, QL and appearance are never used
to infer expansion membership. An incomplete present Rdb record remains
Unknown; explicit supported legacy Actions can still classify a record that
has no Rdb object.

Each record retains its original template identity, name, stored QL, explicit
family QL ranges where established, raw category fields and the precise reason
for its actual primary assignment. `SourceRecordIndex` preserves input definitions even
when identities repeat. Published records sort by template ID, QL and original
record index. No timestamps or random identifiers occur inside published files.

The report distinguishes missing, ambiguous, unsupported, conflicting and
incomplete evidence. `ClassificationSummary.json` binds the input hashes and
each primary list's hash and record count. A repeat invocation verifies the
complete output. A successful replacement retains the previous whole catalog
alongside it; unrelated user files are never recursively pruned.

The generated report gives measured counts and real examples for every
populated classification. Full ordered requirement expressions and
raw values remain available even when the primary assignment is Unknown.

RK-only filtering remains `NOT_READY` unless a separate supported source proves
Rubi-Ka membership; the current requirement rules do not establish it by
absence. A positive Shadowlands association does not by itself identify a
garden/sanctuary key. Expansion membership, structural item kind and normal
chest eligibility remain separate decisions; no loot behavior changes here.
