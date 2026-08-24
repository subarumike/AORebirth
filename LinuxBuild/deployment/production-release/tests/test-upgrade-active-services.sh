#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_dir}/../../../.." && pwd)"
upgrader="${repository_root}/LinuxBuild/deployment/production-release/upgrade-active-services.sh"
login_unit_source="${repository_root}/LinuxBuild/deployment/systemd/ao-rebirth-loginengine.service"
zone_unit_source="${repository_root}/LinuxBuild/deployment/systemd/ao-rebirth-zoneengine.service"
fake_sha="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
other_sha="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
tests_run=0

fail() { echo "FAIL: $*" >&2; exit 1; }
require() { "$@" || fail "assertion failed: $*"; }
set_manifest_value()
{
    awk -F= -v key="$2" -v value="$3" '$1 == key { print key "=" value; next } { print }' "$1" > "$1.tmp"
    mv -f -- "$1.tmp" "$1"
}
create_artifact()
{
    mkdir -p -- "$1"
    printf '#!/usr/bin/env bash\nexit 0\n' > "$1/$2"
    chmod 0755 "$1/$2"
    printf '%s\n' "${fake_sha}" > "$1/SOURCE_SHA"
    printf 'COMMIT_SHA=%s\n' "${fake_sha}" > "$1/BUILD_PROVENANCE.env"
    cat > "$1/LINUX_ACCEPTANCE.env" <<EOF
AO_REBIRTH_SOURCE_SHA=${fake_sha}
SOURCE_SHA_MATCH=PASS
TRACKED_SOURCE_CLEAN=PASS
LINUX_ACCEPTANCE=PASS
EOF
}
create_fixture()
{
    fixture="$(mktemp -d)"
    root="${fixture}/root"
    input="${fixture}/input"
    state="${root}/test-state"
    mkdir -p "${root}/opt/ao-rebirth/loginengine/releases/old-login" "${root}/opt/ao-rebirth/zoneengine/releases/old-zone" \
        "${root}/opt/ao-rebirth/deployment-snapshots" "${root}/etc/systemd/system" \
        "${root}/etc/ao-rebirth/loginengine" "${root}/etc/ao-rebirth/zoneengine" "${root}/var/lib" "${state}" "${input}"
    printf 'old-login\n' > "${root}/opt/ao-rebirth/loginengine/releases/old-login/LoginEngine"
    printf 'old-zone\n' > "${root}/opt/ao-rebirth/zoneengine/releases/old-zone/ZoneEngine"
    chmod 0755 "${root}/opt/ao-rebirth/loginengine/releases/old-login/LoginEngine" "${root}/opt/ao-rebirth/zoneengine/releases/old-zone/ZoneEngine"
    prior_login_link_target="releases/old-login"
    prior_zone_link_target="releases/old-zone"
    MSYS=winsymlinks:sys ln -s -- "${prior_login_link_target}" "${root}/opt/ao-rebirth/loginengine/current"
    MSYS=winsymlinks:sys ln -s -- "${prior_zone_link_target}" "${root}/opt/ao-rebirth/zoneengine/current"
    require test -L "${root}/opt/ao-rebirth/loginengine/current"
    require test -L "${root}/opt/ao-rebirth/zoneengine/current"
    require test "$(readlink -- "${root}/opt/ao-rebirth/loginengine/current")" = "${prior_login_link_target}"
    require test "$(readlink -- "${root}/opt/ao-rebirth/zoneengine/current")" = "${prior_zone_link_target}"
    prior_login_release="$(realpath -e -- "${root}/opt/ao-rebirth/loginengine/current")"
    prior_zone_release="$(realpath -e -- "${root}/opt/ao-rebirth/zoneengine/current")"
    printf 'old login unit\n' > "${root}/etc/systemd/system/ao-rebirth-loginengine.service"
    printf 'old zone unit\n' > "${root}/etc/systemd/system/ao-rebirth-zoneengine.service"
    printf 'environment\n' > "${root}/etc/ao-rebirth/loginengine/loginengine.env"
    printf 'environment\n' > "${root}/etc/ao-rebirth/zoneengine/zoneengine.env"
    printf '<Config />\n' > "${root}/etc/ao-rebirth/loginengine/Config.xml"
    printf '<Config />\n' > "${root}/etc/ao-rebirth/zoneengine/Config.xml"
    printf 'active\n' > "${state}/login.active"; printf 'active\n' > "${state}/zone.active"
    printf '0\n' > "${state}/login.restarts"; printf '0\n' > "${state}/zone.restarts"
    printf '0\n' > "${state}/login.starts"; printf '0\n' > "${state}/zone.starts"; printf '0\n' > "${state}/online"
    printf '0\n' > "${state}/login.listener-delay"; printf '0\n' > "${state}/zone.listener-delay"
    printf '0\n' > "${state}/login.listener-checks"; printf '0\n' > "${state}/zone.listener-checks"
    create_artifact "${input}/login" LoginEngine
    create_artifact "${input}/zone" ZoneEngine
    cp -- "${login_unit_source}" "${input}/login.service"
    cp -- "${zone_unit_source}" "${input}/zone.service"
    manifest="${input}/release.manifest"
    cat > "${manifest}" <<EOF
FORMAT=1
SOURCE_SHA=${fake_sha}
BUILD_TIMESTAMP_UTC=2026-08-24T00:00:00Z
LOGINENGINE_ARTIFACT_DIR=${input}/login
LOGINENGINE_ARTIFACT_SHA256=$(sha256sum "${input}/login/LoginEngine" | awk '{print $1}')
ZONEENGINE_ARTIFACT_DIR=${input}/zone
ZONEENGINE_ARTIFACT_SHA256=$(sha256sum "${input}/zone/ZoneEngine" | awk '{print $1}')
LOGINENGINE_UNIT_PATH=${input}/login.service
LOGINENGINE_UNIT_SHA256=$(sha256sum "${input}/login.service" | awk '{print $1}')
ZONEENGINE_UNIT_PATH=${input}/zone.service
ZONEENGINE_UNIT_SHA256=$(sha256sum "${input}/zone.service" | awk '{print $1}')
LOGINENGINE_SERVICE=ao-rebirth-loginengine.service
ZONEENGINE_SERVICE=ao-rebirth-zoneengine.service
PREVIOUS_LOGINENGINE_RELEASE=
PREVIOUS_ZONEENGINE_RELEASE=
EOF
    old_login_unit_hash="$(sha256sum "${root}/etc/systemd/system/ao-rebirth-loginengine.service" | awk '{print $1}')"
    old_zone_unit_hash="$(sha256sum "${root}/etc/systemd/system/ao-rebirth-zoneengine.service" | awk '{print $1}')"
    old_login_artifact_hash="$(sha256sum "${prior_login_release}/LoginEngine" | awk '{print $1}')"
    old_zone_artifact_hash="$(sha256sum "${prior_zone_release}/ZoneEngine" | awk '{print $1}')"
    new_login_unit_hash="$(sha256sum "${input}/login.service" | awk '{print $1}')"
    new_zone_unit_hash="$(sha256sum "${input}/zone.service" | awk '{print $1}')"
    new_login_artifact_hash="$(sha256sum "${input}/login/LoginEngine" | awk '{print $1}')"
    new_zone_artifact_hash="$(sha256sum "${input}/zone/ZoneEngine" | awk '{print $1}')"
    require test "${old_login_unit_hash}" != "${new_login_unit_hash}"
    require test "${old_zone_unit_hash}" != "${new_zone_unit_hash}"
    require test "${old_login_artifact_hash}" != "${new_login_artifact_hash}"
    require test "${old_zone_artifact_hash}" != "${new_zone_artifact_hash}"
}
run_upgrade()
{
    env MSYS=winsymlinks:sys AO_REBIRTH_DEPLOY_TEST_MODE=1 AO_REBIRTH_DEPLOY_TEST_ROOT="${root}" AO_REBIRTH_DEPLOY_TEST_FAIL_STEP="${2:-}" \
        bash "${upgrader}" --manifest "${manifest}" --expected-sha "${1:-${fake_sha}}"
}
assert_old_pair()
{
    require test -L "${root}/opt/ao-rebirth/loginengine/current"
    require test -L "${root}/opt/ao-rebirth/zoneengine/current"
    require test "$(readlink -- "${root}/opt/ao-rebirth/loginengine/current")" = "${prior_login_link_target}"
    require test "$(readlink -- "${root}/opt/ao-rebirth/zoneengine/current")" = "${prior_zone_link_target}"
    require test -d "${prior_login_release}"
    require test -d "${prior_zone_release}"
    require test "$(realpath -e -- "${root}/opt/ao-rebirth/loginengine/current")" = "${prior_login_release}"
    require test "$(realpath -e -- "${root}/opt/ao-rebirth/zoneengine/current")" = "${prior_zone_release}"
    require test "$(sha256sum "${root}/opt/ao-rebirth/loginengine/current/LoginEngine" | awk '{print $1}')" = "${old_login_artifact_hash}"
    require test "$(sha256sum "${root}/opt/ao-rebirth/zoneengine/current/ZoneEngine" | awk '{print $1}')" = "${old_zone_artifact_hash}"
    require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-loginengine.service" | awk '{print $1}')" = "${old_login_unit_hash}"
    require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-zoneengine.service" | awk '{print $1}')" = "${old_zone_unit_hash}"
    require test "$(sha256sum "${root}/opt/ao-rebirth/loginengine/current/LoginEngine" | awk '{print $1}')" != "${new_login_artifact_hash}"
    require test "$(sha256sum "${root}/opt/ao-rebirth/zoneengine/current/ZoneEngine" | awk '{print $1}')" != "${new_zone_artifact_hash}"
    require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-loginengine.service" | awk '{print $1}')" != "${new_login_unit_hash}"
    require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-zoneengine.service" | awk '{print $1}')" != "${new_zone_unit_hash}"
    require test "$(cat "${state}/login.active")" = active
    require test "$(cat "${state}/zone.active")" = active
}
expect_preflight_failure()
{
    if run_upgrade "${1:-${fake_sha}}" > "${fixture}/output" 2>&1; then fail "expected preflight failure"; fi
    assert_old_pair
    tests_run=$((tests_run + 1))
}
expect_transaction_failure()
{
    if run_upgrade "${fake_sha}" "$1" > "${fixture}/output" 2>&1; then fail "expected transaction failure at $1"; fi
    grep -F 'ROLLBACK_BOTH_SERVICES=PASS' "${fixture}/output" >/dev/null || fail "paired rollback did not pass for $1"
    grep -F 'ROLLBACK_EXACT_PRIOR_TARGETS=PASS' "${fixture}/output" >/dev/null || fail "exact prior symlink targets were not restored for $1"
    grep -F 'ROLLBACK_PRIOR_ARTIFACTS_AND_UNITS=PASS' "${fixture}/output" >/dev/null || fail "prior artifacts and units were not restored for $1"
    grep -F 'ROLLBACK_NO_MIXED_STATE=PASS' "${fixture}/output" >/dev/null || fail "rollback left mixed deployment state for $1"
    grep -F 'ROLLBACK_STEP_LOGIN_READINESS=PASS' "${fixture}/output" >/dev/null || fail "LoginEngine rollback readiness did not pass for $1"
    grep -F 'ROLLBACK_STEP_ZONE_READINESS=PASS' "${fixture}/output" >/dev/null || fail "ZoneEngine rollback readiness did not pass for $1"
    assert_old_pair
    tests_run=$((tests_run + 1))
}

