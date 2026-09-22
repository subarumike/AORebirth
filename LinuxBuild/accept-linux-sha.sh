#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_dir}/.." && pwd)"

expected_sha=""
workspace=""
repo_url=""
runtime_id="linux-x64"
self_contained="true"

usage()
{
    echo "usage: LinuxBuild/accept-linux-sha.sh --expected-sha <sha> --workspace <controlled-workspace> [--repo-url <url>] [--runtime linux-x64|linux-arm64] [--self-contained true|false]" >&2
}

fail()
{
    echo "$*" >&2
    echo "LINUX_ACCEPTANCE=FAIL"
    exit 1
}

while [[ "$#" -gt 0 ]]; do
    case "$1" in
        --expected-sha)
            expected_sha="${2:-}"
            shift 2
            ;;
        --workspace)
            workspace="${2:-}"
            shift 2
            ;;
        --repo-url)
            repo_url="${2:-}"
            shift 2
            ;;
        --runtime)
            runtime_id="${2:-}"
            shift 2
            ;;
        --self-contained)
            self_contained="${2:-}"
            shift 2
            ;;
        --help)
            usage
            exit 0
            ;;
        *)
            usage
            exit 2
            ;;
    esac
done

[[ "${expected_sha}" =~ ^[0-9a-fA-F]{40}$ ]] || fail "SOURCE_SHA_MISMATCH invalid expected SHA"
expected_sha="${expected_sha,,}"
[[ -n "${workspace}" ]] || fail "LINUX_ACCEPTANCE_WORKSPACE_MISSING"
case "${runtime_id}" in
    linux-x64|linux-arm64) ;;
    *) fail "LINUX_RUNTIME_INVALID" ;;
esac
case "${self_contained}" in
    true) package_kind="self-contained" ;;
    false) package_kind="framework-dependent" ;;
    *) fail "LINUX_SELF_CONTAINED_INVALID" ;;
esac

if [[ -z "${repo_url}" ]]; then
    repo_url="$(git -C "${repository_root}" config --get remote.origin.url || true)"
fi
[[ -n "${repo_url}" ]] || fail "LINUX_REPOSITORY_URL_MISSING"
command -v python3 >/dev/null || fail "LINUX_PACKAGE_TEST_PREREQUISITE_MISSING python3"

workspace="$(mkdir -p -- "${workspace}" && cd -- "${workspace}" && pwd)"
sentinel="${workspace}/.ao-rebirth-linux-acceptance-workspace"
repo_dir="${workspace}/repo"

if [[ ! -e "${sentinel}" ]]; then
    if find "${workspace}" -mindepth 1 -maxdepth 1 | grep -q .; then
        fail "LINUX_ACCEPTANCE_WORKSPACE_NOT_EMPTY_NO_SENTINEL"
    fi
    : > "${sentinel}"
fi

if [[ ! -d "${repo_dir}/.git" ]]; then
    git clone --no-checkout "${repo_url}" "${repo_dir}"
fi

# Validate input location before cleanup, so a mistakenly supplied archive inside
# the checkout is rejected rather than deleted by the governed clean operation.
[[ -n "${AO_REBIRTH_PLAYFIELD_PACKAGE_ARCHIVE:-}" ]] || fail "PLAYFIELD_PACKAGE_ARCHIVE_REQUIRED"
[[ -f "${AO_REBIRTH_PLAYFIELD_PACKAGE_ARCHIVE}" && ! -L "${AO_REBIRTH_PLAYFIELD_PACKAGE_ARCHIVE}" ]] \
    || fail "PLAYFIELD_PACKAGE_ARCHIVE_INVALID"
