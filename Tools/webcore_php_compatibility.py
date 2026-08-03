#!/usr/bin/env python3
"""Deterministic, offline CellAO WebCore PHP compatibility patching.

The tool never downloads or executes WebCore or PHP. Production patch inputs are
bound to the checked-in base manifest, exact upstream commit, and per-file
SHA-256 values. Patches are non-fuzzy and output is verified against a complete
7,140-entry patched manifest.
"""

import argparse
import hashlib
import json
import os
import re
import shutil
import sys
import tempfile
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Dict, Iterable, List, Mapping, Optional, Sequence, Tuple
from xml.sax.saxutils import quoteattr


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
CONFIG_ROOT = REPOSITORY_ROOT / "AORebirth" / "Config"
BASE_MANIFEST_PATH = CONFIG_ROOT / "WebCoreAssets.manifest.xml"
COMPATIBILITY_MANIFEST_PATH = CONFIG_ROOT / "WebCoreCompatibility.manifest.xml"
PATCHED_MANIFEST_PATH = CONFIG_ROOT / "WebCorePatchedAssets.manifest.xml"
INVENTORY_PATH = REPOSITORY_ROOT / "docs" / "generated" / "webcore_php_compatibility_inventory.json"

UPSTREAM_COMMIT = "765c3850767b63af1cd259bab7f2f7ca3e97adf9"
BASE_MANIFEST_SHA256 = "85c1515d274c2e4051013e89ca6d2a355365d5d01df7d621cc060dfa84e38463"
PATCH_SET_ID = "cellao-webcore-php85-compatibility-v1"
PATCH_SET_VERSION = "1"
EXPECTED_BASE_FILE_COUNT = 7140


class CompatibilityError(RuntimeError):
    pass


@dataclass(frozen=True)
class FileEntry:
    path: str
    size: int
    sha256: str


@dataclass(frozen=True)
class PatchDefinition:
    operation_id: str
    path: str
    input_sha256: str
    transform: Callable[[bytes], bytes]


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            block = handle.read(1024 * 1024)
            if not block:
                break
            digest.update(block)
    return digest.hexdigest()


def _decode_php(data: bytes, path: str) -> str:
    try:
        return data.decode("utf-8")
    except UnicodeDecodeError as error:
        raise CompatibilityError("patch input is not UTF-8: {0}: {1}".format(path, error))


def _newline(text: str) -> str:
    return "\r\n" if "\r\n" in text else "\n"


def _replace_exact(text: str, old: str, new: str, expected_count: int, operation_id: str) -> str:
    count = text.count(old)
    if count != expected_count:
        raise CompatibilityError(
            "{0}: exact replacement count mismatch for {1!r}: expected {2}, got {3}".format(
                operation_id, old, expected_count, count
            )
        )
    return text.replace(old, new)


def _replace_line_range(text: str, start_line: int, end_line: int, replacement: str, operation_id: str) -> str:
    lines = text.splitlines(keepends=True)
    if start_line < 1 or end_line < start_line or end_line > len(lines):
        raise CompatibilityError(
            "{0}: invalid line range {1}-{2} for {3} lines".format(
                operation_id, start_line, end_line, len(lines)
            )
        )
    newline = _newline(text)
    normalized = replacement.replace("\r\n", "\n").replace("\r", "\n")
    replacement_lines = normalized.split("\n")
    if replacement_lines and replacement_lines[-1] == "":
        replacement_lines.pop()
    encoded_lines = [line + newline for line in replacement_lines]
    lines[start_line - 1 : end_line] = encoded_lines
    return "".join(lines)


def _patch_engine(data: bytes) -> bytes:
    operation_id = "engine-pdo-and-random-bytes-v1"
    text = _decode_php(data, "engine.php")
    replacement = """function CreateAccount($username, $password, $charsallowed, $expansion, $email)
{
\t$passhash = create_hash($password);
\t$connection = webcore_db();
\t$statement = $connection->prepare(\"INSERT INTO `login` (`CreationDate`, `Email`, `Username`, `Password`, `AllowedCharacters`, `Flags`, `AccountFlags`, `Expansions`, `GM`, `FirstName`, `LastName`) VALUES (NOW(), :email, :username, :password, :allowedCharacters, 0, 0, :expansions, 0, '', '')\");
\treturn $statement->execute(array(
\t\t':email' => $email,
\t\t':username' => $username,
\t\t':password' => $passhash,
\t\t':allowedCharacters' => $charsallowed,
\t\t':expansions' => $expansion
\t));
}
"""
    text = _replace_line_range(text, 14, 25, replacement, operation_id)
    text = _replace_exact(
        text,
        "mcrypt_create_iv(PBKDF2_SALT_BYTES, MCRYPT_DEV_URANDOM)",
        "random_bytes(PBKDF2_SALT_BYTES)",
        1,
        operation_id,
    )
    return text.encode("utf-8")


def _patch_process_login(data: bytes) -> bytes:
    operation_id = "process-login-pdo-v1"
    text = _decode_php(data, "process-login.php")
    replacements = (
        (
            85,
            91,
            """\tfunction loginFailed($errorMessages){
\t\t$errorText = \"\";
\t\tforeach ($errorMessages as $error) {
\t\t\t$errorText .= $error . \"<br />\";
\t\t}
\t\theader(\"Location: register.php?err=\" . rawurlencode($errorText));
\t\texit();
\t}
""",
        ),
        (
            78,
            80,
            """\t\t}else {
\t\t\tloginFailed(array('Failed to log you in.'));
\t\t}
""",
        ),
        (
            60,
            62,
            """\t\t\tif(!validate_password($password, $passhash)){
\t\t\t\tloginFailed(array('Failed to log you in.'));
\t\t\t}
\t\t\tsession_regenerate_id(true);
""",
        ),
        (
            53,
            58,
            """\tif($result) {
\t\t$member = $result->fetch(PDO::FETCH_ASSOC);
\t\tif($member !== false) {
\t\t\t//Login Successful
\t\t\t$passhash = $member['Password'];
""",
        ),
        (
            48,
            50,
            """\t//Use the shared PDO connection and preserve the historical session field names.
\t$result = $pdo->prepare(\"SELECT `Id`, `CreationDate`, `Email`, `Username`, `Password`, `AllowedCharacters` AS `Allowed_Characters`, `Flags`, `AccountFlags`, `Expansions`, `GM`, `FirstName`, `LastName` FROM `login` WHERE `Username` = :login\");
\t$result->execute(array(':login' => $login));
""",
        ),
        (
            32,
            33,
            """\tisset($_POST['login']) ? $login = trim($_POST['login']) : $login = '';
\tisset($_POST['password']) ? $password = (string)$_POST['password'] : $password = '';
""",
        ),
        (22, 29, ""),
        (
            10,
            20,
            """\t//Use the validated PDO boundary created by includes/config.php.
\t$pdo = webcore_db();
""",
        ),
    )
    for start_line, end_line, replacement in replacements:
        text = _replace_line_range(text, start_line, end_line, replacement, operation_id)
    text = _replace_exact(
        text,
        "\t//TODO: Change MySQL to PDO based queries.",
        "\t//PDO compatibility is provided by the shared WebCore database boundary.",
        1,
        operation_id,
    )
    return text.encode("utf-8")


