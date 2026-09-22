#!/usr/bin/env bash
# Run explicitly inside a disposable systemd container, never on production.
set -euo pipefail
[[ "${AO_REBIRTH_ISOLATED_SYSTEMD_FIXTURE:-}" == YES && -f /run/systemd/container ]] || exit 2
source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)/systemd-unit-state.sh"
service=aorebirth-systemd-fixture.service
unit=/etc/systemd/system/${service}
[[ ! -e "${unit}" ]] || exit 2
cleanup() { systemctl stop "${service}" >/dev/null 2>&1 || true; rm -f -- "${unit}"; systemctl daemon-reload; }
trap cleanup EXIT
write_unit() { printf '[Unit]\nDescription=Disposable AORebirth systemd test\n[Service]\nType=simple\nExecStart=%s\n' "$1" > "${unit}"; systemctl daemon-reload; }
expect_rejected() { if systemd_unit_prepare_start "${service}" "${unit}"; then echo "Unexpected accepted state: $1" >&2; exit 1; fi; }
write_unit '/usr/bin/sleep infinity'
systemctl start "${service}"
systemd_unit_inspect "${service}" "${unit}"
[[ "${SYSTEMD_UNIT_CLASS}" == UNIT_ACTIVE ]]
expect_rejected active
systemctl stop "${service}"
# list-units does not load a unit; show does. Wait only for the bounded GC case.
for attempt in {1..50}; do
    if ! systemctl list-units --all --plain --no-legend "${service}" | grep -Fq "${service}"; then break; fi
    sleep 0.1
done
set +e
failure="$(systemctl reset-failed "${service}" 2>&1)"
failure_status=$?
set -e
printf 'FAILING_COMMAND=systemctl reset-failed %s\nFAILING_EXIT_CODE=%s\nFAILING_STDERR=%s\n' "${service}" "${failure_status}" "${failure}"
[[ "${failure_status}" == 1 && "${failure}" == *'not loaded'* ]]
echo PRIOR_PRODUCTION_FAILURE_REPRODUCED=YES
systemd_unit_prepare_start "${service}" "${unit}"
[[ "${SYSTEMD_UNIT_CLASS}" == UNIT_NOT_LOADED_BUT_UNIT_FILE_EXISTS ]]
systemctl start "${service}"
systemctl is-active --quiet "${service}"
systemctl stop "${service}"
# stop also accepts the unloaded, valid unit that reset-failed rejects.
systemctl stop "${service}"
echo UNLOADED_VALID_UNIT_HANDLED=PASS
write_unit /usr/bin/false
systemctl start "${service}"
for attempt in {1..50}; do systemctl is-failed --quiet "${service}" && break; sleep 0.1; done
systemd_unit_prepare_start "${service}" "${unit}"
[[ "${SYSTEMD_UNIT_ACTIVE_STATE}" == inactive && "${SYSTEMD_UNIT_RESTARTS}" == 0 ]]
echo FAILED_UNIT_RESET=PASS
rm -- "${unit}"
systemctl daemon-reload
expect_rejected missing
echo MISSING_UNIT_REJECTED=PASS
ln -s /dev/null "${unit}"
systemctl daemon-reload
expect_rejected masked
echo MASKED_UNIT_REJECTED=PASS
rm -- "${unit}"
write_unit ''
expect_rejected invalid
echo INVALID_UNIT_REJECTED=PASS
write_unit '/usr/bin/sleep infinity'
systemd_unit_prepare_start "${service}" "${unit}"
systemctl start "${service}"
systemctl is-active --quiet "${service}"
echo PRIOR_PRODUCTION_FAILURE_FIXTURE_AFTER_REPAIR=PASS
