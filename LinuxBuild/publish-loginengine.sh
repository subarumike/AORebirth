#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
runtime_id="${1:-linux-x64}"
self_contained="${2:-false}"

case "${runtime_id}" in
  linux-x64|linux-arm64) ;;
  *) exit 2 ;;
esac

case "${self_contained}" in
  true) package_kind="self-contained" ;;
  false) package_kind="framework-dependent" ;;
  *) exit 2 ;;
esac

cd "${script_dir}"

dotnet run --project Tools/SourceInventoryGuard/SourceInventoryGuard.csproj -- \
  --repository-root .. \
  --manifest source-inventory/inventory.json \
  --check

dotnet run --project Tools/LoginEnginePublishDirectoryGuard/LoginEnginePublishDirectoryGuard.csproj -- \
  .. \
  "${runtime_id}" \
  "${package_kind}"

dotnet restore Tools/Stage7OfflineSmokeTests/Stage7OfflineSmokeTests.csproj \
  --nologo

dotnet restore Projects/LoginEngine.Linux.csproj \
  --runtime "${runtime_id}" \
  --nologo

dotnet clean Projects/LoginEngine.Linux.csproj \
  --configuration Release \
  --runtime "${runtime_id}" \
  --nologo

dotnet publish Projects/LoginEngine.Linux.csproj \
  --configuration Release \
  --runtime "${runtime_id}" \
  --self-contained "${self_contained}" \
  --output "artifacts/loginengine/${runtime_id}/${package_kind}" \
  --no-restore \
  --nologo

dotnet run --project Tools/Stage7OfflineSmokeTests/Stage7OfflineSmokeTests.csproj \
  --configuration Release \
  --no-restore \
  -- \
  .. \
  "artifacts/loginengine/${runtime_id}/${package_kind}" \
  --structure-only \
  "${runtime_id}" \
  "${package_kind}"