def _patch_config(data: bytes) -> bytes:
    operation_id = "config-env-pdo-session-auth-v1"
    text = _decode_php(data, "includes/config.php")
    newline = _newline(text)
    replacement = """<?php
function webcore_required_env($name)
{
\t$value = getenv($name);
\tif ($value === false || trim($value) === '') {
\t\tthrow new RuntimeException('Missing required WebCore database environment variable: ' . $name);
\t}
\treturn $value;
}

function webcore_db()
{
\tglobal $pdo;
\tif ($pdo instanceof PDO) {
\t\treturn $pdo;
\t}

\t$host = webcore_required_env('AOREBIRTH_WEBCORE_DB_HOST');
\t$name = webcore_required_env('AOREBIRTH_WEBCORE_DB_NAME');
\t$user = webcore_required_env('AOREBIRTH_WEBCORE_DB_USER');
\t$password = webcore_required_env('AOREBIRTH_WEBCORE_DB_PASSWORD');
\tif (!preg_match('/^[A-Za-z0-9._-]+$/D', $host)) {
\t\tthrow new RuntimeException('Invalid AOREBIRTH_WEBCORE_DB_HOST value.');
\t}
\tif (!preg_match('/^[A-Za-z0-9_]+$/D', $name)) {
\t\tthrow new RuntimeException('Invalid AOREBIRTH_WEBCORE_DB_NAME value.');
\t}

\t$pdo = new PDO(
\t\t'mysql:host=' . $host . ';dbname=' . $name . ';charset=latin1',
\t\t$user,
\t\t$password,
\t\tarray(
\t\t\tPDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
\t\t\tPDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
\t\t\tPDO::ATTR_EMULATE_PREPARES => true,
\t\t\tPDO::ATTR_PERSISTENT => false,
\t\t\tPDO::ATTR_STRINGIFY_FETCHES => false
\t\t)
\t);
\treturn $pdo;
}

function webcore_html($value)
{
\treturn htmlspecialchars((string)$value, ENT_QUOTES | ENT_SUBSTITUTE, 'ISO-8859-1');
}

if (session_status() !== PHP_SESSION_ACTIVE && !session_start()) {
\tthrow new RuntimeException('Unable to start the WebCore session.');
}

defined('PRE_LINK') ? $preLink = PRE_LINK : $preLink = '';
if(isset($includeCSSFiles)){
\t$includeCSSFiles[] = $preLink . 'css/style.css';
} else {
\t$includeCSSFiles = array($preLink . 'css/style.css');
}
if(!isset($includeJavascriptFiles)){
\t$includeJavascriptFiles = array();
}

if(defined('AUTH_REQUIRED') && AUTH_REQUIRED && !isset($_SESSION['SESS_ID'])){
\theader('Location: ' . $preLink . 'index.php?err=You must be logged in to view this page.');
\texit;
}
if(defined('ADMIN_REQUIRED') && ADMIN_REQUIRED
\t&& (!isset($_SESSION['SESS_ID'], $_SESSION['SESS_GM']) || (int)$_SESSION['SESS_GM'] < 100)){
\theader('Location: ' . $preLink . 'index.php?err=You do not have sufficient permission to view this page.');
\texit;
}
if(defined('GM_REQUIRED') && GM_REQUIRED
\t&& (!isset($_SESSION['SESS_ID'], $_SESSION['SESS_GM']) || (int)$_SESSION['SESS_GM'] < 1)){
\theader('Location: ' . $preLink . 'index.php?err=You do not have sufficient permission to view this page.');
\texit;
}

$pdo = null;
$pdo = webcore_db();
?>
"""
    if len(text.splitlines()) != 61:
        raise CompatibilityError(
            "{0}: expected 61 input lines, got {1}".format(operation_id, len(text.splitlines()))
        )
    return replacement.replace("\n", newline).encode("utf-8")


def _patch_register(data: bytes) -> bytes:
    operation_id = "register-string-length-v1"
    text = _decode_php(data, "register.php")
    text = _replace_exact(text, "sizeof($regArgs", "strlen($regArgs", 4, operation_id)
    return text.encode("utf-8")


def _patch_notfound(data: bytes) -> bytes:
    operation_id = "notfound-php8-and-server-html-encoding-v2"
    text = _decode_php(data, "notfound.php")
    text = _replace_exact(text, "<?$p=getdate();", "<?php $p=getdate();", 1, operation_id)
    text = _replace_exact(
        text,
        "echo '404 error: http://' . $_SERVER['SERVER_NAME'] . $_SERVER['REQUEST_URI'];",
        "echo '404 error: http://' . htmlspecialchars((string)$_SERVER['SERVER_NAME'], ENT_QUOTES | ENT_SUBSTITUTE, 'ISO-8859-1') . htmlspecialchars((string)$_SERVER['REQUEST_URI'], ENT_QUOTES | ENT_SUBSTITUTE, 'ISO-8859-1');",
        1,
        operation_id,
    )
    text = _replace_exact(
        text,
        "<?=$_SERVER['HTTP_REFERER']?>",
        "<?=isset($_SERVER['HTTP_REFERER']) ? htmlspecialchars((string)$_SERVER['HTTP_REFERER'], ENT_QUOTES | ENT_SUBSTITUTE, 'ISO-8859-1') : ''?>",
        1,
        operation_id,
    )
    text = _replace_exact(
        text,
        "<?=($_SERVER['HTTP_REFERER'])?$_SERVER['HTTP_REFERER']:\"<span class='m1'>Not Defined</span>\"?>",
        "<?=(isset($_SERVER['HTTP_REFERER']) && $_SERVER['HTTP_REFERER'] !== '') ? htmlspecialchars((string)$_SERVER['HTTP_REFERER'], ENT_QUOTES | ENT_SUBSTITUTE, 'ISO-8859-1') : \"<span class='m1'>Not Defined</span>\"?>",
        1,
        operation_id,
    )
    return text.encode("utf-8")


