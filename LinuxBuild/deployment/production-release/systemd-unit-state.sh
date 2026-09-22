#!/usr/bin/env bash
# A unit file and a resident systemd runtime object have different lifetimes.
systemd_unit_error() { echo "SYSTEMD_UNIT_ERROR: $*" >&2; return 1; }

systemd_unit_inspect()
{
    local service="$1" unit_file="$2" listed properties key value
    if [[ -L "${unit_file}" ]]; then
        if [[ "$(readlink -- "${unit_file}")" == /dev/null ]]; then
            SYSTEMD_UNIT_CLASS=UNIT_MASKED
        else
            SYSTEMD_UNIT_CLASS=UNIT_INVALID
        fi
        systemd_unit_error "${SYSTEMD_UNIT_CLASS}: ${unit_file}"; return 1
    fi
    [[ -f "${unit_file}" ]] \
        || { SYSTEMD_UNIT_CLASS=UNIT_NOT_FOUND; systemd_unit_error "${SYSTEMD_UNIT_CLASS}: ${unit_file}"; return 1; }
    listed="$(systemctl list-units --all --plain --no-legend --no-pager --full "${service}")" \
        || { systemd_unit_error "cannot inspect resident unit: ${service}"; return 1; }
    SYSTEMD_UNIT_RESIDENT=NO
    if awk -v unit="${service}" '$1 == unit { found=1 } END { exit !found }' <<< "${listed}"; then
        SYSTEMD_UNIT_RESIDENT=YES
    fi
    properties="$(systemctl show --property=LoadState,ActiveState,SubState,UnitFileState,FragmentPath,NRestarts "${service}")" \
        || { systemd_unit_error "cannot load/inspect unit: ${service}"; return 1; }
    SYSTEMD_UNIT_LOAD_STATE= SYSTEMD_UNIT_ACTIVE_STATE= SYSTEMD_UNIT_FILE_STATE=
    SYSTEMD_UNIT_FRAGMENT= SYSTEMD_UNIT_RESTARTS= SYSTEMD_UNIT_SUB_STATE=
    local seen='|'
    while IFS='=' read -r key value; do
        [[ "${seen}" != *"|${key}|"* ]] || return 1
        seen+="${key}|"
        case "${key}" in
            LoadState) SYSTEMD_UNIT_LOAD_STATE="${value}" ;;
            ActiveState) SYSTEMD_UNIT_ACTIVE_STATE="${value}" ;;
            SubState) SYSTEMD_UNIT_SUB_STATE="${value}" ;;
            UnitFileState) SYSTEMD_UNIT_FILE_STATE="${value}" ;;
            FragmentPath) SYSTEMD_UNIT_FRAGMENT="${value}" ;;
            NRestarts) SYSTEMD_UNIT_RESTARTS="${value}" ;;
            *) systemd_unit_error "unexpected unit property: ${key}"; return 1 ;;
        esac
    done <<< "${properties}"
    case "${SYSTEMD_UNIT_LOAD_STATE}" in
        loaded) ;;
        not-found) SYSTEMD_UNIT_CLASS=UNIT_NOT_FOUND ;;
        masked) SYSTEMD_UNIT_CLASS=UNIT_MASKED ;;
        *) SYSTEMD_UNIT_CLASS=UNIT_INVALID ;;
    esac
    [[ "${SYSTEMD_UNIT_LOAD_STATE}" == loaded ]] \
        || { systemd_unit_error "${SYSTEMD_UNIT_CLASS}: unit ${service} LoadState=${SYSTEMD_UNIT_LOAD_STATE}"; return 1; }
    case "${SYSTEMD_UNIT_FILE_STATE}" in
        enabled|enabled-runtime|disabled|static|indirect) ;;
        masked|masked-runtime) SYSTEMD_UNIT_CLASS=UNIT_MASKED; systemd_unit_error "${SYSTEMD_UNIT_CLASS}: ${service}"; return 1 ;;
        *) SYSTEMD_UNIT_CLASS=UNIT_INVALID; systemd_unit_error "${SYSTEMD_UNIT_CLASS}: unit ${service} UnitFileState=${SYSTEMD_UNIT_FILE_STATE}"; return 1 ;;
    esac
    [[ "${SYSTEMD_UNIT_FRAGMENT}" == "${unit_file}" && "${SYSTEMD_UNIT_RESTARTS}" =~ ^[0-9]+$ ]] \
        || { systemd_unit_error "unit fragment/counter is not governed: ${service}"; return 1; }
    systemd-analyze verify "${unit_file}" \
        || { systemd_unit_error "unit definition is invalid: ${service}"; return 1; }
    case "${SYSTEMD_UNIT_ACTIVE_STATE}/${SYSTEMD_UNIT_SUB_STATE}" in
        active/running) SYSTEMD_UNIT_CLASS=UNIT_ACTIVE ;;
        inactive/dead) SYSTEMD_UNIT_CLASS=UNIT_INACTIVE_LOADED ;;
        failed/failed) SYSTEMD_UNIT_CLASS=UNIT_FAILED_LOADED ;;
        *) systemd_unit_error "unsafe transition state: ${service} ${SYSTEMD_UNIT_ACTIVE_STATE}/${SYSTEMD_UNIT_SUB_STATE}"; return 1 ;;
    esac
    if [[ "${SYSTEMD_UNIT_RESIDENT}" == NO ]]; then
        [[ "${SYSTEMD_UNIT_ACTIVE_STATE}" == inactive ]] \
            || { systemd_unit_error "unit changed state during inspection: ${service}"; return 1; }
        SYSTEMD_UNIT_CLASS=UNIT_NOT_LOADED_BUT_UNIT_FILE_EXISTS
    fi
    echo "SYSTEMD_UNIT_STATE service=${service} class=${SYSTEMD_UNIT_CLASS} load=${SYSTEMD_UNIT_LOAD_STATE} active=${SYSTEMD_UNIT_ACTIVE_STATE} fileState=${SYSTEMD_UNIT_FILE_STATE} residentBefore=${SYSTEMD_UNIT_RESIDENT} restarts=${SYSTEMD_UNIT_RESTARTS}"
}

