<?php
/**
 * Queue GMI withdraw for ZoneEngine mail delivery (capture 20260715-143838).
 * Debits vault in MySQL immediately so the Market UI updates without reopen.
 * Zone pending file uses preDebited=1 → mail only (no second vault deduct).
 *
 * kind=credits&amount=N  |  kind=item&index=N&count=N
 */
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

require_once __DIR__ . DIRECTORY_SEPARATOR . 'gmi_lib.php';

$pendingDir = gmi_data_dir() . DIRECTORY_SEPARATOR . 'pending';
if (!is_dir($pendingDir)) {
    @mkdir($pendingDir, 0777, true);
}

$kind = isset($_REQUEST['kind']) ? strtolower(trim($_REQUEST['kind'])) : '';
list($character, $characterId) = gmi_request_identity();

if ($character === '' && $characterId === '') {
    echo json_encode(array(
        'ok' => false,
        'error' => 'Character identity missing. Open Market from in-game.',
    ));
    exit;
}

$vault = gmi_load_vault($character, $characterId);
if (!is_array($vault)) {
    echo json_encode(array(
        'ok' => false,
        'error' => 'No vault yet.',
        'debugCharacter' => $character,
        'debugCharacterId' => $characterId,
    ));
    exit;
}
if ($character === '' && !empty($vault['character'])) {
    $character = preg_replace('/[^0-9A-Za-z _.-]/', '', $vault['character']);
}
if ($characterId === '' && !empty($vault['characterId'])) {
    $characterId = preg_replace('/[^0-9A-Za-z_-]/', '', $vault['characterId']);
}
$vault['character'] = $character;
$vault['characterId'] = $characterId;

if (!isset($vault['credits'])) {
    $vault['credits'] = 0;
}
if (!isset($vault['items']) || !is_array($vault['items'])) {
    $vault['items'] = array();
}

$payload = array(
    'kind' => $kind,
    'character' => $character,
    'characterId' => $characterId,
    'requestedAt' => gmdate('c'),
    'preDebited' => 1,
);

$notice = '';
if ($kind === 'credits') {
    $amount = isset($_REQUEST['amount']) ? intval($_REQUEST['amount']) : 0;
    if ($amount <= 0) {
        echo json_encode(array('ok' => false, 'error' => 'Amount must be positive.'));
        exit;
    }
    if (intval($vault['credits']) < $amount) {
        echo json_encode(array('ok' => false, 'error' => 'Not enough market credits.'));
        exit;
    }
    $vault['credits'] = intval($vault['credits']) - $amount;
    $payload['amount'] = $amount;
    $notice = 'you withdrew ' . number_format($amount) . ' credits';
} elseif ($kind === 'item') {
    $index = isset($_REQUEST['index']) ? intval($_REQUEST['index']) : -1;
    $count = isset($_REQUEST['count']) ? intval($_REQUEST['count']) : 1;
    if ($index < 0 || $index >= count($vault['items'])) {
        echo json_encode(array('ok' => false, 'error' => 'Select an inventory item.'));
        exit;
    }
    if ($count <= 0) {
        $count = 1;
    }
    $item = $vault['items'][$index];
    $have = isset($item['count']) ? intval($item['count']) : 1;
    if ($count > $have) {
        $count = $have;
    }
    $payload['index'] = $index;
    $payload['count'] = $count;
    $payload['lowId'] = isset($item['lowId']) ? intval($item['lowId']) : 0;
    $payload['highId'] = isset($item['highId']) ? intval($item['highId']) : 0;
    $payload['quality'] = isset($item['quality']) ? intval($item['quality']) : 1;
    $payload['name'] = isset($item['name']) ? $item['name'] : ('Item ' . $payload['lowId']);
    $notice = 'you withdrew ' . $count . ' unit of ' . $payload['name'];
    if ($count >= $have) {
        array_splice($vault['items'], $index, 1);
    } else {
        $vault['items'][$index]['count'] = $have - $count;
    }
} else {
    echo json_encode(array('ok' => false, 'error' => 'Unknown withdraw kind.'));
    exit;
}

if (!gmi_save_vault($character, $characterId, $vault)) {
    echo json_encode(array('ok' => false, 'error' => 'Failed to update vault.'));
    exit;
}

$fname = sprintf(
    'w_%s_%s_%s.json',
    preg_replace('/[^0-9A-Za-z_-]/', '', $characterId !== '' ? $characterId : 'x'),
    $kind,
    str_replace('.', '', uniqid('', true))
);
$path = $pendingDir . DIRECTORY_SEPARATOR . $fname;
if (@file_put_contents($path, json_encode($payload)) === false) {
    echo json_encode(array('ok' => false, 'error' => 'Failed to queue withdraw mail.'));
    exit;
}

echo json_encode(array(
    'ok' => true,
    'queued' => true,
    'preDebited' => true,
    'file' => $fname,
    'credits' => intval($vault['credits']),
    'items' => $vault['items'],
    'character' => $character,
    'characterId' => $characterId,
    'hint' => 'Balance updated. Mail arrives after Zone processes (open Mail or Deposit once).',
    'notice' => $notice,
));