def _patch_header(data: bytes) -> bytes:
    operation_id = "header-html-encoding-v1"
    text = _decode_php(data, "includes/header.php")
    text = _replace_exact(
        text,
        'echo("<span class=\'error\'>" . $_REQUEST[\'err\'] . "</span>");',
        'echo("<span class=\'error\'>" . webcore_html($_REQUEST[\'err\']) . "</span>");',
        1,
        operation_id,
    )
    text = _replace_exact(
        text,
        'echo("<span class=\'message\'>" . $_REQUEST[\'msg\'] . "</span>");',
        'echo("<span class=\'message\'>" . webcore_html($_REQUEST[\'msg\']) . "</span>");',
        1,
        operation_id,
    )
    text = _replace_exact(
        text,
        "<?php print($_SESSION['SESS_FIRST_NAME']); ?>",
        "<?php print(webcore_html($_SESSION['SESS_FIRST_NAME'])); ?>",
        1,
        operation_id,
    )
    return text.encode("utf-8")


def _patch_playfields(data: bytes) -> bytes:
    operation_id = "playfields-sql-syntax-v1"
    text = _decode_php(data, "includes/data/playfields.php")
    text = _replace_exact(
        text,
        'FROM `playfields` WHERE `playfields`.");',
        'FROM `playfields` WHERE `playfields`.";',
        1,
        operation_id,
    )
    return text.encode("utf-8")


PATCH_DEFINITIONS: Tuple[PatchDefinition, ...] = (
    PatchDefinition(
        "engine-pdo-and-random-bytes-v1",
        "engine.php",
        "3b8e5dd745e076d278a31c923a6b4e43d411fb54a7ddf1bca0619385fac04c11",
        _patch_engine,
    ),
    PatchDefinition(
        "process-login-pdo-v1",
        "process-login.php",
        "73caa625e07f956c412fcdc218877d725c993a3a8aabc08209772bc8f69a5915",
        _patch_process_login,
    ),
    PatchDefinition(
        "config-env-pdo-session-auth-v1",
        "includes/config.php",
        "5f8ab5c37b98a993925c731180b6cc82be8b81f8ccb385a8bda7ecacac9eb7fb",
        _patch_config,
    ),
    PatchDefinition(
        "register-string-length-v1",
        "register.php",
        "8a3d97270c13e848f464ce3ae18e2fe8ec2dba601140b89a81625a0494996fd6",
        _patch_register,
    ),
    PatchDefinition(
        "notfound-php8-and-server-html-encoding-v2",
        "notfound.php",
        "5b1231bc293f6a74c3f4ffedf14cf41ee742795ebf512400e8eb73bc249e9fe2",
        _patch_notfound,
    ),
    PatchDefinition(
        "header-html-encoding-v1",
        "includes/header.php",
        "c12f87f77bded03e7bda7036b39ff667e854529108ff761737dc50d7b5604c2e",
        _patch_header,
    ),
    PatchDefinition(
        "playfields-sql-syntax-v1",
        "includes/data/playfields.php",
        "55013f36f6850124a2f26360cd40933356d2a7f5f6b2f31bd0a4045ad192f340",
        _patch_playfields,
    ),
)


REQUESTED_CATEGORIES: Tuple[str, ...] = (
    "syntax_errors",
    "removed_php_functions",
    "deprecated_php_functions",
    "mysql_star",
    "mysqli_star",
    "pdo",
    "mcrypt_star",
    "each",
    "create_function",
    "split",
    "ereg",
    "curly_brace_offsets",
    "old_style_constructors",
    "dynamic_properties",
    "bareword_array_keys",
    "short_open_tags",
    "register_globals",
    "magic_quotes",
    "safe_mode",
    "session_register",
    "get_magic_quotes_gpc",
    "legacy_password_hashing",
    "unserialized_user_controlled_data",
    "eval",
    "variable_includes",
    "shell_execution",
    "file_uploads",
    "path_construction",
    "cookie_security_flags",
    "session_storage_assumptions",
    "direct_sql_interpolation",
    "character_set_assumptions",
    "timezone_assumptions",
    "windows_path_behavior",
    "php_extension_dependencies",
    "bundled_third_party_php_libraries",
    "sql_injection",
    "command_injection",
    "path_traversal",
    "arbitrary_upload",
    "local_remote_file_inclusion",
    "unsafe_deserialization",
    "authentication_bypass",
    "session_fixation",
    "weak_password_hashing",
    "csrf",
    "xss",
    "hardcoded_secrets",
    "debug_installer_admin_endpoints",
)


def _finding(
    relative_path: str,
    line: str,
    pattern: str,
    runtime_impact: str,
    minimum_php: str,
    maximum_php: Optional[str],
    security_relevance: str,
    reachable: str,
    disposition: str,
) -> Dict[str, object]:
    return {
        "relative_path": relative_path,
        "line": line,
        "pattern": pattern,
        "runtime_impact": runtime_impact,
        "minimum_php": minimum_php,
        "maximum_php": maximum_php,
        "security_relevance": security_relevance,
        "reachable": reachable,
        "required_patch_action": disposition,
        "disposition": disposition,
    }


