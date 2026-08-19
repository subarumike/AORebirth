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

source_sha="$(git -C .. rev-parse HEAD)"
tracked_source_clean="PASS"
if ! git -C .. diff --quiet -- || ! git -C .. diff --cached --quiet --; then
  tracked_source_clean="FAIL"
fi
publish_dir="artifacts/zoneengine/${runtime_id}/${package_kind}"
dotnet_sdk_version="$(dotnet --version)"
build_timestamp_utc="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

printf '%s\n' "${source_sha}" > "${publish_dir}/SOURCE_SHA"
cat > "${publish_dir}/BUILD_PROVENANCE.env" <<EOF
REPOSITORY=AORebirth
COMMIT_SHA=${source_sha}
BUILD_PLATFORM=linux
RUNTIME_IDENTIFIER=${runtime_id}
CONFIGURATION=Release
SELF_CONTAINED=${self_contained}
DOTNET_SDK_VERSION=${dotnet_sdk_version}
TRACKED_SOURCE_CLEAN=${tracked_source_clean}
BUILD_TIMESTAMP_UTC=${build_timestamp_utc}
ACCEPTANCE_RESULT=UNVERIFIED
EOF
