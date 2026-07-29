<?php
/**
 * Buy open sell order with buyer GMI credits.
 * unitPrice is per item; buyCount is how many to take from the stack (default = full stack).
 * Purchase arrives as mail From=Market; seller credits go to GMI vault.
 *
 * GET/POST: orderId, count (optional buy qty), character, characterId
 */
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

require_once __DIR__ . DIRECTORY_SEPARATOR . 'gmi_lib.php';

list($character, $characterId) = gmi_request_identity();
if ($character === '' && $characterId === '') {
    echo json_encode(array('ok' => false, 'error' => 'Character identity missing.'));
    exit;
}

$orderId = isset($_REQUEST['orderId']) ? $_REQUEST['orderId'] : '';
$order = gmi_find_order($orderId);
if (!is_array($order) || !isset($order['status']) || $order['status'] !== 'open') {
    echo json_encode(array('ok' => false, 'error' => 'Sell order not found or already sold.'));
    exit;
}
$otype = isset($order['orderType']) ? $order['orderType'] : 'sell';
if ($otype !== 'sell') {
    echo json_encode(array('ok' => false, 'error' => 'Not a sell order.'));
    exit;
}

$sellerName = isset($order['sellerCharacter']) ? $order['sellerCharacter'] : '';
$sellerId = isset($order['sellerCharacterId']) ? $order['sellerCharacterId'] : '';
if (gmi_same_character($character, $characterId, $sellerName, $sellerId)) {
    echo json_encode(array('ok' => false, 'error' => 'You cannot buy your own sell order.'));
    exit;
}

$unitPrice = isset($order['unitPrice']) ? intval($order['unitPrice']) : 0;
$available = isset($order['count']) ? intval($order['count']) : 1;
if ($unitPrice <= 0 || $available <= 0) {
    echo json_encode(array('ok' => false, 'error' => 'Invalid order price or count.'));
    exit;
}

$buyCount = isset($_REQUEST['count']) ? intval($_REQUEST['count']) : $available;
if ($buyCount <= 0) {
    $buyCount = 1;
}
if ($buyCount > $available) {
    $buyCount = $available;
}

$total = $unitPrice * $buyCount;

$buyer = gmi_load_vault($character, $characterId);
if (intval($buyer['credits']) < $total) {
    echo json_encode(array(
        'ok' => false,
        'error' => 'Not enough market credits. Need ' . number_format($total)
            . ' (' . number_format($unitPrice) . ' x ' . $buyCount . ').',
        'need' => $total,
        'have' => intval($buyer['credits']),
        'unitPrice' => $unitPrice,
    ));
    exit;
}

$seller = gmi_load_vault($sellerName, $sellerId);

$buyer['credits'] = intval($buyer['credits']) - $total;
$seller['credits'] = intval($seller['credits']) + $total;

if (!gmi_save_vault($character, $characterId, $buyer)) {
    echo json_encode(array('ok' => false, 'error' => 'Failed to update buyer vault.'));
    exit;
}
if (!gmi_save_vault($sellerName, $sellerId, $seller)) {
    $buyer['credits'] = intval($buyer['credits']) + $total;
    gmi_save_vault($character, $characterId, $buyer);
    echo json_encode(array('ok' => false, 'error' => 'Failed to credit seller vault.'));
    exit;
}

$pendingDir = gmi_data_dir() . DIRECTORY_SEPARATOR . 'pending';
if (!is_dir($pendingDir)) {
    @mkdir($pendingDir, 0777, true);
}

$itemName = isset($order['name']) ? $order['name'] : 'Item';
$payload = array(
    'kind' => 'purchase_item',
    'character' => $character,
    'characterId' => $characterId,
    'preDebited' => 1,
    'count' => $buyCount,
    'lowId' => isset($order['lowId']) ? intval($order['lowId']) : 0,
    'highId' => isset($order['highId']) ? intval($order['highId']) : 0,
    'quality' => isset($order['quality']) ? intval($order['quality']) : 1,
    'name' => $itemName,
    'orderId' => isset($order['id']) ? $order['id'] : '',
    'requestedAt' => gmdate('c'),
);

$fname = sprintf(
    'p_%s_%s.json',
    preg_replace('/[^0-9A-Za-z_-]/', '', $characterId !== '' ? $characterId : 'x'),
    str_replace('.', '', uniqid('', true))
);
if (@file_put_contents($pendingDir . DIRECTORY_SEPARATOR . $fname, json_encode($payload)) === false) {
    $buyer['credits'] = intval($buyer['credits']) + $total;
    $seller['credits'] = intval($seller['credits']) - $total;
    gmi_save_vault($character, $characterId, $buyer);
    gmi_save_vault($sellerName, $sellerId, $seller);
    echo json_encode(array('ok' => false, 'error' => 'Failed to queue purchase mail.'));
    exit;
}

$remaining = $available - $buyCount;
if ($remaining > 0) {
    $order['count'] = $remaining;
    $order['status'] = 'open';
} else {
    $order['status'] = 'filled';
    $order['count'] = 0;
    $order['filledAt'] = gmdate('c');
}
$order['buyerCharacter'] = $character;
$order['buyerCharacterId'] = $characterId;
$order['lastFilledAt'] = gmdate('c');
$order['lastBuyCount'] = $buyCount;
$order['lastTotal'] = $total;
gmi_update_order($order);

$notice = 'purchased ' . $buyCount . ' x ' . $itemName . ' @ '
    . number_format($unitPrice) . ' = ' . number_format($total)
    . ' credits — item arrives by mail (open Mail or Deposit once)';

gmi_append_log($character, $characterId, array(
    'type' => 'buy',
    'message' => $notice,
    'name' => $itemName,
    'count' => $buyCount,
    'unitPrice' => $unitPrice,
    'total' => $total,
    'quality' => isset($order['quality']) ? intval($order['quality']) : 0,
    'lowId' => isset($order['lowId']) ? intval($order['lowId']) : 0,
    'highId' => isset($order['highId']) ? intval($order['highId']) : 0,
    'icon' => isset($order['icon']) ? intval($order['icon']) : 0,
    'otherCharacter' => $sellerName,
    'otherCharacterId' => $sellerId,
));
gmi_append_log($sellerName, $sellerId, array(
    'type' => 'sell',
    'message' => 'sold ' . $buyCount . ' x ' . $itemName . ' for ' . number_format($total)
        . ' credits @ ' . number_format($unitPrice) . ' each to ' . ($character !== '' ? $character : 'buyer'),
    'name' => $itemName,
    'count' => $buyCount,
    'unitPrice' => $unitPrice,
    'total' => $total,
    'quality' => isset($order['quality']) ? intval($order['quality']) : 0,
    'lowId' => isset($order['lowId']) ? intval($order['lowId']) : 0,
    'highId' => isset($order['highId']) ? intval($order['highId']) : 0,
    'icon' => isset($order['icon']) ? intval($order['icon']) : 0,
    'otherCharacter' => $character,
    'otherCharacterId' => $characterId,
));

echo json_encode(array(
    'ok' => true,
    'credits' => intval($buyer['credits']),
    'items' => $buyer['items'],
    'character' => $character,
    'characterId' => $characterId,
    'mailQueued' => true,
    'unitPrice' => $unitPrice,
    'buyCount' => $buyCount,
    'total' => $total,
    'notice' => $notice,
));
