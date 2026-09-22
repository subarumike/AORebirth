#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUNTIME_ID="${1:-linux-x64}"
SELF_CONTAINED="${2:-false}"
if [[ "$RUNTIME_ID" != "linux-x64" && "$RUNTIME_ID" != "linux-arm64" ]]; then exit 2; fi
if [[ "$SELF_CONTAINED" == "true" ]]; then PACKAGE_KIND="self-contained"; elif [[ "$SELF_CONTAINED" == "false" ]]; then PACKAGE_KIND="framework-dependent"; else exit 2; fi
cd "$SCRIPT_DIR"
dotnet run --project Tools/SourceInventoryGuard/SourceInventoryGuard.csproj -- --repository-root .. --manifest source-inventory/inventory.json --check
dotnet restore Projects/BotServiceHost.Linux.csproj --runtime "$RUNTIME_ID" --nologo
dotnet clean Projects/BotServiceHost.Linux.csproj --configuration Release --runtime "$RUNTIME_ID" --nologo
dotnet publish Projects/BotServiceHost.Linux.csproj --configuration Release --runtime "$RUNTIME_ID" --self-contained "$SELF_CONTAINED" --output "artifacts/botservice/$RUNTIME_ID/$PACKAGE_KIND" --no-restore --nologo
