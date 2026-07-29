<?php
/**
 * MySQL connection helpers for GMI vault / orders / trade log.
 */

function gmi_db_config() {
    static $cfg = null;
    if ($cfg !== null) {
        return $cfg;
    }
    $path = __DIR__ . DIRECTORY_SEPARATOR . 'gmi_db_config.php';
    if (is_file($path)) {
        $loaded = include $path;
        if (is_array($loaded)) {
            $cfg = $loaded;
            return $cfg;
        }
    }
    $cfg = array(
        'host' => 'localhost',
        'user' => 'root',
        'password' => '',
        'database' => 'cellao_codex_clean',
    );
    return $cfg;
}

/**
 * @return mysqli
 */
function gmi_db() {
    static $db = null;
    if ($db instanceof mysqli) {
        return $db;
    }
    $cfg = gmi_db_config();
    $db = @new mysqli(
        isset($cfg['host']) ? $cfg['host'] : 'localhost',
        isset($cfg['user']) ? $cfg['user'] : 'root',
        isset($cfg['password']) ? $cfg['password'] : '',
        isset($cfg['database']) ? $cfg['database'] : 'cellao_codex_clean'
    );
    if ($db->connect_errno) {
        throw new Exception('GMI MySQL connect failed: ' . $db->connect_error);
    }
    $db->set_charset('utf8mb4');
    return $db;
}

/** Normalize browser/Zone hex or decimal character id to INT for gmi_* tables. */
function gmi_char_id_int($characterId) {
    if ($characterId === '' || $characterId === null) {
        return 0;
    }
    $s = (string)$characterId;
    if (ctype_digit($s)) {
        return intval($s, 10);
    }
    if (preg_match('/^[0-9A-Fa-f]+$/', $s)) {
        return intval(hexdec($s));
    }
    return 0;
}

function gmi_db_begin() {
    $db = gmi_db();
    if (!$db->begin_transaction()) {
        throw new Exception('GMI MySQL begin_transaction failed');
    }
}

function gmi_db_commit() {
    $db = gmi_db();
    if (!$db->commit()) {
        throw new Exception('GMI MySQL commit failed');
    }
}

function gmi_db_rollback() {
    $db = gmi_db();
    @$db->rollback();
}