case "${AO_REBIRTH_DEPLOY_TEST_CASE:-all}" in
    all) ;;
    success)
        create_fixture
        printf '7\n' > "${state}/login.listener-delay"; printf '7\n' > "${state}/zone.listener-delay"
        run_upgrade > "${fixture}/success-output"
        require grep -F 'TRANSACTIONAL_DEPLOYMENT=PASS' "${fixture}/success-output"
        require grep -F 'READINESS_WAIT=PASS engine=login elapsedSeconds=7' "${fixture}/success-output"
        require grep -F 'READINESS_WAIT=PASS engine=zone elapsedSeconds=7' "${fixture}/success-output"
        echo "PASS: production deployment workflow successful startup (1/1)"
        exit 0
        ;;
    artifact_install|unit_install|login_start|zone_start)
        selected_case="${AO_REBIRTH_DEPLOY_TEST_CASE}"
        create_fixture
        if [[ "${selected_case}" == artifact_install ]]; then
            printf '7\n' > "${state}/login.listener-delay"; printf '7\n' > "${state}/zone.listener-delay"
        fi
        expect_transaction_failure "${selected_case}"
        if [[ "${selected_case}" == artifact_install ]]; then
            require grep -F 'READINESS_WAIT=PASS engine=login elapsedSeconds=7' "${fixture}/output"
            require grep -F 'READINESS_WAIT=PASS engine=zone elapsedSeconds=7' "${fixture}/output"
        fi
        if [[ "${selected_case}" == login_start ]]; then
            require grep -F 'OWNERSHIP_DIR_FIXTURE_SHARED_PATH=PASS' "${fixture}/output"
            require test -d "${root}/var/lib/ao-rebirth/session-ownership"
        fi
        echo "PASS: production deployment workflow ${selected_case} rollback (1/1)"
        exit 0
        ;;
    idempotent)
        create_fixture
        run_upgrade > "${fixture}/success-output"
        require grep -F 'TRANSACTIONAL_DEPLOYMENT=PASS' "${fixture}/success-output"
        login_starts_before="$(cat "${state}/login.starts")"
        zone_starts_before="$(cat "${state}/zone.starts")"
        run_upgrade > "${fixture}/idempotent-output"
        require grep -F 'IDEMPOTENT_REDEPLOY=PASS' "${fixture}/idempotent-output"
        require grep -F 'OWNERSHIP_DIR_FIXTURE_IDEMPOTENT=PASS' "${fixture}/idempotent-output"
        require test "$(cat "${state}/ownership-directory")" = "${root}/var/lib/ao-rebirth/session-ownership"
        require test "$(cat "${state}/login.starts")" = "${login_starts_before}"
        require test "$(cat "${state}/zone.starts")" = "${zone_starts_before}"
        echo "PASS: production deployment workflow idempotent redeploy (1/1)"
        exit 0
        ;;
    *) fail "unknown deployment fixture case: ${AO_REBIRTH_DEPLOY_TEST_CASE}" ;;