def _base_category_inventory() -> Dict[str, Dict[str, object]]:
    categories = {
        name: {"base_count": 0, "patched_count": 0, "findings": []}
        for name in REQUESTED_CATEGORIES
    }

    categories["syntax_errors"].update(
        base_count=1,
        patched_count=0,
        findings=[
            _finding(
                "includes/data/playfields.php",
                "24 (getPlayfield)",
                'SQL string closes as FROM `playfields` WHERE `playfields`.");',
                "PHP parse failure prevents the playfield endpoint from loading",
                "4.0",
                None,
                "availability",
                "denied by the WebEngine HTTP route allowlist",
                "remove only the unmatched closing parenthesis so the complete patched payload remains parseable",
            )
        ],
    )

    mysql_findings = [
        _finding(
            "engine.php",
            "17-19",
            "mysql_connect/mysql_select_db/mysql_query",
            "undefined functions on PHP 7+ if the function is called",
            "4.0",
            "5.6",
            "critical SQL interpolation in an otherwise unreferenced function",
            "engine.php, login, and registration routes are denied by the WebEngine HTTP allowlist",
            "replace with the exact PDO prepared operation",
        ),
        _finding(
            "process-login.php",
            "11,17,28,50,54,57",
            "mysql_* login flow",
            "normal login fatals on PHP 7+",
            "4.0",
            "5.6",
            "legacy escaping and implicit connection reuse",
            "process-login.php is denied by the WebEngine HTTP allowlist",
            "replace with the shared PDO prepared login query",
        ),
    ]
    categories["mysql_star"].update(base_count=9, patched_count=0, findings=mysql_findings)
    categories["removed_php_functions"].update(
        base_count=11,
        patched_count=0,
        findings=mysql_findings
        + [
            _finding(
                "engine.php",
                "30",
                "mcrypt_create_iv",
                "registration fatals because mcrypt is unavailable",
                "4.0",
                "7.1 core",
                "salt entropy boundary",
                "engine.php and register.php are denied by the WebEngine HTTP allowlist",
                "replace with random_bytes while preserving the exact stored format",
            ),
            _finding(
                "process-login.php",
                "25",
                "get_magic_quotes_gpc",
                "undefined function on PHP 8",
                "4.0",
                "7.4",
                "obsolete SQL escaping",
                "process-login.php is denied by the WebEngine HTTP allowlist",
                "remove with the mysql_* clean function",
            ),
        ],
    )
    categories["pdo"].update(
        base_count=45,
        patched_count=47,
        findings=[
            _finding(
                "includes/config.php; register.php; includes/data/users.php; includes/data/characters.php; includes/data/items.php; includes/data/playfields.php",
                "config 19; register 70-123; users 34-99; characters 24-99; items 35-83; playfields 21-55",
                "PDO constructor and prepare/execute/fetch operations",
                "requires PDO plus pdo_mysql",
                "5.1",
                None,
                "database boundary",
                "all files are directly addressable; most are reached by normal/admin UI",
                "retain with an explicit latin1 DSN, error mode, and prepared statements",
            )
        ],
    )
    categories["mcrypt_star"].update(
        base_count=1,
        patched_count=0,
        findings=categories["removed_php_functions"]["findings"][2:3],
    )
    categories["magic_quotes"].update(
        base_count=1,
        patched_count=0,
        findings=categories["removed_php_functions"]["findings"][3:4],
    )
    categories["get_magic_quotes_gpc"].update(
        base_count=1,
        patched_count=0,
        findings=categories["removed_php_functions"]["findings"][3:4],
    )
    categories["short_open_tags"].update(
        base_count=1,
        patched_count=0,
        findings=[
            _finding(
                "notfound.php",
                "56",
                "<? short opening tag",
                "not parsed on PHP 8",
                "4.0",
                "7.4",
                "source fragment may be emitted",
                "direct route; not selected by the C# 404 handler",
                "replace with the full PHP opening tag",
            )
        ],
    )
    categories["deprecated_php_functions"].update(
        base_count=4,
        patched_count=0,
        findings=[
            _finding(
                "register.php",
                "137-140",
                "sizeof on string",
                "warning from PHP 7.2 and TypeError on PHP 8",
                "4.0",
                "7.1 without warning",
                "availability",
                "registration form after submission",
                "replace the four string-size calls with strlen",
            )
        ],
    )
    categories["legacy_password_hashing"].update(
        base_count=1,
        patched_count=1,
        findings=[
            _finding(
                "engine.php",
                "3-109",
                "PBKDF2-HMAC-SHA1, 1111-1366 iterations, 30-byte salt/hash",
                "executes on maintained PHP after entropy repair",
                "5.5",
                None,
                "weak by modern password-storage standards but exact AO persisted contract",
                "engine.php, process-login.php, and register.php are denied by the WebEngine HTTP allowlist",
                "preserve in this no-schema task; document migration as separate work",
            )
        ],
    )
    categories["path_construction"].update(
        base_count=32,
        patched_count=32,
        findings=[
            _finding(
                "19 PHP files",
                "fixed require/include calls and stats.php 23,45",
                "relative local includes and fixed DOMDocument paths",
                "depends on deterministic CGI working directory/open_basedir",
                "5.5",
                None,
                "no user-controlled PHP include or filesystem path found",
                "normal and direct routes",
                "set script-directory CGI cwd and constrain open_basedir",
            )
        ],
    )
    categories["cookie_security_flags"].update(
        base_count=0,
        patched_count=0,
        findings=[
            _finding(
                "includes/config.php",
                "8",
                "session_start with no source-level cookie policy",
                "inherits php.ini defaults",
                "5.5",
                None,
                "high",
                "all stateful routes",
                "require strict cookie-only HttpOnly SameSite policy in the approved php.ini; Secure requires HTTPS",
            )
        ],
    )
    categories["session_storage_assumptions"].update(
        base_count=43,
        patched_count=43,
        findings=[
            _finding(
                "includes/config.php; process-login.php; logout.php; auth.php; includes/header.php; member-profile.php",
                "43 session API/superglobal references",
                "default file session storage and cookie settings",
                "requires a writable dedicated non-web session path",
                "5.5",
                None,
                "high",
                "all authenticated routes",
                "validate php.ini session path/strict mode/cookie policy; retain exact session field names",
            )
        ],
    )
    categories["direct_sql_interpolation"].update(
        base_count=2,
        patched_count=0,
        findings=mysql_findings,
    )
    categories["sql_injection"].update(
        base_count=2,
        patched_count=0,
        findings=mysql_findings,
    )
    categories["character_set_assumptions"].update(
        base_count=2,
        patched_count=1,
        findings=[
            _finding(
                "includes/header.php; includes/config.php",
                "header 8; config 19",
                "ISO-8859-1 HTML plus PDO DSN without charset",
                "JSON and database conversions depend on implicit encodings",
                "5.5",
                None,
                "high data-integrity boundary",
                "all pages and DB routes",
                "preserve repository latin1 tables with explicit PDO charset; live non-ASCII validation remains blocked",
            )
        ],
    )
    categories["timezone_assumptions"].update(
        base_count=2,
        patched_count=2,
        findings=[
            _finding(
                "register.php; includes/header.php",
                "register 26; header 40",
                "date() uses runtime timezone",
                "account CreationDate and displayed date vary by php.ini",
                "5.5",
                None,
                "medium persisted/display behavior",
                "registration and every page header",
                "pin the approved timezone; credential-backed parity remains unverified",
            )
        ],
    )
    categories["php_extension_dependencies"].update(
        base_count=7,
        patched_count=7,
        findings=[
            _finding(
                "active PHP inventory",
                "PDO/config/data; DOM stats; session/hash/json/filter/ctype throughout",
                "pdo_mysql, dom, session, hash, json, filter, ctype",
                "missing extension causes startup or route failure",
                "8.2",
                None,
                "runtime supply boundary",
                "normal and direct routes",
                "validate the seven extensions; mcrypt is explicitly not required after patching",
            )
        ],
    )
    categories["authentication_bypass"].update(
        base_count=3,
        patched_count=0,
        findings=[
            _finding(
                "includes/config.php",
                "35-58",
                "AUTH_REQUIRED/ADMIN_REQUIRED/GM_REQUIRED redirect without exit",
                "denied scripts continue executing",
                "5.5",
                None,
                "critical",
                "admin, member, and data routes are denied by the WebEngine HTTP allowlist",
                "terminate immediately and verify session identity before role comparison",
            )
        ],
    )
    categories["session_fixation"].update(
        base_count=1,
        patched_count=0,
        findings=[
            _finding(
                "process-login.php",
                "56-75",
                "session_regenerate_id before password validation without deleting old session",
                "session transition is not tightly bound to successful authentication",
                "5.5",
                None,
                "high",
                "process-login.php is denied by the WebEngine HTTP allowlist",
                "regenerate with deletion only after password validation",
            )
        ],
    )
    categories["weak_password_hashing"].update(
        base_count=1,
        patched_count=1,
        findings=categories["legacy_password_hashing"]["findings"],
    )
    categories["csrf"].update(
        base_count=5,
        patched_count=5,
        findings=[
            _finding(
                "register.php; logout.php; includes/data/users.php; includes/data/characters.php; includes/data/items.php",
                "registration/logout/account/character/item state routes",
                "state changes have no CSRF token; several use GET",
                "source routes lack CSRF defenses but cannot be reached through WebEngine",
                "5.5",
                None,
                "high to critical",
                "all affected routes are denied by the WebEngine HTTP allowlist",
                "fail closed at the host boundary; add POST and session-bound tokens before any future route expansion",
            )
        ],
    )
    categories["xss"].update(
        base_count=6,
        patched_count=4,
        findings=[
            _finding(
                "includes/header.php",
                "31-36,48",
                "request/session values emitted into HTML",
                "script execution in the WebCore origin",
                "5.5",
                None,
                "high",
                "allowlisted pages that include the shared header",
                "HTML-escape err/msg/session name in compatibility patch",
            ),
            _finding(
                "notfound.php",
                "2,35,50",
                "SERVER_NAME, REQUEST_URI, and HTTP_REFERER emitted without context encoding",
                "script execution in the allowlisted not-found response",
                "5.5",
                None,
                "high",
                "allowlisted notfound.php route",
                "HTML-encode every server-derived output in the exact compatibility transform",
            ),
            _finding(
                "member-profile.php; admin/findUser.php; admin/editUser.php; admin/editCharacter.php",
                "profile 39-49; findUser 6,53-80; editUser 2,157,163,179; editCharacter 2,201,216,262",
                "unescaped database/request values in HTML or JavaScript",
                "script execution in authenticated/admin/direct contexts",
                "5.5",
                None,
                "high to critical",
                "all affected admin/member routes are denied by the WebEngine HTTP allowlist",
                "fail closed at the host boundary; encode by output context before any future route expansion",
            ),
        ],
    )
    categories["hardcoded_secrets"].update(
        base_count=1,
        patched_count=0,
        findings=[
            _finding(
                "includes/config.php",
                "3-6",
                "hardcoded upstream DB literals including password placeholder",
                "cannot safely select an operator database",
                "5.5",
                None,
                "critical credential boundary",
                "every page through eager PDO",
                "require the four local environment variables and never persist their values",
            )
        ],
    )
    categories["debug_installer_admin_endpoints"].update(
        base_count=8,
        patched_count=8,
        findings=[
            _finding(
                "admin/*.php; includes/data/users.php; includes/data/characters.php; includes/data/items.php",
                "direct routes",
                "historical administrative UI and JSON endpoints",
                "exposes account, character, inventory, and item-spawn functions",
                "5.5",
                None,
                "critical until separately hardened",
                "admin and includes routes are denied by the WebEngine HTTP allowlist",
                "retain the fail-closed host route policy; separately harden before any route expansion",
            )
        ],
    )
    for category_name, category in categories.items():
        category["findings"] = [
            dict(finding, category=category_name) for finding in category["findings"]
        ]
    return categories


