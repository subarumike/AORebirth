#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_dir}/../../.." && pwd)"
source "${repository_root}/LinuxBuild/placement-provenance.sh"
expected_sha=""
login_artifact_dir=""
zone_artifact_dir=""
login_unit=""
zone_unit=""
output=""
previous_login_release=""
previous_zone_release=""

fail() { echo "FAIL: $*" >&2; exit 1; }
usage()
{
    echo "usage: create-release-manifest.sh --expected-sha <sha> --login-artifact-dir <dir> --zone-artifact-dir <dir> --login-unit <file> --zone-unit <file> --output <file> [--previous-login-release <id>] [--previous-zone-release <id>]" >&2
}

while [[ "$#" -gt 0 ]]; do
    case "$1" in
        --expected-sha) expected_sha="${2:-}"; shift 2 ;;
        --login-artifact-dir) login_artifact_dir="${2:-}"; shift 2 ;;
        --zone-artifact-dir) zone_artifact_dir="${2:-}"; shift 2 ;;
        --login-unit) login_unit="${2:-}"; shift 2 ;;
        --zone-unit) zone_unit="${2:-}"; shift 2 ;;
        --output) output="${2:-}"; shift 2 ;;
        --previous-login-release) previous_login_release="${2:-}"; shift 2 ;;
        --previous-zone-release) previous_zone_release="${2:-}"; shift 2 ;;
        --help) usage; exit 0 ;;
        *) usage; exit 2 ;;
    esac
done

[[ "${expected_sha}" =~ ^[0-9a-f]{40}$ ]] || fail "invalid expected source SHA"
[[ "$(git -C "${repository_root}" rev-parse HEAD)" == "${expected_sha}" ]] || fail "repository HEAD does not match expected source SHA"
git -C "${repository_root}" diff --quiet -- || fail "tracked source is dirty"
git -C "${repository_root}" diff --cached --quiet -- || fail "tracked index is dirty"

login_artifact_dir="$(realpath -e -- "${login_artifact_dir}")"
zone_artifact_dir="$(realpath -e -- "${zone_artifact_dir}")"
login_unit="$(realpath -e -- "${login_unit}")"
zone_unit="$(realpath -e -- "${zone_unit}")"
[[ "${login_unit}" == "${repository_root}/LinuxBuild/deployment/systemd/ao-rebirth-loginengine.service" ]] || fail "LoginEngine unit is not the repository-controlled unit"
[[ "${zone_unit}" == "${repository_root}/LinuxBuild/deployment/systemd/ao-rebirth-zoneengine.service" ]] || fail "ZoneEngine unit is not the repository-controlled unit"

for pair in "${login_artifact_dir}:LoginEngine" "${zone_artifact_dir}:ZoneEngine"; do
    artifact_dir="${pair%:*}"
    apphost="${pair##*:}"
    [[ -f "${artifact_dir}/${apphost}" && ! -L "${artifact_dir}/${apphost}" ]] || fail "missing ${apphost} artifact"
    [[ "$(tr -d '\r\n\t ' < "${artifact_dir}/SOURCE_SHA")" == "${expected_sha}" ]] || fail "${apphost} SOURCE_SHA does not match"
    grep -Fx "COMMIT_SHA=${expected_sha}" "${artifact_dir}/BUILD_PROVENANCE.env" >/dev/null || fail "${apphost} build provenance does not match"
    grep -Fx "LINUX_ACCEPTANCE=PASS" "${artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null || fail "${apphost} Linux acceptance did not pass"
done

placement_provenance_load "${zone_artifact_dir}" "${expected_sha}" linux \
    || fail "ZoneEngine official placement provenance is invalid"
placement_require_build_provenance "${zone_artifact_dir}/BUILD_PROVENANCE.env" \
    || fail "ZoneEngine build provenance lacks official placement evidence"
grep -Fx "PLACEMENT_VALIDATION=PASS" "${zone_artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null \
    || fail "ZoneEngine official placement acceptance did not pass"