esac

behavior_hash_before="$(sha256sum "${repository_root}/AORebirth/Server/LoginEngine/CoreClient/LoginHandoffLifecycle.cs" \
    "${repository_root}/AORebirth/Server/ZoneEngine/StaleOnlineRecovery.cs" \
    "${repository_root}/AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs")"

create_fixture; expect_preflight_failure "${other_sha}"
create_fixture; set_manifest_value "${manifest}" LOGINENGINE_ARTIFACT_SHA256 "$(printf bad | sha256sum | awk '{print $1}')"; expect_preflight_failure
create_fixture; set_manifest_value "${manifest}" ZONEENGINE_ARTIFACT_SHA256 "$(printf bad | sha256sum | awk '{print $1}')"; expect_preflight_failure
create_fixture; rm -f -- "${input}/login.service"; expect_preflight_failure
create_fixture; rm -f -- "${input}/zone.service"; expect_preflight_failure
create_fixture; sed -i 's|AO_REBIRTH_SESSION_OWNERSHIP_DIR=/var/lib/ao-rebirth/session-ownership|AO_REBIRTH_SESSION_OWNERSHIP_DIR=/var/lib/ao-rebirth/zone-private|' "${input}/zone.service"; set_manifest_value "${manifest}" ZONEENGINE_UNIT_SHA256 "$(sha256sum "${input}/zone.service" | awk '{print $1}')"; expect_preflight_failure
create_fixture; sed -i 's|/var/lib/ao-rebirth/session-ownership|/tmp/session-ownership|g' "${input}/login.service" "${input}/zone.service"; set_manifest_value "${manifest}" LOGINENGINE_UNIT_SHA256 "$(sha256sum "${input}/login.service" | awk '{print $1}')"; set_manifest_value "${manifest}" ZONEENGINE_UNIT_SHA256 "$(sha256sum "${input}/zone.service" | awk '{print $1}')"; expect_preflight_failure
create_fixture; sed -i '/^PrivateTmp=true$/d' "${input}/login.service"; set_manifest_value "${manifest}" LOGINENGINE_UNIT_SHA256 "$(sha256sum "${input}/login.service" | awk '{print $1}')"; expect_preflight_failure
create_fixture; sed -i '/ZoneEngine --recover-stale-online/d' "${input}/zone.service"; set_manifest_value "${manifest}" ZONEENGINE_UNIT_SHA256 "$(sha256sum "${input}/zone.service" | awk '{print $1}')"; expect_preflight_failure
create_fixture; printf '7\n' > "${state}/login.listener-delay"; printf '7\n' > "${state}/zone.listener-delay"; expect_transaction_failure artifact_install; require grep -F 'READINESS_WAIT=PASS engine=login elapsedSeconds=7' "${fixture}/output"; require grep -F 'READINESS_WAIT=PASS engine=zone elapsedSeconds=7' "${fixture}/output"
create_fixture; expect_transaction_failure unit_install
create_fixture; expect_transaction_failure login_start
create_fixture; expect_transaction_failure zone_start
create_fixture; expect_transaction_failure listener; require grep -F 'READINESS_WAIT=TIMEOUT engine=login elapsedSeconds=30' "${fixture}/output"; require grep -F 'READINESS_JOURNAL_BEGIN service=ao-rebirth-loginengine.service' "${fixture}/output"; require grep -F 'READINESS_JOURNAL_END service=ao-rebirth-loginengine.service' "${fixture}/output"

