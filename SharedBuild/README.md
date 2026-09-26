# Shared Windows build support

This directory contains source inventories, compatibility adapters and validation
fixtures required by the authoritative Windows build. It contains no production
deployment implementation. Shared gameplay remains in AORebirth.

Windows acceptance checks the compiled Windows contracts here. Linux
target-platform validation, production packaging, and deployment tooling are
maintained under [LinuxBuild](../LinuxBuild/README.md) and consume the same
accepted public-master SHA.