def _expected_scan_counts() -> Mapping[str, int]:
    return {
        "mysql_star": 9,
        "mysqli_star": 0,
        "mcrypt_star": 1,
        "each": 0,
        "create_function": 0,
        "split": 0,
        "ereg": 0,
        "curly_brace_offsets": 0,
        "dynamic_properties": 0,
        "short_open_tags": 1,
        "register_globals": 0,
        "safe_mode": 0,
        "session_register": 0,
        "get_magic_quotes_gpc": 1,
        "unserialized_user_controlled_data": 0,
        "eval": 0,
        "shell_execution": 0,
        "file_uploads": 0,
        "sizeof_string": 4,
    }


def _scan_php_text(text: str) -> Dict[str, int]:
    patterns = {
        "mysql_star": re.compile(r"\bmysql_[A-Za-z0-9_]+\s*\("),
        "mysqli_star": re.compile(r"\bmysqli(?:_[A-Za-z0-9_]+)?\s*\("),
        "mcrypt_star": re.compile(r"\bmcrypt_[A-Za-z0-9_]+\s*\("),
        "each": re.compile(r"(?<![A-Za-z0-9_.])each\s*\("),
        "create_function": re.compile(r"\bcreate_function\s*\("),
        "split": re.compile(r"(?<![A-Za-z0-9_])split\s*\("),
        "ereg": re.compile(r"\bereg(?:i|_replace|i_replace)?\s*\("),
        "curly_brace_offsets": re.compile(r"\$[A-Za-z_][A-Za-z0-9_]*(?:\[[^\]]+\]|->[A-Za-z_][A-Za-z0-9_]*)*\s*\{[^{}\r\n]+\}"),
        "dynamic_properties": re.compile(r"\$this->[A-Za-z_][A-Za-z0-9_]*\s*="),
        "short_open_tags": re.compile(r"<\?(?!php\b|=)", re.IGNORECASE),
        "register_globals": re.compile(r"\bregister_globals\b", re.IGNORECASE),
        "safe_mode": re.compile(r"\bsafe_mode\b", re.IGNORECASE),
        "session_register": re.compile(r"\bsession_register\s*\("),
        "get_magic_quotes_gpc": re.compile(r"\bget_magic_quotes_gpc\s*\("),
        "unserialized_user_controlled_data": re.compile(r"\bunserialize\s*\("),
        "eval": re.compile(r"\beval\s*\("),
        "shell_execution": re.compile(r"\b(?:exec|system|passthru|shell_exec|popen|proc_open|pcntl_exec)\s*\("),
        "file_uploads": re.compile(r"\$_FILES\b|\b(?:move_uploaded_file|is_uploaded_file)\s*\("),
        "sizeof_string": re.compile(r"\bsizeof\s*\(\s*\$regArgs"),
    }
    return {name: len(pattern.findall(text)) for name, pattern in patterns.items()}


def scan_php_categories(root: Path, php_paths: Iterable[str]) -> Dict[str, int]:
    text = "\n".join((root / path).read_text(encoding="utf-8") for path in sorted(php_paths))
    return _scan_php_text(text)


def scan_patched_php_categories(
    base_root: Path,
    php_paths: Iterable[str],
    patched_bytes: Mapping[str, bytes],
) -> Dict[str, int]:
    chunks = []
    for path in sorted(php_paths):
        data = patched_bytes.get(path)
        if data is None:
            data = (base_root / Path(*path.split("/"))).read_bytes()
        chunks.append(_decode_php(data, path))
    return _scan_php_text("\n".join(chunks))


