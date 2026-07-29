<?php
/**
 * One-shot import of legacy JSON vault/orders/logs into MySQL.
 * Run after alter_gmi_mysql_cutover.sql. Safe to re-run (vault upsert; orders skip existing by scanning; logs append).
 *
 * CLI: php migrate_json_to_mysql.php
 * Browser: /market/migrate_json_to_mysql.php?confirm=1
 */
header('Content-Type: text/plain; charset=utf-8');

if (PHP_SAPI !== 'cli') {
    if (!isset($_GET['confirm']) || $_GET['confirm'] !== '1') {
        echo "Add ?confirm=1 to run JSON → MySQL migration.\n";
        exit;
    }
}

require_once __DIR__ . DIRECTORY_SEPARATOR . 'gmi_lib.php';

$report = array(
    'vaults' => 0,
    'vaultItems' => 0,
    'orders' => 0,
    'logs' => 0,
    'errors' => array(),
);

function mig_char_id($raw) {
    return gmi_char_id_int($raw);
}

$dataDir = gmi_data_dir();
$seenVault = array();

foreach (glob($dataDir . DIRECTORY_SEPARATOR . 'char_*.json') as $path) {
    $vault = gmi_read_json_file($path);
    if (!is_array($vault)) {
        continue;
    }
    $cid = mig_char_id(isset($vault['characterId']) ? $vault['characterId'] : '');
    if ($cid <= 0) {
        $base = basename($path, '.json');
        $cid = mig_char_id(substr($base, 5));
    }
    if ($cid <= 0 || isset($seenVault[$cid])) {
        continue;
    }
    $seenVault[$cid] = true;
    $name = isset($vault['character']) ? $vault['character'] : '';
    $vault['character'] = $name;
    $vault['characterId'] = strval($cid);
    if (!isset($vault['items']) || !is_array($vault['items'])) {
        $vault['items'] = array();
    }
    if (gmi_save_vault($name, strval($cid), $vault)) {
        $report['vaults']++;
        $report['vaultItems'] += count($vault['items']);
    } else {
        $report['errors'][] = 'vault save failed: ' . $path;
    }
}

foreach (glob($dataDir . DIRECTORY_SEPARATOR . 'name_*.json') as $path) {
    $vault = gmi_read_json_file($path);
    if (!is_array($vault)) {
        continue;
    }
    $cid = mig_char_id(isset($vault['characterId']) ? $vault['characterId'] : '');
    if ($cid <= 0 || isset($seenVault[$cid])) {
        continue;
    }
    $seenVault[$cid] = true;
    $name = isset($vault['character']) ? $vault['character'] : '';
    $vault['character'] = $name;
    $vault['characterId'] = strval($cid);
    if (!isset($vault['items']) || !is_array($vault['items'])) {
        $vault['items'] = array();
    }
    if (gmi_save_vault($name, strval($cid), $vault)) {
        $report['vaults']++;
        $report['vaultItems'] += count($vault['items']);
    } else {
        $report['errors'][] = 'vault save failed: ' . $path;
    }
}

