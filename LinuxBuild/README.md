# Linux build from public master

Public GitHub `master` is the only source authority. Windows and Linux build
the exact same Git commit. Linux portability code, projects, tests and reusable
deployment tooling live in this repository and are reviewed through public master.
There is no private source assembly, Git patch, or Linux child commit.

The release invariant is:

```text
origin/master == linux-private/master == Linux build HEAD
              == live LoginEngine SOURCE_SHA == live ZoneEngine_New SOURCE_SHA
```

## Source acceptance

From a clean exact-SHA checkout on Windows:

```cmd
cmd /d /c Tools\test_linux_source_identity.cmd
cmd /d /c Tools\accept_windows_source.cmd --expected-sha <sha> --mandatory-gate
```

After Windows acceptance passes and `<sha>` is public master, mirror that exact
commit to `linux-private/master`. Preserve its previous live commit under an
archive ref before moving the mirror. Verify both remote refs and zero commits
and source differences between them. Do not force public master backwards.

Use an isolated Linux SDK environment with Git LFS, Python 3, zip and unzip.
Supply the pinned Playfields archive outside the controlled checkout as documented
in `docs/project/PLAYFIELD_PACKAGE_SUPPLY.md`:

```sh
export AO_REBIRTH_PLAYFIELD_PACKAGE_ARCHIVE=/inputs/playfields.zip
bash LinuxBuild/run-native-content-acceptance.sh <sha> /acceptance
```

The governed acceptance clones public master, checks exact identity against
GitHub before and after publication, rejects dirty/untracked source and private
assembly metadata, validates inventories and content, runs runtime/deployment
regressions, and publishes accepted artifacts. A failure requires a public-master
repair and new Windows acceptance before retrying Linux.

For development compilation only, use `LinuxBuild\build-linux.cmd` on Windows
or `bash LinuxBuild/build-linux.sh` on Linux. NewEngine is the only supported
fresh zone build; historical recovery artifacts are retained without rebuilding
the retired engine.

## Deployment

After both exact-SHA gates pass, use
`deployment/production-release/README.md` to package, preflight, dry-run and
transactionally deploy LoginEngine/ZoneEngine_New. Runtime source and release
tooling source must use the same accepted public-master SHA. Build artifacts
outside production; preserve prior release directories and rollback snapshots.

Active environment files, secrets, installed systemd overrides, permissions,
symlinks and host settings remain external. Repository unit files are reusable
templates, not the authority for server secrets. Deployment does not authorize
database migrations or client operation. Report unperformed client checks honestly.