playfield_package_archive="$(realpath -e -- "${AO_REBIRTH_PLAYFIELD_PACKAGE_ARCHIVE}")"
case "${playfield_package_archive}" in
    "${repo_dir}"|"${repo_dir}"/*) fail "PLAYFIELD_PACKAGE_ARCHIVE_MUST_BE_OUTSIDE_CHECKOUT" ;;
esac

git -C "${repo_dir}" fetch origin
git -C "${repo_dir}" checkout --detach "${expected_sha}"
git -C "${repo_dir}" reset --hard "${expected_sha}"
git -C "${repo_dir}" clean -ffdx

actual_sha="$(git -C "${repo_dir}" rev-parse HEAD)"
echo "AO_REBIRTH_SOURCE_SHA=${actual_sha}"
echo "EXPECTED_SOURCE_SHA=${expected_sha}"
if [[ "${actual_sha}" != "${expected_sha}" ]]; then
    echo "SOURCE_SHA_MATCH=FAIL"
    echo "LINUX_ACCEPTANCE=FAIL"
    exit 10
fi
echo "SOURCE_SHA_MATCH=PASS"

if [[ -n "$(git -C "${repo_dir}" status --porcelain --untracked-files=no)" ]]; then
    echo "TRACKED_SOURCE_CLEAN=FAIL"
    echo "LINUX_ACCEPTANCE=FAIL"
    exit 11
fi
echo "TRACKED_SOURCE_CLEAN=PASS"

# Bind acceptance policy to the exact detached source revision, never to the
# caller checkout that launched this wrapper.
source "${repo_dir}/LinuxBuild/placement-provenance.sh"
source "${repo_dir}/LinuxBuild/content-provenance.sh"

python3 "${repo_dir}/LinuxBuild/Tools/linux_drift_audit.py" \
    --repository-root "${repo_dir}" --expected-sha "${expected_sha}"
python3 "${repo_dir}/LinuxBuild/Tools/test_linux_source_identity.py"
python3 "${repo_dir}/LinuxBuild/Tools/test_placement_parity.py"

# NewEngine consumes editable data. The safe importer verifies the complete
# archive and rejects path escapes/symlinks/overwrites; final bytes are inventoried.
# Historical artifact/recovery validation remains a separate offline regression gate.
python3 "${repo_dir}/LinuxBuild/Tools/test_content_provenance.py"
python3 "${repo_dir}/LinuxBuild/Tools/test_engine_retirement.py"
python3 "${repo_dir}/LinuxBuild/Tools/import_editable_playfields.py" \
    "${playfield_package_archive}" "${repo_dir}/AORebirth/GameData/Playfields" \
    > "${workspace}/EDITABLE_PLAYFIELD_IMPORT.json"
"${repo_dir}/LinuxBuild/build-linux.sh"
dotnet test "${repo_dir}/AORebirth/Server/ZoneEngine_New.Tests/ZoneEngine_New.Tests.csproj" --configuration Release --nologo
"${repo_dir}/LinuxBuild/publish-chatengine.sh" "${runtime_id}" "${self_contained}"
"${repo_dir}/LinuxBuild/publish-loginengine.sh" "${runtime_id}" "${self_contained}"
"${repo_dir}/LinuxBuild/publish-zoneengine.sh" "${runtime_id}" "${self_contained}"
bash "${repo_dir}/LinuxBuild/deployment/production-release/tests/test-upgrade-active-services.sh"
bash "${repo_dir}/LinuxBuild/deployment/zone-stage9/test-artifact-provenance.sh"

login_publish_dir="${repo_dir}/LinuxBuild/artifacts/loginengine/${runtime_id}/${package_kind}"
zone_publish_dir="${repo_dir}/LinuxBuild/artifacts/zoneengine/${runtime_id}/${package_kind}"
# No fresh Legacy executable is built; archived recovery behavior is unchanged.
content_provenance_load "${zone_publish_dir}" "${expected_sha}" linux || fail "CONTENT_VALIDATION_FAILED"
content_require_build_provenance "${zone_publish_dir}/BUILD_PROVENANCE.env" || fail "CONTENT_BUILD_PROVENANCE_INVALID"
dotnet_sdk_version="$(dotnet --version)"
[[ -z "$(git -C "${repo_dir}" status --porcelain --untracked-files=no)" ]] || fail "TRACKED_SOURCE_CHANGED_DURING_ACCEPTANCE"
python3 "${repo_dir}/LinuxBuild/Tools/linux_drift_audit.py" \
    --repository-root "${repo_dir}" --expected-sha "${expected_sha}"
build_timestamp_utc="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
build_host_type="$(uname -srm)"

write_accepted_provenance()
{
    local publish_dir="$1"
    local include_content="$2"
    printf '%s\n' "${expected_sha}" > "${publish_dir}/SOURCE_SHA"
    cat > "${publish_dir}/BUILD_PROVENANCE.env" <<EOF
REPOSITORY=AORebirth
COMMIT_SHA=${expected_sha}
PUBLIC_MASTER_SHA=${expected_sha}
LINUX_SOURCE_PATCH_LAYER=NONE
BUILD_PLATFORM=linux
RUNTIME_IDENTIFIER=${runtime_id}
CONFIGURATION=Release
SELF_CONTAINED=${self_contained}
DOTNET_SDK_VERSION=${dotnet_sdk_version}
BUILD_HOST_TYPE=${build_host_type}
BUILD_TIMESTAMP_UTC=${build_timestamp_utc}
ACCEPTANCE_RESULT=PASS
EOF
    if [[ "${include_content}" == "true" ]]; then
        printf '%s\n' 'ZONEENGINE_IMPLEMENTATION=new' >> "${publish_dir}/BUILD_PROVENANCE.env"
        content_append_build_provenance "${publish_dir}/BUILD_PROVENANCE.env"
    fi

    cat > "${publish_dir}/LINUX_ACCEPTANCE.env" <<EOF
AO_REBIRTH_SOURCE_SHA=${expected_sha}
EXPECTED_SOURCE_SHA=${expected_sha}
SOURCE_SHA_MATCH=PASS
TRACKED_SOURCE_CLEAN=PASS
RESTORE=PASS
BUILD=PASS
TESTS=PASS
PUBLISH=PASS
RUNTIME_IDENTIFIER=${runtime_id}
SELF_CONTAINED=${self_contained}
LINUX_ACCEPTANCE=PASS
ZONEENGINE_DEFAULT=ZoneEngine_New
EOF
    if [[ "${include_content}" == "true" ]]; then
        cat >> "${publish_dir}/LINUX_ACCEPTANCE.env" <<EOF
CONTENT_VALIDATION=PASS
CONTENT_ARCHITECTURE_GUARD=PASS
CONTENT_MANIFEST_SHA256=${CONTENT_MANIFEST_SHA256}
EOF
    fi
}

write_accepted_provenance "${login_publish_dir}" false
write_accepted_provenance "${zone_publish_dir}" true

python3 "${repo_dir}/LinuxBuild/deployment/production-release/tests/test-package-release.py"
echo "RELEASE_PACKAGE_REGRESSIONS=PASS"

release_manifest="${repo_dir}/LinuxBuild/artifacts/production-release/release.manifest"
bash "${repo_dir}/LinuxBuild/deployment/production-release/create-release-manifest.sh" \
    --expected-sha "${expected_sha}" \
    --login-artifact-dir "${login_publish_dir}" \
    --zone-artifact-dir "${zone_publish_dir}" \
    --login-unit "${repo_dir}/LinuxBuild/deployment/systemd/ao-rebirth-loginengine.service" \
    --zone-unit "${repo_dir}/LinuxBuild/deployment/systemd/ao-rebirth-zoneengine.service" \
    --output "${release_manifest}"

echo "RESTORE=PASS"
echo "BUILD=PASS"
echo "TESTS=PASS"
echo "PUBLISH=PASS"
echo "LINUX_LOGINENGINE_ARTIFACT_DIR=${login_publish_dir}"
echo "LINUX_ZONEENGINE_ARTIFACT_DIR=${zone_publish_dir}"
echo "LINUX_ARTIFACT_DIR=${zone_publish_dir}"
echo "LINUX_RELEASE_MANIFEST=${release_manifest}"
echo "LINUX_CONTENT_MANIFEST_SHA256=${CONTENT_MANIFEST_SHA256}"
echo "LINUX_CONTENT_FILE_COUNT=${CONTENT_FILE_COUNT}"
echo "LINUX_CONTENT_TOTAL_BYTES=${CONTENT_TOTAL_BYTES}"
echo "LINUX_DEFAULT_ZONEENGINE=ZoneEngine_New"
echo "LINUX_ACCEPTANCE=PASS"