def validate_patched_scan(counts: Mapping[str, int]) -> None:
    nonzero = {name: count for name, count in counts.items() if count != 0}
    if nonzero:
        raise CompatibilityError("patched PHP banned-pattern scan failed: " + repr(nonzero))


def build_inventory(base_root: Path, base_entries: Sequence[FileEntry], patched_entries: Sequence[FileEntry]) -> Dict[str, object]:
    php_paths = [entry.path for entry in base_entries if entry.path.lower().endswith(".php")]
    scan_counts = scan_php_categories(base_root, php_paths)
    expected = _expected_scan_counts()
    for name, expected_count in expected.items():
        actual = scan_counts.get(name)
        if actual != expected_count:
            raise CompatibilityError(
                "compatibility audit drift for {0}: expected {1}, got {2}".format(
                    name, expected_count, actual
                )
            )

    categories = _base_category_inventory()
    if set(categories) != set(REQUESTED_CATEGORIES):
        raise CompatibilityError("compatibility category inventory is incomplete")
    return {
        "schema_version": 1,
        "patch_set_id": PATCH_SET_ID,
        "patch_set_version": PATCH_SET_VERSION,
        "upstream_commit": UPSTREAM_COMMIT,
        "base_manifest_sha256": BASE_MANIFEST_SHA256,
        "scope": {
            "file_count": len(base_entries),
            "php_file_count": len(php_paths),
            "patched_file_count": len(PATCH_DEFINITIONS),
            "base_total_bytes": sum(entry.size for entry in base_entries),
            "patched_total_bytes": sum(entry.size for entry in patched_entries),
        },
        "scan_counts": scan_counts,
        "categories": categories,
        "patch_operations": [
            {
                "operation_id": definition.operation_id,
                "relative_path": definition.path,
                "input_sha256": definition.input_sha256,
                "output_sha256": next(
                    entry.sha256 for entry in patched_entries if entry.path == definition.path
                ),
            }
            for definition in PATCH_DEFINITIONS
        ],
        "security_boundary": {
            "webengine_status": "development-only",
            "production_safe": False,
            "live_database_validation": "blocked-no-valid-credential",
            "host_route_policy": {
                "allowed_php_routes": [
                    "about.php",
                    "index.php",
                    "notfound.php",
                    "support.php",
                ],
                "denied_php_routes": [
                    "admin/",
                    "includes/",
                    "auth.php",
                    "engine.php",
                    "logout.php",
                    "member-*.php",
                    "process-login.php",
                    "register.php",
                ],
                "state_mutation_and_admin_member_findings": "fail-closed-unreachable",
            },
            "unresolved": [
                "live database behavior remains unvalidated without a valid credential",
                "plain HTTP cannot safely satisfy Secure authenticated-cookie transport",
                "upstream licensing remains unresolved",
            ],
        },
    }


def load_base_manifest(path: Path = BASE_MANIFEST_PATH) -> List[FileEntry]:
    if sha256_file(path) != BASE_MANIFEST_SHA256:
        raise CompatibilityError("base WebCore manifest SHA-256 mismatch")
    root = ET.parse(str(path)).getroot()
    if root.tag != "WebCoreAssetManifest":
        raise CompatibilityError("unexpected base WebCore manifest root")
    if root.attrib.get("UpstreamCommit") != UPSTREAM_COMMIT:
        raise CompatibilityError("base WebCore manifest commit mismatch")
    entries = []
    for element in root:
        if element.tag != "File" or set(element.attrib) != {"Path", "Size", "Sha256"}:
            raise CompatibilityError("invalid base WebCore manifest entry")
        entries.append(
            FileEntry(
                element.attrib["Path"],
                int(element.attrib["Size"]),
                element.attrib["Sha256"].lower(),
            )
        )
    _validate_entry_contract(entries, EXPECTED_BASE_FILE_COUNT)
    return entries


def _validate_entry_contract(entries: Sequence[FileEntry], expected_count: Optional[int] = None) -> None:
    if expected_count is not None and len(entries) != expected_count:
        raise CompatibilityError(
            "manifest file count mismatch: expected {0}, got {1}".format(expected_count, len(entries))
        )
    paths = [entry.path for entry in entries]
    if paths != sorted(paths, key=lambda value: value.encode("utf-8")):
        raise CompatibilityError("manifest paths are not ordinally sorted")
    lowered = [path.lower() for path in paths]
    if len(lowered) != len(set(lowered)):
        raise CompatibilityError("manifest contains duplicate or case-colliding paths")
    for entry in entries:
        if not entry.path or "\\" in entry.path or entry.path.startswith("/") or ".." in entry.path.split("/"):
            raise CompatibilityError("unsafe manifest path: " + entry.path)
        if entry.size < 0 or not re.fullmatch(r"[0-9a-f]{64}", entry.sha256):
            raise CompatibilityError("invalid manifest metadata for: " + entry.path)


def _enumerate_tree(root: Path) -> Dict[str, Path]:
    if not root.is_dir():
        raise CompatibilityError("asset root is missing: " + str(root))
    files = {}
    casefolded_paths = set()
    for current_root, directory_names, file_names in os.walk(str(root), followlinks=False):
        current = Path(current_root)
        for directory_name in directory_names:
            directory = current / directory_name
            if directory.is_symlink():
                raise CompatibilityError("asset tree contains a symlink: " + str(directory))
        for file_name in file_names:
            path = current / file_name
            if path.is_symlink():
                raise CompatibilityError("asset tree contains a symlink: " + str(path))
            relative = path.relative_to(root).as_posix()
            casefolded = relative.casefold()
            if casefolded in casefolded_paths:
                raise CompatibilityError("asset tree contains a case-colliding path: " + relative)
            files[relative] = path
            casefolded_paths.add(casefolded)
    return files


def validate_tree(root: Path, entries: Sequence[FileEntry]) -> None:
    actual = _enumerate_tree(root)
    expected_paths = {entry.path for entry in entries}
    actual_paths = set(actual)
    if actual_paths != expected_paths:
        missing = sorted(expected_paths - actual_paths)[:5]
        unexpected = sorted(actual_paths - expected_paths)[:5]
        raise CompatibilityError(
            "asset inventory mismatch; missing={0}; unexpected={1}".format(missing, unexpected)
        )
    for entry in entries:
        path = actual[entry.path]
        if path.stat().st_size != entry.size:
            raise CompatibilityError("asset size mismatch: " + entry.path)
        if sha256_file(path) != entry.sha256:
            raise CompatibilityError("asset SHA-256 mismatch: " + entry.path)