create_fixture
printf '7\n' > "${state}/login.listener-delay"; printf '7\n' > "${state}/zone.listener-delay"
run_upgrade > "${fixture}/success-output"
require grep -F 'TRANSACTIONAL_DEPLOYMENT=PASS' "${fixture}/success-output"
require grep -F 'READINESS_WAIT=PASS engine=login elapsedSeconds=7' "${fixture}/success-output"
require grep -F 'READINESS_WAIT=PASS engine=zone elapsedSeconds=7' "${fixture}/success-output"
require test "$(tr -d '\r\n\t ' < "${root}/opt/ao-rebirth/loginengine/current/SOURCE_SHA")" = "${fake_sha}"
require test "$(tr -d '\r\n\t ' < "${root}/opt/ao-rebirth/zoneengine/current/SOURCE_SHA")" = "${fake_sha}"
require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-loginengine.service" | awk '{print $1}')" = "$(sha256sum "${input}/login.service" | awk '{print $1}')"
require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-zoneengine.service" | awk '{print $1}')" = "$(sha256sum "${input}/zone.service" | awk '{print $1}')"
require test -d "${root}/var/lib/ao-rebirth/session-ownership"
tests_run=$((tests_run + 1))

login_starts_before="$(cat "${state}/login.starts")"; zone_starts_before="$(cat "${state}/zone.starts")"
run_upgrade > "${fixture}/idempotent-output"
require grep -F 'IDEMPOTENT_REDEPLOY=PASS' "${fixture}/idempotent-output"
require grep -F 'OWNERSHIP_DIR_FIXTURE_IDEMPOTENT=PASS' "${fixture}/idempotent-output"
require test "$(cat "${state}/ownership-directory")" = "${root}/var/lib/ao-rebirth/session-ownership"
require test "$(cat "${state}/login.starts")" = "${login_starts_before}"
require test "$(cat "${state}/zone.starts")" = "${zone_starts_before}"
tests_run=$((tests_run + 1))

behavior_hash_after="$(sha256sum "${repository_root}/AORebirth/Server/LoginEngine/CoreClient/LoginHandoffLifecycle.cs" \
    "${repository_root}/AORebirth/Server/ZoneEngine/StaleOnlineRecovery.cs" \
    "${repository_root}/AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs")"
require test "${behavior_hash_before}" = "${behavior_hash_after}"
tests_run=$((tests_run + 1))
require test "${tests_run}" = 17
echo "PASS: production deployment workflow tests (17/17)"
