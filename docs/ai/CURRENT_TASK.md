# Current Task

Produce a local Windows package from public master
`6ec06bbd45e72496923275dcc8fd4e25fecace73`.

Mike authorized a precompiled package with the full release GameData, bundled
runtime and dedicated local MySQL, graphical first-run/account/play controls,
and a separate download of the fixed website client. Players must not need
Git, a compiler, developer tools, or terminal commands.

Implementation is isolated under `Tools/WindowsLocalPackage`. Reuse the current
Windows project graph, configuration/GameData environment overrides, schema
tools, Account Broker, and engine headless/shutdown signals. Do not change
gameplay or existing engine behavior. Private GameData and binary packages
remain outside tracked source. No Linux work or deployment is authorized.

PROVEN: the established Windows build completed on the exact master source.
The local Windows candidate ZIP is built. Its staged launcher inventory check,
every compressed payload hash, full GameData comparison against the authorized
source, and ZIP checksum verification succeeded. Bundled runtimes and MySQL
load, and the existing offline ZoneEngine_New startup validation succeeds.
The Classic website ZIP layout and executable checksum were inspected without
launching the client. The private ZIP, checksum and validation record remain in
ignored local output. No automated fixture, unit, or regression suites were
requested or run.

UNVERIFIED: clean-machine prerequisites/UAC, fresh database initialization,
account creation, complete client download, and live login/play/restart. Mike
performs live client acceptance; do not describe compilation as playable
acceptance. Existing databases must never be reset by first-run setup.

Packaging is complete as a local acceptance candidate. The next step is Mike's
fresh-install and live-client acceptance of that exact ZIP; distribution or
deployment has not been performed.