grep -Fx "EXPECTED_PLACEMENT_BUILD_MANIFEST_SHA256=${PLACEMENT_BUILD_MANIFEST_SHA256}" \
    "${zone_artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null \
    || fail "ZoneEngine accepted placement manifest digest does not match"
grep -Fx "PLACEMENT_BUILD_MANIFEST_SHA256=${PLACEMENT_BUILD_MANIFEST_SHA256}" \
    "${zone_artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null \
    || fail "ZoneEngine placement manifest acceptance provenance is missing"

[[ -n "${output}" ]] || fail "manifest output is required"
output_parent="$(mkdir -p -- "$(dirname -- "${output}")" && cd -- "$(dirname -- "${output}")" && pwd)"
output="${output_parent}/$(basename -- "${output}")"
temporary="${output}.tmp.$$"
trap 'rm -f -- "${temporary}"' EXIT
cat > "${temporary}" <<EOF
FORMAT=2
SOURCE_SHA=${expected_sha}
BUILD_TIMESTAMP_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)
LOGINENGINE_ARTIFACT_DIR=${login_artifact_dir}
LOGINENGINE_ARTIFACT_SHA256=$(sha256sum "${login_artifact_dir}/LoginEngine" | awk '{print $1}')
ZONEENGINE_ARTIFACT_DIR=${zone_artifact_dir}
ZONEENGINE_ARTIFACT_SHA256=$(sha256sum "${zone_artifact_dir}/ZoneEngine" | awk '{print $1}')
PLACEMENT_CORPUS_VERSION=${PLACEMENT_CORPUS_VERSION}
PLACEMENT_CORPUS_MANIFEST_SHA256=${PLACEMENT_CORPUS_MANIFEST_SHA256}
PLACEMENT_CORPUS_SUMMARY_SHA256=${PLACEMENT_CORPUS_SUMMARY_SHA256}
PLACEMENT_CORPUS_INDEX_SHA256=${PLACEMENT_CORPUS_INDEX_SHA256}
PLACEMENT_ACGHASH_INVENTORY_SHA256=${PLACEMENT_ACGHASH_INVENTORY_SHA256}
PLACEMENT_BUILD_MANIFEST_SHA256=${PLACEMENT_BUILD_MANIFEST_SHA256}
PLACEMENT_RESOURCE_COUNT=${PLACEMENT_RESOURCE_COUNT}
PLACEMENT_PARSED_RESOURCE_COUNT=${PLACEMENT_PARSED_RESOURCE_COUNT}
PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT=${PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT}
PLACEMENT_DISTRICT_COUNT=${PLACEMENT_DISTRICT_COUNT}
PLACEMENT_RECORD_COUNT=${PLACEMENT_RECORD_COUNT}
PLACEMENT_UNIQUE_ACGHASH_COUNT=${PLACEMENT_UNIQUE_ACGHASH_COUNT}
PLACEMENT_RUNTIME_AUTHORIZED_COUNT=${PLACEMENT_RUNTIME_AUTHORIZED_COUNT}
LOGINENGINE_UNIT_PATH=${login_unit}
LOGINENGINE_UNIT_SHA256=$(sha256sum "${login_unit}" | awk '{print $1}')
ZONEENGINE_UNIT_PATH=${zone_unit}
ZONEENGINE_UNIT_SHA256=$(sha256sum "${zone_unit}" | awk '{print $1}')
LOGINENGINE_SERVICE=ao-rebirth-loginengine.service
ZONEENGINE_SERVICE=ao-rebirth-zoneengine.service
PREVIOUS_LOGINENGINE_RELEASE=${previous_login_release}
PREVIOUS_ZONEENGINE_RELEASE=${previous_zone_release}
EOF
chmod 0600 "${temporary}"
mv -f -- "${temporary}" "${output}"
trap - EXIT
echo "RELEASE_MANIFEST=${output}"
echo "RELEASE_MANIFEST_SOURCE_SHA=${expected_sha}"
echo "RELEASE_MANIFEST_RESULT=PASS"
