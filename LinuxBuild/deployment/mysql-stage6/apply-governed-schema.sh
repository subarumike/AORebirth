#!/usr/bin/env bash
set -euo pipefail

readonly CONTAINER_NAME="aorebirth-chatengine-mysql-stage6"
readonly DATABASE_NAME="aorebirth_chatengine_stage6"
readonly DATABASE_USER="aorebirth_stage6"
readonly NETWORK_NAME="aorebirth_chatengine_stage6_internal"
readonly SECRET_DIRECTORY="/etc/ao-rebirth/chatengine/stage6"
readonly MYSQL_ENVIRONMENT="${SECRET_DIRECTORY}/mysql.env"
readonly DISPOSABLE_LABEL="org.aorebirth.purpose=chatengine-stage6-disposable"
readonly MIGRATION_CONTRACT_FILE="charactersactivenanos_alter.sql"

readonly SCHEMA_FILES=(
    characterstimers.sql
    characters.sql
    charactersactivenanos.sql
    charactersmeshs.sql
    charactersuploadednanos.sql
    charactersperks.sql
    instanceditems.sql
    itemnames.sql
    items.sql
    login.sql
    missionaccountflags.sql
    missionflags.sql
    missionobjectiveobservations.sql
    missionobjectiveprogress.sql
    missionrewardledger.sql
    missionstates.sql
    mobdroptable.sql
    mobspawns.sql
    mobspawnsactivenanos.sql
    mobspawnsinventory.sql
    mobspawnsmeshs.sql
    mobspawnsuploadednanos.sql
    mobspawns_stats.sql
    mobtemplate.sql
    organizations.sql
    proxydestinations.sql
    receivedmessages.sql
    shopinventorytemplates.sql
    staticdynels.sql
    stats.sql
    teleports.sql
    tradeskill.sql
    vendors.sql
    vendortemplate.sql
)
readonly GOVERNED_SQL_FILES=("${SCHEMA_FILES[@]}" "${MIGRATION_CONTRACT_FILE}")

fail()
{
    echo "REFUSED: $*" >&2
    exit 1
}

