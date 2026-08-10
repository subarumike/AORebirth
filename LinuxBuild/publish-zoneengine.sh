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

rm -rf -- "artifacts/zoneengine/${runtime_id:?}/${package_kind:?}"
mkdir -p -- "artifacts/zoneengine/${runtime_id}/${package_kind}"

dotnet restore Projects/ZoneEngine.Linux.csproj \
  --runtime "${runtime_id}" \
  --nologo

dotnet clean Projects/ZoneEngine.Linux.csproj \
  --configuration Release \
  --runtime "${runtime_id}" \
  --nologo

dotnet publish Projects/ZoneEngine.Linux.csproj \
  --configuration Release \
  --runtime "${runtime_id}" \
  --self-contained "${self_contained}" \
  --output "artifacts/zoneengine/${runtime_id}/${package_kind}" \
  --no-restore \
  --nologo

dotnet build Tools/Stage8OfflineSmokeTests/Stage8OfflineSmokeTests.csproj \
  --configuration Release \
  --verbosity minimal

if [[ "${self_contained}" == "true" ]]; then
  dotnet Tools/Stage8OfflineSmokeTests/bin/Release/net10.0/Stage8OfflineSmokeTests.dll \
    --repository-root .. \
    --zone-output "artifacts/zoneengine/${runtime_id}/${package_kind}" \
    --structure-only
else
  dotnet Tools/Stage8OfflineSmokeTests/bin/Release/net10.0/Stage8OfflineSmokeTests.dll \
    --repository-root .. \
    --zone-output "artifacts/zoneengine/${runtime_id}/${package_kind}"
fi
