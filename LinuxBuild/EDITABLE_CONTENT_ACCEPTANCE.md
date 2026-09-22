# Editable content acceptance

The exact public-master source commit supplies runtime content and Linux build
tools. `accept-linux-sha.sh` requires that same commit, an isolated workspace and
the explicit operator-supplied Playfields archive outside the checkout.

Archive import validates paths, rejects overwrites and records SHA256, file count
and bytes. Content manifests bind the published artifact to the accepted source.
Content/package integrity does not authorize new gameplay content.

Fresh zone builds contain only NewEngine. Historical recovery helpers, provenance
validation and the recovery unit remain for existing backups. They do not build
or publish the retired engine. Release format 3 uses editable content integrity;
historical format 2 retains its original placement validation.

Run the commands in `README.md`. Do not assemble private source, apply patches,
rewrite manifest source identities or create a separate Linux commit. Both platform
acceptances and the transactional deployment gates remain required.
