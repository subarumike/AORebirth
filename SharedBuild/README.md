# Shared Windows build support

This directory contains source inventories, compatibility adapters and validation
fixtures required by the authoritative Windows build. It contains no production
deployment implementation. Shared gameplay remains in AORebirth.

Windows acceptance checks the compiled Windows contracts here. Production builds
and target-platform compatibility checks are maintained separately in the private
operations repository and consume an exact Windows-accepted source commit.