verify_root_owned_not_writable()
{
    local target_path="$1"
    local target_owner
    local target_mode

    target_owner="$(stat -c '%U' -- "${target_path}")"
    target_mode="$(stat -c '%a' -- "${target_path}")"
    [[ "${target_owner}" == "root" ]] || fail "schema path is not root-owned"
    (( (8#${target_mode} & 0022) == 0 )) \
        || fail "schema path is group- or world-writable"
}

if [[ "${EUID}" -ne 0 ]]; then
    fail "run as root"
fi

if [[ "$#" -gt 1 ]]; then
    fail "usage: apply-governed-schema.sh [exact-sql-directory]"
fi

schema_directory="${1:-/opt/ao-rebirth/chatengine/current/SqlTables}"
schema_directory="$(realpath -e -- "${schema_directory}")"
[[ -d "${schema_directory}" ]] || fail "schema directory is unavailable"
verify_root_owned_not_writable "${schema_directory}"

[[ -f "${MYSQL_ENVIRONMENT}" ]] || fail "root-only MySQL environment is missing"
[[ "$(stat -c '%U:%G:%a' "${MYSQL_ENVIRONMENT}")" == "root:root:600" ]] \
    || fail "unexpected MySQL environment ownership or mode"

container_label="$(docker inspect --format '{{index .Config.Labels "org.aorebirth.purpose"}}' "${CONTAINER_NAME}" 2>/dev/null || true)"
[[ "${container_label}" == "chatengine-stage6-disposable" ]] \
    || fail "the exact labeled disposable container is unavailable"

attached_networks="$(docker inspect --format '{{range $name, $_ := .NetworkSettings.Networks}}{{$name}}{{"\n"}}{{end}}' "${CONTAINER_NAME}")"
[[ "${attached_networks}" == "${NETWORK_NAME}" ]] \
    || fail "unexpected disposable database network attachment"

health_status="$(docker inspect --format '{{.State.Health.Status}}' "${CONTAINER_NAME}")"
[[ "${health_status}" == "healthy" ]] || fail "disposable MySQL is not healthy"

set -a
# shellcheck disable=SC1090
source "${MYSQL_ENVIRONMENT}"
set +a

[[ "${MYSQL_DATABASE:-}" == "${DATABASE_NAME}" ]] || fail "unexpected database identity in secret file"
[[ "${MYSQL_USER:-}" == "${DATABASE_USER}" ]] || fail "unexpected database user in secret file"
[[ -n "${MYSQL_ROOT_PASSWORD:-}" && -n "${MYSQL_PASSWORD:-}" ]] \
    || fail "database credentials are incomplete"

mapfile -t actual_sql_files < <(
    find "${schema_directory}" -maxdepth 1 -name '*.sql' -printf '%f\n' | LC_ALL=C sort
)
mapfile -t expected_sql_files < <(printf '%s\n' "${GOVERNED_SQL_FILES[@]}" | LC_ALL=C sort)

[[ "${#actual_sql_files[@]}" -eq "${#expected_sql_files[@]}" ]] \
    || fail "schema directory does not contain exactly 35 governed SQL files"

for index in "${!expected_sql_files[@]}"; do
    [[ "${actual_sql_files[index]}" == "${expected_sql_files[index]}" ]] \
        || fail "schema filename or case mismatch"
done

for schema_file in "${GOVERNED_SQL_FILES[@]}"; do
    schema_path="${schema_directory}/${schema_file}"
    [[ -f "${schema_path}" && ! -L "${schema_path}" ]] \
        || fail "schema entry is not a regular non-symlink file: ${schema_file}"
    verify_root_owned_not_writable "${schema_path}"
done

mysql_root_query()
{
    docker exec "${CONTAINER_NAME}" sh -c \
        'MYSQL_PWD="${MYSQL_ROOT_PASSWORD}" exec mysql --batch --skip-column-names --protocol=TCP -h 127.0.0.1 -uroot "${MYSQL_DATABASE}" -e "$1"' \
        sh "$1"
}

active_database="$(mysql_root_query 'SELECT DATABASE()')"
[[ "${active_database}" == "${DATABASE_NAME}" ]] || fail "active database identity mismatch"

existing_table_count="$(mysql_root_query "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${DATABASE_NAME}' AND table_type='BASE TABLE'")"
[[ "${existing_table_count}" == "0" ]] \
    || fail "bootstrap is one-shot and requires a brand-new empty database"

migration_fixture="__aorebirth_active_nano_migration_contract"
migration_fixture_created=false
cleanup_migration_fixture()
{
    if [[ "${migration_fixture_created}" == true ]]; then
        mysql_root_query "DROP TABLE IF EXISTS \`${migration_fixture}\`" >/dev/null || true
    fi
}
trap cleanup_migration_fixture EXIT

[[ "$(grep -Fo '`charactersactivenanos`' "${schema_directory}/${MIGRATION_CONTRACT_FILE}" | wc -l | tr -d ' ')" == 1 ]] \
    || fail "active-nano migration target contract changed"
mysql_root_query \
    "CREATE TABLE \`${migration_fixture}\` (\`Id\` int(32) NOT NULL AUTO_INCREMENT, \`CharacterId\` int(32) NOT NULL, \`NanoId\` int(32) unsigned NOT NULL, \`Strain\` int(32) unsigned NOT NULL, PRIMARY KEY (\`Id\`)) ENGINE=InnoDB DEFAULT CHARSET=latin1" \
    >/dev/null
migration_fixture_created=true
mysql_root_query \
    "INSERT INTO \`${migration_fixture}\` (Id, CharacterId, NanoId, Strain) VALUES (77, 53, 123456, 789)" \
    >/dev/null
sed 's/`charactersactivenanos`/`__aorebirth_active_nano_migration_contract`/g' \
    "${schema_directory}/${MIGRATION_CONTRACT_FILE}" \
    | docker exec --interactive "${CONTAINER_NAME}" sh -c \
        'MYSQL_PWD="${MYSQL_ROOT_PASSWORD}" exec mysql --protocol=TCP -h 127.0.0.1 -uroot "${MYSQL_DATABASE}"'

migration_fixture_columns="$(mysql_root_query "SELECT GROUP_CONCAT(CONCAT_WS('|', column_name, data_type, column_type, is_nullable, COALESCE(column_default, '<NULL>'), extra, generation_expression, ordinal_position) ORDER BY ordinal_position SEPARATOR ';') FROM information_schema.columns WHERE table_schema='${DATABASE_NAME}' AND table_name='${migration_fixture}' AND ordinal_position >= 5")"
[[ "${migration_fixture_columns}" == "NanoInstance|int|int|NO|0|||5;DurationCentiseconds|int|int|NO|0|||6;ExpiresAtUtcTicks|bigint|bigint|NO|0|||7" ]] \
    || fail "active-nano forward migration column contract mismatch"
migration_fixture_row="$(mysql_root_query "SELECT CONCAT_WS('|', Id, CharacterId, NanoId, Strain, NanoInstance, DurationCentiseconds, ExpiresAtUtcTicks) FROM \`${migration_fixture}\`")"
[[ "${migration_fixture_row}" == "77|53|123456|789|0|0|0" ]] \
    || fail "active-nano forward migration did not preserve the legacy sentinel row"
migration_fixture_index="$(mysql_root_query "SELECT GROUP_CONCAT(CONCAT_WS('|', index_name, non_unique, seq_in_index, column_name) ORDER BY index_name, seq_in_index SEPARATOR ';') FROM information_schema.statistics WHERE table_schema='${DATABASE_NAME}' AND table_name='${migration_fixture}'")"
[[ "${migration_fixture_index}" == "PRIMARY|0|1|Id" ]] \
    || fail "active-nano forward migration index contract mismatch"
mysql_root_query "DROP TABLE \`${migration_fixture}\`" >/dev/null
migration_fixture_created=false
trap - EXIT
[[ "$(mysql_root_query "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${DATABASE_NAME}' AND table_type='BASE TABLE'")" == 0 ]] \
    || fail "active-nano migration fixture cleanup failed"
echo "ACTIVE_NANO_FORWARD_MIGRATION_CONTRACT=PASS sentinelRows=1 defaults=0,0,0"

for schema_file in "${SCHEMA_FILES[@]}"; do
    echo "Applying ${schema_file}"
    docker exec \
        --interactive \
        "${CONTAINER_NAME}" sh -c \
        'MYSQL_PWD="${MYSQL_ROOT_PASSWORD}" exec mysql --protocol=TCP -h 127.0.0.1 -uroot "${MYSQL_DATABASE}"' \
        < "${schema_directory}/${schema_file}"
done

mapfile -t actual_tables < <(
    mysql_root_query "SELECT table_name FROM information_schema.tables WHERE table_schema='${DATABASE_NAME}' AND table_type='BASE TABLE' ORDER BY table_name"
)
mapfile -t expected_tables < <(
    printf '%s\n' "${SCHEMA_FILES[@]%.sql}" | LC_ALL=C sort
)

[[ "${#actual_tables[@]}" -eq "${#expected_tables[@]}" ]] \
    || fail "import did not create exactly 34 base tables"

for index in "${!expected_tables[@]}"; do
    [[ "${actual_tables[index]}" == "${expected_tables[index]}" ]] \
        || fail "imported table set mismatch"
done

online_column_count="$(mysql_root_query "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='${DATABASE_NAME}' AND table_name='characters' AND column_name='Online'")"
[[ "${online_column_count}" == "1" ]] || fail "characters.Online schema contract mismatch"

require_column_contract()
{
    local table_name="$1"
    local column_name="$2"
    local data_type="$3"
    local column_type="$4"
    local nullable="$5"
    local default_value="$6"
    local extra="$7"
    local ordinal_position="$8"
    local actual
    actual="$(mysql_root_query "SELECT CONCAT(data_type, '|', column_type, '|', is_nullable, '|', COALESCE(column_default, '<NULL>'), '|', extra, '|', generation_expression, '|', ordinal_position) FROM information_schema.columns WHERE table_schema='${DATABASE_NAME}' AND table_name='${table_name}' AND column_name='${column_name}'")"
    [[ "${actual}" == "${data_type}|${column_type}|${nullable}|${default_value}|${extra}||${ordinal_position}" ]] \
        || fail "${table_name}.${column_name} schema contract mismatch"
}

require_column_contract charactersactivenanos Id int int NO '<NULL>' auto_increment 1
require_column_contract charactersactivenanos CharacterId int int NO '<NULL>' '' 2
require_column_contract charactersactivenanos NanoId int 'int unsigned' NO '<NULL>' '' 3
require_column_contract charactersactivenanos Strain int 'int unsigned' NO '<NULL>' '' 4
require_column_contract charactersactivenanos NanoInstance int int NO 0 '' 5
require_column_contract charactersactivenanos DurationCentiseconds int int NO 0 '' 6
require_column_contract charactersactivenanos ExpiresAtUtcTicks bigint bigint NO 0 '' 7

active_nano_column_count="$(mysql_root_query "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='${DATABASE_NAME}' AND table_name='charactersactivenanos'")"
[[ "${active_nano_column_count}" == "7" ]] \
    || fail "charactersactivenanos column-count contract mismatch"
active_nano_index_contract="$(mysql_root_query "SELECT GROUP_CONCAT(CONCAT_WS('|', index_name, non_unique, seq_in_index, column_name) ORDER BY index_name, seq_in_index SEPARATOR ';') FROM information_schema.statistics WHERE table_schema='${DATABASE_NAME}' AND table_name='charactersactivenanos'")"
[[ "${active_nano_index_contract}" == "PRIMARY|0|1|Id" ]] \
    || fail "charactersactivenanos index contract mismatch"
active_nano_table_contract="$(mysql_root_query "SELECT CONCAT(engine, '|', table_collation) FROM information_schema.tables WHERE table_schema='${DATABASE_NAME}' AND table_name='charactersactivenanos' AND table_type='BASE TABLE'")"
[[ "${active_nano_table_contract}" == "InnoDB|latin1_swedish_ci" ]] \
    || fail "charactersactivenanos table contract mismatch"

online_character_count="$(mysql_root_query 'SELECT COUNT(*) FROM characters WHERE Online <> 0')"
[[ "${online_character_count}" == "0" ]] || fail "fresh database contains online characters"

for empty_table in login characters stats organizations receivedmessages; do
    row_count="$(mysql_root_query "SELECT COUNT(*) FROM \`${empty_table}\`")"
    [[ "${row_count}" == "0" ]] || fail "fresh mutable table is not empty: ${empty_table}"
done

for table_name in "${expected_tables[@]}"; do
    mysql_root_query "SELECT 1 FROM \`${table_name}\` LIMIT 0" >/dev/null
done

mysql_root_query \
    "REVOKE ALL PRIVILEGES, GRANT OPTION FROM '${DATABASE_USER}'@'%'; GRANT SELECT, INSERT, UPDATE, DELETE ON \`${DATABASE_NAME}\`.* TO '${DATABASE_USER}'@'%'; FLUSH PRIVILEGES;" \
    >/dev/null

app_database="$(
    docker exec "${CONTAINER_NAME}" sh -c \
        'MYSQL_PWD="${MYSQL_PASSWORD}" exec mysql --batch --skip-column-names --protocol=TCP -h 127.0.0.1 -u"${MYSQL_USER}" "${MYSQL_DATABASE}" -e "SELECT DATABASE()"'
)"
[[ "${app_database}" == "${DATABASE_NAME}" ]] || fail "runtime account database identity mismatch"

echo "PASS: exact governed 35-artifact/34-table ChatEngine schema loaded; runtime account is restricted to row-level CRUD."