$ordersDir = gmi_orders_dir();
foreach (glob($ordersDir . DIRECTORY_SEPARATOR . 'order_*.json') as $path) {
    $order = gmi_read_json_file($path);
    if (!is_array($order)) {
        continue;
    }
    $otype = isset($order['orderType']) ? $order['orderType'] : 'sell';
    $status = isset($order['status']) ? $order['status'] : 'open';
    $fields = array(
        'orderType' => $otype,
        'sellerCharacter' => isset($order['sellerCharacter']) ? $order['sellerCharacter'] : '',
        'sellerCharacterId' => isset($order['sellerCharacterId']) ? $order['sellerCharacterId'] : '0',
        'buyerCharacter' => isset($order['buyerCharacter']) ? $order['buyerCharacter'] : '',
        'buyerCharacterId' => isset($order['buyerCharacterId']) ? $order['buyerCharacterId'] : '0',
        'lowId' => isset($order['lowId']) ? intval($order['lowId']) : 0,
        'highId' => isset($order['highId']) ? intval($order['highId']) : 0,
        'quality' => isset($order['quality']) ? intval($order['quality']) : 1,
        'minQl' => isset($order['minQl']) ? intval($order['minQl']) : (isset($order['quality']) ? intval($order['quality']) : 1),
        'maxQl' => isset($order['maxQl']) ? intval($order['maxQl']) : (isset($order['quality']) ? intval($order['quality']) : 1),
        'count' => isset($order['count']) ? intval($order['count']) : 1,
        'icon' => isset($order['icon']) ? intval($order['icon']) : 0,
        'name' => isset($order['name']) ? $order['name'] : '',
        'unitPrice' => isset($order['unitPrice']) ? intval($order['unitPrice']) : 0,
        'expiresAt' => isset($order['expiresAt']) ? $order['expiresAt'] : null,
    );
    $newId = gmi_create_order($fields);
    if ($newId <= 0) {
        $report['errors'][] = 'order create failed: ' . $path;
        continue;
    }
    if ($status !== 'open') {
        $upd = gmi_find_order($newId);
        if (is_array($upd)) {
            $upd['status'] = $status;
            $upd['count'] = isset($order['count']) ? intval($order['count']) : 0;
            if (!empty($order['sellerCharacter'])) {
                $upd['sellerCharacter'] = $order['sellerCharacter'];
            }
            if (!empty($order['sellerCharacterId'])) {
                $upd['sellerCharacterId'] = $order['sellerCharacterId'];
            }
            if (!empty($order['buyerCharacter'])) {
                $upd['buyerCharacter'] = $order['buyerCharacter'];
            }
            if (!empty($order['buyerCharacterId'])) {
                $upd['buyerCharacterId'] = $order['buyerCharacterId'];
            }
            gmi_update_order($upd);
        }
    }
    $report['orders']++;
}

$logsDir = gmi_logs_dir();
$allLog = $logsDir . DIRECTORY_SEPARATOR . 'all.jsonl';
$logFiles = is_file($allLog) ? array($allLog) : glob($logsDir . DIRECTORY_SEPARATOR . '*.jsonl');
$seenLog = array();
foreach ($logFiles as $path) {
    $raw = gmi_strip_bom(@file_get_contents($path));
    if ($raw === false || $raw === '') {
        continue;
    }
    $lines = preg_split("/\r\n|\n|\r/", $raw);
    foreach ($lines as $line) {
        $line = trim($line);
        if ($line === '') {
            continue;
        }
        $row = json_decode($line, true);
        if (!is_array($row)) {
            continue;
        }
        $key = (isset($row['at']) ? $row['at'] : '') . '|' . (isset($row['message']) ? $row['message'] : '');
        if (isset($seenLog[$key])) {
            continue;
        }
        $seenLog[$key] = true;
        $cname = isset($row['character']) ? $row['character'] : '';
        $cid = isset($row['characterId']) ? $row['characterId'] : '';
        if (gmi_append_log($cname, $cid, array(
            'type' => isset($row['type']) ? $row['type'] : 'trade',
            'message' => isset($row['message']) ? $row['message'] : '',
            'name' => isset($row['name']) ? $row['name'] : '',
            'count' => isset($row['count']) ? intval($row['count']) : 0,
            'unitPrice' => isset($row['unitPrice']) ? intval($row['unitPrice']) : 0,
            'total' => isset($row['total']) ? intval($row['total']) : 0,
            'quality' => isset($row['quality']) ? intval($row['quality']) : 0,
            'lowId' => isset($row['lowId']) ? intval($row['lowId']) : 0,
            'highId' => isset($row['highId']) ? intval($row['highId']) : 0,
            'icon' => isset($row['icon']) ? intval($row['icon']) : 0,
            'otherCharacter' => isset($row['otherCharacter']) ? $row['otherCharacter'] : '',
            'otherCharacterId' => isset($row['otherCharacterId']) ? $row['otherCharacterId'] : '',
        ))) {
            $report['logs']++;
        }
    }
    if (basename($path) === 'all.jsonl') {
        break;
    }
}

echo "GMI JSON → MySQL migration complete\n";
echo 'vaults=' . $report['vaults'] . "\n";
echo 'vaultItems=' . $report['vaultItems'] . "\n";
echo 'orders=' . $report['orders'] . "\n";
echo 'logs=' . $report['logs'] . "\n";
if (count($report['errors']) > 0) {
    echo "errors:\n";
    foreach ($report['errors'] as $e) {
        echo '  ' . $e . "\n";
    }
}
