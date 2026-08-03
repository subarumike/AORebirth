# Current Task

## Active

Complete and publish the offline WebEngine PHP 8.5.9 / CellAO WebCore
compatibility boundary. The implementation is fail-closed and does not change
supported game runtime behavior.

The selected PHP authority is the official x64 NTS VS17 PHP 8.5.9 archive. The
content authority remains CellAO WebCore commit
`765c3850767b63af1cd259bab7f2f7ca3e97adf9`. Neither runtime payload is tracked
in Git or acquired by a server process.

## Reconciliation scope

- Pin and validate the exact official PHP archive, complete extracted tree,
  approved INI, required modules, x64/NTS build facts, and a real CGI probe.
- Audit all 7,140 WebCore files and all 25 PHP files, then apply only the seven
  deterministic compatibility/security transformations recorded by the
  checked-in manifests.
- Expose only the allowlisted read-only PHP routes and approved static file
  types; reject administrative, authentication, registration, logout, member,
  mutation, traversal, encoded-separator, and unsupported routes.
- Hold exclusive PHP and WebCore leases for WebEngine process lifetime, repeat
  complete validation before listener creation, and keep imports explicit and
  offline.
- Run the focused supply, compatibility, security, lint, engine-management,
  build, secret, and complete 13-stage acceptance paths before publication.

## External blockers and support boundary

- No valid disposable WebCore database credential is available. Live database
  connectivity and credential-backed WebEngine startup remain unverified and
  must not be simulated with invented values.
- A previously exposed database credential still requires external rotation.
- The historical listener is plaintext HTTP, so secure-only cookies cannot be
  enabled without a transport change. WebEngine remains development-only.
- No license file was found in the pinned upstream WebCore snapshot. Integrity
  and compatibility validation do not grant redistribution rights.

## Authoritative evidence

- `docs/evidence/WEBENGINE_PHP_COMPATIBILITY_20260802.md`
- `docs/project/PHP_RUNTIME_SUPPLY.md`
- `docs/project/WEBCORE_ASSET_SUPPLY.md`
- `docs/generated/webcore_php_compatibility_inventory.json`
- `AORebirth/Config/PhpRuntime.manifest.xml`
- `AORebirth/Config/WebCoreAssets.manifest.xml`
- `AORebirth/Config/WebCoreCompatibility.manifest.xml`
- `AORebirth/Config/WebCorePatchedAssets.manifest.xml`
