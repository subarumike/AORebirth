<?php
/**
 * List a deposited vault item for sale (live GMI sell is web-side; captures show no Zone sell N3).
 * Removes stack from seller vault and inserts gmi_order.
 *
 * GET/POST: index, price, count, character, characterId
 */
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

require_once __DIR__ . DIRECTORY_SEPARATOR . 'gmi_lib.php';

list($character, $characterId) = gmi_request_identity();
if ($character === '' && $characterId === '') {
    echo json_encode(array('ok' => false, 'error' => 'Character identity missing.'));
    exit;
}

$index = isset($_REQUEST['index']) ? intval($_REQUEST['index']) : -1;
$price = isset($_REQUEST['price']) ? intval($_REQUEST['price']) : 0;
$count = isset($_REQUEST['count']) ? intval($_REQUEST['count']) : 1;
if ($price <= 0) {
    echo json_encode(array('ok' => false, 'error' => 'Price must be positive.'));
    exit;
}
if ($count <= 0) {
    $count = 1;
}

$vault = gmi_load_vault($character, $characterId);
if (!is_array($vault) || $index < 0 || $index >= count($vault['items'])) {
    echo json_encode(array('ok' => false, 'error' => 'Select an inventory item first.'));
    exit;
}

$item = $vault['items'][$index];
$have = isset($item['count']) ? intval($item['count']) : 1;
if ($count > $have) {
    $count = $have;
}

$lowId = isset($item['lowId']) ? intval($item['lowId']) : 0;
$highId = isset($item['highId']) ? intval($item['highId']) : $lowId;
$quality = isset($item['quality']) ? intval($item['quality']) : 1;
$icon = isset($item['icon']) ? intval($item['icon']) : $lowId;
$name = isset($item['name']) ? $item['name'] : ('Item ' . $lowId);

if ($count >= $have) {
    array_splice($vault['items'], $index, 1);
} else {
    $vault['items'][$index]['count'] = $have - $count;
}

if (!gmi_save_vault($character, $characterId, $vault)) {
    echo json_encode(array('ok' => false, 'error' => 'Failed to update vault.'));
    exit;
}

$expires = gmdate('c', time() + 7 * 86400);
$newId = gmi_create_order(array(
    'orderType' => 'sell',
    'sellerCharacter' => $character,
    'sellerCharacterId' => $characterId,
    'lowId' => $lowId,
    'highId' => $highId,
    'quality' => $quality,
    'minQl' => $quality,
    'maxQl' => $quality,
    'count' => $count,
    'icon' => $icon,
    'name' => $name,
    'unitPrice' => $price,
    'expiresAt' => $expires,
));

if ($newId <= 0) {
    $vault['items'][] = array(
        'lowId' => $lowId,
        'highId' => $highId,
        'quality' => $quality,
        'count' => $count,
        'icon' => $icon,
        'name' => $name,
    );
    gmi_save_vault($character, $characterId, $vault);
    echo json_encode(array('ok' => false, 'error' => 'Failed to create sell order.'));
    exit;
}

$order = array(
    'id' => strval($newId),
    'orderType' => 'sell',
    'status' => 'open',
    'sellerCharacter' => $character,
    'sellerCharacterId' => $characterId,
    'lowId' => $lowId,
    'highId' => $highId,
    'quality' => $quality,
    'count' => $count,
    'icon' => $icon,
    'name' => $name,
    'unitPrice' => $price,
    'createdAt' => gmdate('c'),
    'expiresAt' => $expires,
);

$notice = 'listed ' . $count . ' x ' . $name . ' for ' . number_format($price) . ' credits each';
gmi_append_log($character, $characterId, array(
    'type' => 'list',
    'message' => $notice,
    'name' => $name,
    'count' => $count,
    'unitPrice' => $price,
    'total' => $price * $count,
    'quality' => $quality,
    'lowId' => $lowId,
    'highId' => $highId,
    'icon' => $icon,
));

echo json_encode(array(
    'ok' => true,
    'order' => $order,
    'credits' => intval($vault['credits']),
    'items' => $vault['items'],
    'character' => $character,
    'characterId' => $characterId,
    'notice' => $notice,
));