systemd_unit_prepare_start()
{
    local service="$1" unit_file="$2"
    systemd_unit_inspect "${service}" "${unit_file}" || return 1
    [[ "${SYSTEMD_UNIT_ACTIVE_STATE}" != active ]] \
        || { systemd_unit_error "active unit must be stopped before release startup: ${service}"; return 1; }
    if [[ "${SYSTEMD_UNIT_ACTIVE_STATE}" == failed || "${SYSTEMD_UNIT_RESTARTS}" != 0 ]]; then
        # Failed units are retained by the reviewed default CollectMode; a loaded
        # inactive unit with a nonzero restart counter also has state to reset.
        systemctl reset-failed "${service}" \
            || { systemd_unit_error "reset failed for ${service}"; return 1; }
        systemd_unit_inspect "${service}" "${unit_file}" || return 1
        [[ "${SYSTEMD_UNIT_ACTIVE_STATE}" == inactive && "${SYSTEMD_UNIT_RESTARTS}" == 0 ]] \
            || { systemd_unit_error "reset did not establish a clean stopped unit: ${service}"; return 1; }
        echo "SYSTEMD_RESET_DECISION=RESET_FAILURE_OR_COUNTER service=${service}"
    else
        # GetUnitProperties may load a valid inactive unit and systemd may collect
        # it again immediately. start loads it as needed; reset-failed does not.
        echo "SYSTEMD_RESET_DECISION=SKIP_INACTIVE_ZERO_RESTARTS service=${service}"
    fi
    echo "SYSTEMD_UNIT_START_PRECONDITION=PASS service=${service}"
}
