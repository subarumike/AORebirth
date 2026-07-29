<?php
/**
 * Create a buy order: escrow (unitPrice * count) from buyer vault credits.
 * GET/POST: lowId, highId, name, icon, price, count, minQl, maxQl, character, characterId
 */
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

require_once __DIR__ . DIRECTORY_SEPARATOR . 'gmi_lib.php';

list($character, $characterId) = gmi_request_identity();
if ($character === '' && $characterId === '') {
    echo json_encode(array('ok' => false, 'error' => 'Character identity missing.'));
    exit;
}

$lowId = isset($_REQUEST['lowId']) ? intval($_REQUEST['lowId']) : 0;
$highId = isset($_REQUEST['highId']) ? intval($_REQUEST['highId']) : $lowId;
$name = isset($_REQUEST['name']) ? trim($_REQUEST['name']) : ('Item ' . $lowId);
$icon = isset($_REQUEST['icon']) ? intval($_REQUEST['icon']) : $lowId;
$price = isset($_REQUEST['price']) ? intval($_REQUEST['price']) : 0;
$count = isset($_REQUEST['count']) ? intval($_REQUEST['count']) : 1;
$minQl = isset($_REQUEST['minQl']) ? intval($_REQUEST['minQl']) : 1;
$maxQl = isset($_REQUEST['maxQl']) ? intval($_REQUEST['maxQl']) : 200;

if ($lowId <= 0) {
    echo json_encode(array('ok' => false, 'error' => 'Item id missing.'));
    exit;
}
if ($price <= 0) {
    echo json_encode(array('ok' => false, 'error' => 'Price must be positive.'));
    exit;
}
if ($count <= 0) {
    $count = 1;
}
if ($minQl < 1) {
    $minQl = 1;
}
if ($maxQl < $minQl) {
    $maxQl = $minQl;
}

$total = $price * $count;
$vault = gmi_load_vault($character, $characterId);
$credits = isset($vault['credits']) ? intval($vault['credits']) : 0;
if ($credits < $total) {
    echo json_encode(array(
        'ok' => false,
        'error' => 'Not enough market credits (need ' . $total . ', have ' . $credits . ').',
    ));
    exit;
}

$vault['credits'] = $credits - $total;
if (!gmi_save_vault($character, $characterId, $vault)) {
    echo json_encode(array('ok' => false, 'error' => 'Failed to update vault.'));
    exit;
}

$expires = gmdate('c', time() + 7 * 86400);
$orderId = gmi_create_order(array(
    'orderType' => 'buy',
    'buyerCharacter' => $character,
    'buyerCharacterId' => $characterId,
    'sellerCharacter' => '',
    'sellerCharacterId' => '0',
    'lowId' => $lowId,
    'highId' => $highId,
    'minQl' => $minQl,
    'maxQl' => $maxQl,
    'count' => $count,
    'icon' => $icon,
    'name' => $name,
    'unitPrice' => $price,
    'expiresAt' => $expires,
));

if ($orderId <= 0) {
    $vault['credits'] = $credits;
    gmi_save_vault($character, $characterId, $vault);
    echo json_encode(array('ok' => false, 'error' => 'Failed to write buy order.'));
    exit;
}

$notice = 'buy order placed — ' . $count . ' x ' . $name . ' @ '
    . number_format($price) . ' (reserved ' . number_format($total) . ' credits)';
gmi_append_log($character, $characterId, array(
    'type' => 'list',
    'message' => $notice,
    'name' => $name,
    'count' => $count,
    'unitPrice' => $price,
    'total' => $total,
    'quality' => $minQl,
    'lowId' => $lowId,
    'highId' => $highId,
    'icon' => $icon,
));

echo json_encode(array(
    'ok' => true,
    'orderId' => strval($orderId),
    'credits' => intval($vault['credits']),
    'items' => $vault['items'],
    'notice' => $notice,
));
