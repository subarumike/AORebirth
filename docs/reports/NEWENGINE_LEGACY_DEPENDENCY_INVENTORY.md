# Legacy source dependency inventory

The current deterministic [JSON inventory](NEWENGINE_LEGACY_DEPENDENCY_INVENTORY.json)
contains **zero Legacy source/project dependency edges**. The Legacy executable
and its source project are retired. NewEngine builds without that source tree.

The 57 reusable source files still linked at the start of retirement moved under
NewEngine's `SharedGameplay` ownership with namespaces and behavior unchanged.
The 21 entities used by the full shared Core library moved into Core's own
`Entities` folder. Editable content moved to NewEngine's content directory;
historical test and offline-analysis fixtures are outside the runtime tree.

The earlier content cleanup had already moved NPC, vendor, quest, dialogue,
mission and weapon definitions into editable data. Retirement does not restore
compiled content or evidence-based spawn authorization.

The tool follows the NewEngine project-reference graph, reads explicit Compile
links and binds symbols using Windows NET10 reference assemblies. This inventory
does not independently prove reflection behavior or gameplay parity. The runtime
content guard and acceptance suites provide separate checks.

Reproduce after the normal Debug build:

```cmd
cmd /d /c Tools\generate_newengine_cutover_inventory.cmd --check
```

See `LEGACY_ENGINE_RETIREMENT.md` for the removal scope and acceptance receipt.