def compute_patched_entries(base_root: Path, base_entries: Sequence[FileEntry]) -> Tuple[List[FileEntry], Dict[str, bytes]]:
    definitions = {definition.path: definition for definition in PATCH_DEFINITIONS}
    patched_bytes = {}
    entries = []
    for entry in base_entries:
        path = base_root / Path(*entry.path.split("/"))
        data = path.read_bytes()
        definition = definitions.get(entry.path)
        if definition is not None:
            actual_hash = sha256_bytes(data)
            if actual_hash != definition.input_sha256 or actual_hash != entry.sha256:
                raise CompatibilityError("patch input SHA-256 mismatch: " + entry.path)
            data = definition.transform(data)
            patched_bytes[entry.path] = data
        entries.append(FileEntry(entry.path, len(data), sha256_bytes(data)))
    return entries, patched_bytes


def _render_patched_manifest(entries: Sequence[FileEntry]) -> bytes:
    attributes = (
        ("SchemaVersion", "1"),
        ("Id", PATCH_SET_ID),
        ("PatchSetVersion", PATCH_SET_VERSION),
        ("UpstreamCommit", UPSTREAM_COMMIT),
        ("BaseManifestSha256", BASE_MANIFEST_SHA256),
        ("FileCount", str(len(entries))),
        ("TotalBytes", str(sum(entry.size for entry in entries))),
    )
    root = "<WebCorePatchedAssetManifest {0}>".format(
        " ".join("{0}={1}".format(name, quoteattr(value)) for name, value in attributes)
    )
    lines = ['<?xml version="1.0" encoding="utf-8"?>', root]
    lines.extend(
        "  <File Path={0} Size={1} Sha256={2} />".format(
            quoteattr(entry.path), quoteattr(str(entry.size)), quoteattr(entry.sha256)
        )
        for entry in entries
    )
    lines.append("</WebCorePatchedAssetManifest>")
    return ("\n".join(lines) + "\n").encode("utf-8")


def _render_compatibility_manifest(
    base_entries: Sequence[FileEntry],
    patched_entries: Sequence[FileEntry],
    patched_manifest_sha256: str,
) -> bytes:
    base_by_path = {entry.path: entry for entry in base_entries}
    patched_by_path = {entry.path: entry for entry in patched_entries}
    attributes = (
        ("SchemaVersion", "1"),
        ("Id", PATCH_SET_ID),
        ("PatchSetVersion", PATCH_SET_VERSION),
        ("UpstreamCommit", UPSTREAM_COMMIT),
        ("BaseManifestSha256", BASE_MANIFEST_SHA256),
        ("FinalManifestSha256", patched_manifest_sha256),
        ("FileCount", str(len(patched_entries))),
        ("PatchFileCount", str(len(PATCH_DEFINITIONS))),
    )
    root = "<WebCoreCompatibilityManifest {0}>".format(
        " ".join("{0}={1}".format(name, quoteattr(value)) for name, value in attributes)
    )
    lines = ['<?xml version="1.0" encoding="utf-8"?>', root]
    for definition in PATCH_DEFINITIONS:
        base = base_by_path[definition.path]
        patched = patched_by_path[definition.path]
        lines.append(
            "  <Patch OperationId={0} Path={1} InputSize={2} InputSha256={3} OutputSize={4} OutputSha256={5} />".format(
                quoteattr(definition.operation_id),
                quoteattr(definition.path),
                quoteattr(str(base.size)),
                quoteattr(base.sha256),
                quoteattr(str(patched.size)),
                quoteattr(patched.sha256),
            )
        )
    lines.append("</WebCoreCompatibilityManifest>")
    return ("\n".join(lines) + "\n").encode("utf-8")


def _load_patched_manifest(path: Path = PATCHED_MANIFEST_PATH) -> List[FileEntry]:
    root = ET.parse(str(path)).getroot()
    if root.tag != "WebCorePatchedAssetManifest":
        raise CompatibilityError("unexpected patched manifest root")
    expected_root = {
        "SchemaVersion": "1",
        "Id": PATCH_SET_ID,
        "PatchSetVersion": PATCH_SET_VERSION,
        "UpstreamCommit": UPSTREAM_COMMIT,
        "BaseManifestSha256": BASE_MANIFEST_SHA256,
        "FileCount": str(EXPECTED_BASE_FILE_COUNT),
    }
    for name, expected in expected_root.items():
        if root.attrib.get(name) != expected:
            raise CompatibilityError("patched manifest root mismatch: " + name)
    entries = []
    for element in root:
        if element.tag != "File" or set(element.attrib) != {"Path", "Size", "Sha256"}:
            raise CompatibilityError("invalid patched manifest entry")
        entries.append(
            FileEntry(
                element.attrib["Path"],
                int(element.attrib["Size"]),
                element.attrib["Sha256"].lower(),
            )
        )
    _validate_entry_contract(entries, EXPECTED_BASE_FILE_COUNT)
    if root.attrib.get("TotalBytes") != str(sum(entry.size for entry in entries)):
        raise CompatibilityError("patched manifest total-byte mismatch")
    return entries


def _load_compatibility_manifest(path: Path = COMPATIBILITY_MANIFEST_PATH) -> Dict[str, Dict[str, str]]:
    root = ET.parse(str(path)).getroot()
    if root.tag != "WebCoreCompatibilityManifest":
        raise CompatibilityError("unexpected compatibility manifest root")
    expected_root = {
        "SchemaVersion": "1",
        "Id": PATCH_SET_ID,
        "PatchSetVersion": PATCH_SET_VERSION,
        "UpstreamCommit": UPSTREAM_COMMIT,
        "BaseManifestSha256": BASE_MANIFEST_SHA256,
        "FinalManifestSha256": sha256_file(PATCHED_MANIFEST_PATH),
        "FileCount": str(EXPECTED_BASE_FILE_COUNT),
        "PatchFileCount": str(len(PATCH_DEFINITIONS)),
    }
    for name, expected in expected_root.items():
        if root.attrib.get(name) != expected:
            raise CompatibilityError("compatibility manifest root mismatch: " + name)
    patches = {}
    expected_attributes = {
        "OperationId", "Path", "InputSize", "InputSha256", "OutputSize", "OutputSha256"
    }
    for element in root:
        if element.tag != "Patch" or set(element.attrib) != expected_attributes:
            raise CompatibilityError("invalid compatibility patch entry")
        path_value = element.attrib["Path"]
        if path_value in patches:
            raise CompatibilityError("duplicate compatibility patch path: " + path_value)
        patches[path_value] = dict(element.attrib)
    definitions = {definition.path: definition for definition in PATCH_DEFINITIONS}
    if set(patches) != set(definitions):
        raise CompatibilityError("compatibility patch path set mismatch")
    base_entries = {entry.path: entry for entry in load_base_manifest()}
    patched_entries = {entry.path: entry for entry in _load_patched_manifest()}
    for path_value, definition in definitions.items():
        patch = patches[path_value]
        if patch["OperationId"] != definition.operation_id:
            raise CompatibilityError("operation ID mismatch: " + path_value)
        if patch["InputSha256"] != definition.input_sha256:
            raise CompatibilityError("patch input hash mismatch: " + path_value)
        base_entry = base_entries[path_value]
        patched_entry = patched_entries[path_value]
        if patch["InputSize"] != str(base_entry.size) or patch["InputSha256"] != base_entry.sha256:
            raise CompatibilityError("compatibility/base manifest mismatch: " + path_value)
        if (
            patch["OutputSize"] != str(patched_entry.size)
            or patch["OutputSha256"] != patched_entry.sha256
        ):
            raise CompatibilityError("compatibility/patched manifest mismatch: " + path_value)
    return patches


