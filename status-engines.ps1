# Compatibility entry point: use the same configuration and exact process/listener
# ownership validation as status-engines.cmd.
$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "status-engines.cmd") @args
exit $LASTEXITCODE
