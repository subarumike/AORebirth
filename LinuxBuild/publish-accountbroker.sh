#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUNTIME_ID="${1:-linux-x64}"
SELF_CONTAINED="${2:-false}"

case "${RUNTIME_ID}" in
    linux-x64|linux-arm64) ;;
    *) exit 2 ;;
esac

case "${SELF_CONTAINED}" in
    true) PACKAGE_KIND="self-contained" ;;
    false) PACKAGE_KIND="framework-dependent" ;;
    *) exit 2 ;;
esac

cd "${SCRIPT_DIR}"

dotnet run --project Tools/SourceInventoryGuard/SourceInventoryGuard.csproj -- --repository-root .. --manifest source-inventory/inventory.json --check
dotnet restore Projects/AccountBrokerService.Linux.csproj --runtime "${RUNTIME_ID}" --nologo
dotnet clean Projects/AccountBrokerService.Linux.csproj --configuration Release --runtime "${RUNTIME_ID}" --nologo
dotnet publish Projects/AccountBrokerService.Linux.csproj --configuration Release --runtime "${RUNTIME_ID}" --self-contained "${SELF_CONTAINED}" --output "artifacts/accountbroker/${RUNTIME_ID}/${PACKAGE_KIND}" --no-restore --nologo