def _write_or_check(path: Path, expected: bytes, check: bool) -> None:
    if check:
        if not path.is_file() or path.read_bytes() != expected:
            raise CompatibilityError("generated artifact is stale: " + str(path.relative_to(REPOSITORY_ROOT)))
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_bytes(expected)
    os.replace(str(temporary), str(path))


def generate_artifacts(base_root: Path, check: bool = False) -> Dict[str, object]:
    base_entries = load_base_manifest()
    validate_tree(base_root, base_entries)
    patched_entries, patched_bytes = compute_patched_entries(base_root, base_entries)
    php_paths = [entry.path for entry in base_entries if entry.path.lower().endswith(".php")]
    validate_patched_scan(scan_patched_php_categories(base_root, php_paths, patched_bytes))
    patched_manifest = _render_patched_manifest(patched_entries)
    compatibility_manifest = _render_compatibility_manifest(
        base_entries, patched_entries, sha256_bytes(patched_manifest)
    )
    inventory = build_inventory(base_root, base_entries, patched_entries)
    inventory_bytes = (json.dumps(inventory, indent=2, sort_keys=True) + "\n").encode("utf-8")
    _write_or_check(PATCHED_MANIFEST_PATH, patched_manifest, check)
    _write_or_check(COMPATIBILITY_MANIFEST_PATH, compatibility_manifest, check)
    _write_or_check(INVENTORY_PATH, inventory_bytes, check)
    return inventory


def apply_patch_set(source_root: Path, output_root: Path) -> None:
    base_entries = load_base_manifest()
    validate_tree(source_root, base_entries)
    patched_entries = _load_patched_manifest()
    compatibility = _load_compatibility_manifest()
    source_resolved = source_root.resolve()
    output_resolved = output_root.resolve()
    if output_resolved == source_resolved or source_resolved in output_resolved.parents:
        raise CompatibilityError("patch output must not be inside the source tree")
    if output_root.exists():
        raise CompatibilityError("patch output already exists: " + str(output_root))
    output_root.parent.mkdir(parents=True, exist_ok=True)
    staging = Path(tempfile.mkdtemp(prefix=output_root.name + ".staging-", dir=str(output_root.parent)))
    try:
        shutil.copytree(str(source_root), str(staging), dirs_exist_ok=True)
        for definition in PATCH_DEFINITIONS:
            source_path = source_root / Path(*definition.path.split("/"))
            target_path = staging / Path(*definition.path.split("/"))
            data = source_path.read_bytes()
            if sha256_bytes(data) != definition.input_sha256:
                raise CompatibilityError("patch input SHA-256 mismatch: " + definition.path)
            patched = definition.transform(data)
            expected = compatibility[definition.path]
            if len(patched) != int(expected["OutputSize"]):
                raise CompatibilityError("patch output size mismatch: " + definition.path)
            if sha256_bytes(patched) != expected["OutputSha256"]:
                raise CompatibilityError("patch output SHA-256 mismatch: " + definition.path)
            target_path.write_bytes(patched)
        validate_tree(staging, patched_entries)
        os.replace(str(staging), str(output_root))
    except Exception:
        shutil.rmtree(str(staging), ignore_errors=True)
        raise


def audit_base(base_root: Path) -> Dict[str, object]:
    base_entries = load_base_manifest()
    validate_tree(base_root, base_entries)
    patched_entries, _ = compute_patched_entries(base_root, base_entries)
    return build_inventory(base_root, base_entries, patched_entries)


def validate_patched(root: Path) -> None:
    entries = _load_patched_manifest()
    compatibility = _load_compatibility_manifest()
    validate_tree(root, entries)
    entries_by_path = {entry.path: entry for entry in entries}
    for definition in PATCH_DEFINITIONS:
        expected = compatibility[definition.path]
        entry = entries_by_path[definition.path]
        if entry.sha256 != expected["OutputSha256"] or entry.size != int(expected["OutputSize"]):
            raise CompatibilityError("patched manifest/compatibility mismatch: " + definition.path)
    php_paths = [entry.path for entry in entries if entry.path.lower().endswith(".php")]
    validate_patched_scan(scan_php_categories(root, php_paths))


def lint_paths(root: Path) -> List[str]:
    validate_patched(root)
    return [entry.path for entry in _load_patched_manifest() if entry.path.lower().endswith(".php")]


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    audit_parser = subparsers.add_parser("audit", help="validate and audit an exact base WebCore tree")
    audit_parser.add_argument("source_root", type=Path)

    apply_parser = subparsers.add_parser("apply", help="create a verified patched tree from an exact base tree")
    apply_parser.add_argument("source_root", type=Path)
    apply_parser.add_argument("output_root", type=Path)

    validate_parser = subparsers.add_parser("validate", help="validate a complete patched WebCore tree")
    validate_parser.add_argument("root", type=Path)

    lint_parser = subparsers.add_parser("lint-list", help="print the validated patched PHP file list")
    lint_parser.add_argument("root", type=Path)

    generate_parser = subparsers.add_parser("generate", help="regenerate checked-in compatibility artifacts")
    generate_parser.add_argument("source_root", type=Path)
    generate_parser.add_argument("--check", action="store_true")
    return parser


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = _parser().parse_args(argv)
    if args.command == "audit":
        print(json.dumps(audit_base(args.source_root.resolve()), indent=2, sort_keys=True))
    elif args.command == "apply":
        apply_patch_set(args.source_root.resolve(), args.output_root.resolve())
        print("[WebCore PHP Compatibility] APPLY PASS")
    elif args.command == "validate":
        validate_patched(args.root.resolve())
        print("[WebCore PHP Compatibility] VALIDATE PASS")
    elif args.command == "lint-list":
        for relative_path in lint_paths(args.root.resolve()):
            print(str((args.root.resolve() / Path(*relative_path.split("/"))).resolve()))
    elif args.command == "generate":
        generate_artifacts(args.source_root.resolve(), check=args.check)
        print("[WebCore PHP Compatibility] GENERATE {0}".format("CHECK PASS" if args.check else "PASS"))
    else:
        raise CompatibilityError("unsupported command")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (CompatibilityError, OSError, ValueError, ET.ParseError) as error:
        print("[WebCore PHP Compatibility] FAIL: " + str(error))
        sys.exit(1)
