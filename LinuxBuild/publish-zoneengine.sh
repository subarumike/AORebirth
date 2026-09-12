#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
source "${script_dir}/placement-provenance.sh"
runtime_id="${1:-linux-x64}"
self_contained="${2:-false}"
engine="${3:-new}"
case "${engine}" in
  new) project="../AORebirth/Server/ZoneEngine_New/ZoneEngine_New.csproj"; artifact_name="zoneengine" ;;
  legacy) project="Projects/ZoneEngine.Linux.csproj"; artifact_name="zoneengine-legacy" ;;
  *) echo "engine must be new or explicit legacy" >&2; exit 2 ;;
esac

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
source_sha="$(git -C .. rev-parse HEAD)"

dotnet run --project Tools/SourceInventoryGuard/SourceInventoryGuard.csproj -- \
  --repository-root .. \
  --manifest source-inventory/inventory.json \
  --check

rm -rf -- "artifacts/${artifact_name}/${runtime_id:?}/${package_kind:?}"
mkdir -p -- "artifacts/${artifact_name}/${runtime_id}/${package_kind}"

dotnet restore "${project}" \
  --runtime "${runtime_id}" \
  --nologo

dotnet clean "${project}" \
  --configuration Release \
  --runtime "${runtime_id}" \
  --nologo

dotnet publish "${project}" \
  --configuration Release \
  --runtime "${runtime_id}" \
  --self-contained "${self_contained}" \
  --output "artifacts/${artifact_name}/${runtime_id}/${package_kind}" \
  --no-restore \
  --nologo

if [[ "${engine}" == "new" ]]; then
  dotnet run --project Tools/BackendIntegrationGuard/BackendIntegrationGuard.csproj --configuration Release -- \
    --repository-root .. --publish "artifacts/${artifact_name}/${runtime_id}/${package_kind}" \
    --source-sha "${source_sha}" --build-platform linux --self-test
else
  dotnet build Tools/Stage8OfflineSmokeTests/Stage8OfflineSmokeTests.csproj \
    --configuration Release \
    --verbosity minimal

  if [[ "${self_contained}" == "true" ]]; then
    dotnet Tools/Stage8OfflineSmokeTests/bin/Release/net10.0/Stage8OfflineSmokeTests.dll \
      --repository-root .. \
      --zone-output "artifacts/${artifact_name}/${runtime_id}/${package_kind}" \
      --source-sha "${source_sha}" \
      --build-platform linux \
      --structure-only
  else
    dotnet Tools/Stage8OfflineSmokeTests/bin/Release/net10.0/Stage8OfflineSmokeTests.dll \
      --repository-root .. \
      --zone-output "artifacts/${artifact_name}/${runtime_id}/${package_kind}" \
      --source-sha "${source_sha}" \
      --build-platform linux
  fi
fi

tracked_source_clean="PASS"
if ! git -C .. diff --quiet -- || ! git -C .. diff --cached --quiet --; then
  tracked_source_clean="FAIL"
fi
publish_dir="artifacts/${artifact_name}/${runtime_id}/${package_kind}"
if ! placement_provenance_load "${publish_dir}" "${source_sha}" linux; then
  echo "Official placement publish provenance validation failed." >&2
  exit 1
fi
dotnet_sdk_version="$(dotnet --version)"
build_timestamp_utc="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

printf '%s\n' "${source_sha}" > "${publish_dir}/SOURCE_SHA"
cat > "${publish_dir}/BUILD_PROVENANCE.env" <<EOF
REPOSITORY=AORebirth
ZONEENGINE_IMPLEMENTATION=${engine}
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
placement_append_build_provenance "${publish_dir}/BUILD_PROVENANCE.env"
