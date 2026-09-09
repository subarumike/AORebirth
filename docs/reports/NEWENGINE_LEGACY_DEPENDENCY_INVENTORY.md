# Legacy source dependency inventory

The deterministic [JSON inventory](NEWENGINE_LEGACY_DEPENDENCY_INVENTORY.json)
contains 93 Legacy source/project edges, source SHA256 values, declared symbols,
compiler-resolved consumer symbols, classification and destination recommendations.
All 93 are direct links from the NewEngine project; none was moved or deleted.

| Classification | Edges |
| --- | ---: |
| SHARED_CONTRACT | 2 |
| SHARED_MODEL | 28 |
| SHARED_PROTOCOL | 2 |
| SHARED_GAMEPLAY_MECHANIC | 14 |
| SHARED_DATA_LOADER | 13 |
| LEGACY_SPECIFIC | 3 |
| UNKNOWN | 31 |
| OBSOLETE | 0 |

The tool follows NewEngine ProjectReference edges conservatively, reads explicit
Compile links, and binds the actual NewEngine compilation with the Windows
NET10 symbols and built reference assemblies. Semantic errors: 0.
92 files have resolved cross-file symbol uses. `MissionRewardCoordinator.cs`
has no resolved cross-file use in this build but remains an explicit compile
dependency; absence of a resolved use is not authorization to delete it.
Identifier-only candidates are retained separately and must not be confused
with the compiler-resolved field.

The classifications are conservative extraction proposals, not proof that a file
is content-free. 23 content-carrier candidates are marked explicitly. Inherited
captured/generated C# content must be separated into validated content data with
a neutral loader/model; moving the same constants into NewEngine is not the goal.
UNKNOWN and LEGACY_SPECIFIC entries require an owner decision. Reflection,
conditional builds, content files and runtime reachability are not proved by this
source-link inventory.

## Extraction order

1. Move content-free contracts/models to existing shared interfaces/domain
   ownership; update both engines' references and preserve namespace/API behavior.
2. Place reusable protocol types in the existing shared communication layer.
3. Extract reusable mechanics only after separating entity/item/quest-specific
   content into validated data. Preserve existing captured evidence and fixtures.
4. Put shared loading/validation with its data contract, then retire duplicate
   loaders only when both consumers build and equivalent data is tested.
5. Resolve unknown and Legacy-specific dependencies explicitly. Re-run the
   inventory, both builds and affected tests after each bounded move.

NewEngine -> neutral shared module <- Legacy is the target. No wholesale copy
into NewEngine and no parallel persistence implementation. A zero-link inventory
is necessary before deleting Legacy, along with launcher/build/deployment
references and a separately accepted recovery plan.

Reproduction after the normal Debug build:
`cmd /d /c Tools\generate_newengine_cutover_inventory.cmd --check`.
No source extraction was necessary to make this foundation build.
