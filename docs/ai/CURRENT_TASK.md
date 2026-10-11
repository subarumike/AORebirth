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
The Classic website ZIP layout and executable checksum were inspected without
launching the client. Package construction and offline verification are in
progress. No automated fixture, unit, or regression suites were requested or run.

UNVERIFIED: clean-machine prerequisites/UAC, fresh database initialization,
account creation, complete client download, and live login/play/restart. Mike
performs live client acceptance; do not describe compilation as playable
acceptance. Existing databases must never be reset by first-run setup.
