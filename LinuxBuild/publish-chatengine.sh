#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
runtime_id="${1:-linux-x64}"
self_contained="${2:-false}"

case "$runtime_id" in
  linux-x64|linux-arm64) ;;
  *) exit 2 ;;
esac

case "$self_contained" in
  true) package_kind="self-contained" ;;
  false) package_kind="framework-dependent" ;;
  *) exit 2 ;;
esac

cd "$script_dir"

dotnet run --project Tools/SourceInventoryGuard/SourceInventoryGuard.csproj -- \
  --repository-root .. \
  --manifest source-inventory/inventory.json \
  --check

dotnet run --project Tools/PublishDirectoryGuard/PublishDirectoryGuard.csproj -- \
  .. \
  "$runtime_id" \
  "$package_kind"

dotnet restore Projects/ChatEngine.Linux.csproj \
  --runtime "$runtime_id" \
  --nologo

dotnet clean Projects/ChatEngine.Linux.csproj \
  --configuration Release \
  --runtime "$runtime_id" \
  --nologo

dotnet publish Projects/ChatEngine.Linux.csproj \
  --configuration Release \
  --runtime "$runtime_id" \
  --self-contained "$self_contained" \
  --output "artifacts/chatengine/$runtime_id/$package_kind" \
  --nologo

dotnet run --project Tools/Stage5OfflineSmokeTests/Stage5OfflineSmokeTests.csproj \
  --configuration Release \
  -- \
  .. \
  "artifacts/chatengine/$runtime_id/$package_kind" \
  --structure-only \
  "$runtime_id" \
  "$package_kind"
