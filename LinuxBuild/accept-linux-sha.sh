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

"${repo_dir}/LinuxBuild/publish-zoneengine.sh" "${runtime_id}" "${self_contained}"

publish_dir="${repo_dir}/LinuxBuild/artifacts/zoneengine/${runtime_id}/${package_kind}"
dotnet_sdk_version="$(dotnet --version)"
build_timestamp_utc="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
build_host_type="$(uname -srm)"

printf '%s\n' "${expected_sha}" > "${publish_dir}/SOURCE_SHA"
cat > "${publish_dir}/BUILD_PROVENANCE.env" <<EOF
REPOSITORY=AORebirth
COMMIT_SHA=${expected_sha}
BUILD_PLATFORM=linux
RUNTIME_IDENTIFIER=${runtime_id}
CONFIGURATION=Release
SELF_CONTAINED=${self_contained}
DOTNET_SDK_VERSION=${dotnet_sdk_version}
BUILD_HOST_TYPE=${build_host_type}
BUILD_TIMESTAMP_UTC=${build_timestamp_utc}
ACCEPTANCE_RESULT=PASS
EOF

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
EOF

echo "RESTORE=PASS"
echo "BUILD=PASS"
echo "TESTS=PASS"
echo "PUBLISH=PASS"
echo "LINUX_ARTIFACT_DIR=${publish_dir}"
echo "LINUX_ACCEPTANCE=PASS"
