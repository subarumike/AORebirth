<?php
/**
 * Fulfill an open buy order: seller must have matching item in market vault.
 * Pays seller unitPrice*count from already-escrowed buy credits; mails item to buyer.
 *
 * GET/POST: orderId, character, characterId  (optional: index to pick vault slot)
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
$preferIndex = isset($_REQUEST['index']) ? intval($_REQUEST['index']) : -1;
$order = gmi_find_order($orderId);
if (!is_array($order) || !isset($order['status']) || $order['status'] !== 'open') {
    echo json_encode(array('ok' => false, 'error' => 'Buy order not found or already filled.'));
    exit;
}
$otype = isset($order['orderType']) ? $order['orderType'] : 'sell';
if ($otype !== 'buy') {
    echo json_encode(array('ok' => false, 'error' => 'Not a buy order.'));
    exit;
}

$buyerName = isset($order['buyerCharacter']) ? $order['buyerCharacter'] : '';
$buyerId = isset($order['buyerCharacterId']) ? $order['buyerCharacterId'] : '';
if (gmi_same_character($character, $characterId, $buyerName, $buyerId)) {
    echo json_encode(array('ok' => false, 'error' => 'You cannot sell to your own buy order.'));
    exit;
}

$needCount = isset($order['count']) ? intval($order['count']) : 1;
if ($needCount < 1) {
    $needCount = 1;
}
$unitPrice = isset($order['unitPrice']) ? intval($order['unitPrice']) : 0;
$minQl = isset($order['minQl']) ? intval($order['minQl']) : 1;
$maxQl = isset($order['maxQl']) ? intval($order['maxQl']) : 200;
$wantLow = isset($order['lowId']) ? intval($order['lowId']) : 0;
$total = $unitPrice * $needCount;
if ($unitPrice <= 0 || $total <= 0) {
    echo json_encode(array('ok' => false, 'error' => 'Invalid buy order price.'));
    exit;
}
$escrow = isset($order['escrow']) ? intval($order['escrow']) : $total;
if ($escrow < $total) {
    $escrow = $total;
}

$seller = gmi_load_vault($character, $characterId);
$matchIndex = -1;
$matchItem = null;

if ($preferIndex >= 0 && $preferIndex < count($seller['items'])) {
    $cand = $seller['items'][$preferIndex];
    $clow = isset($cand['lowId']) ? intval($cand['lowId']) : 0;
    $cql = isset($cand['quality']) ? intval($cand['quality']) : 0;
    $ccnt = isset($cand['count']) ? intval($cand['count']) : 1;
    if ($clow === $wantLow && $cql >= $minQl && $cql <= $maxQl && $ccnt >= $needCount) {
        $matchIndex = $preferIndex;
        $matchItem = $cand;
    }
}

if ($matchIndex < 0) {
    for ($i = 0; $i < count($seller['items']); $i++) {
        $cand = $seller['items'][$i];
        $clow = isset($cand['lowId']) ? intval($cand['lowId']) : 0;
        $cql = isset($cand['quality']) ? intval($cand['quality']) : 0;
        $ccnt = isset($cand['count']) ? intval($cand['count']) : 1;
        if ($clow === $wantLow && $cql >= $minQl && $cql <= $maxQl && $ccnt >= $needCount) {
            $matchIndex = $i;
            $matchItem = $cand;
            break;
        }
    }
}

if ($matchIndex < 0 || !is_array($matchItem)) {
    echo json_encode(array(
        'ok' => false,
        'error' => 'You do not have a matching item in Market Inventory (QL ' . $minQl . '-' . $maxQl . '). Deposit it first.',
    ));
    exit;
}

$have = isset($matchItem['count']) ? intval($matchItem['count']) : 1;
$quality = isset($matchItem['quality']) ? intval($matchItem['quality']) : 1;
$highId = isset($matchItem['highId']) ? intval($matchItem['highId']) : $wantLow;
$itemName = isset($matchItem['name']) ? $matchItem['name'] : (isset($order['name']) ? $order['name'] : 'Item');

if ($needCount >= $have) {
    array_splice($seller['items'], $matchIndex, 1);
} else {
    $seller['items'][$matchIndex]['count'] = $have - $needCount;
}

$seller['credits'] = (isset($seller['credits']) ? intval($seller['credits']) : 0) + $total;

if (!gmi_save_vault($character, $characterId, $seller)) {
    echo json_encode(array('ok' => false, 'error' => 'Failed to update seller vault.'));
    exit;
}

$pendingDir = gmi_data_dir() . DIRECTORY_SEPARATOR . 'pending';
if (!is_dir($pendingDir)) {
    @mkdir($pendingDir, 0777, true);
}

$payload = array(
    'kind' => 'purchase_item',
    'character' => $buyerName,
    'characterId' => $buyerId,
    'preDebited' => 1,
    'count' => $needCount,
    'lowId' => $wantLow,
    'highId' => $highId,
    'quality' => $quality,
    'name' => $itemName,
    'orderId' => isset($order['id']) ? $order['id'] : '',
    'requestedAt' => gmdate('c'),
);

$fname = sprintf(
    'p_%s_%s.json',
    preg_replace('/[^0-9A-Za-z_-]/', '', $buyerId !== '' ? $buyerId : 'x'),
    str_replace('.', '', uniqid('', true))
);
if (@file_put_contents($pendingDir . DIRECTORY_SEPARATOR . $fname, json_encode($payload)) === false) {
    $seller['credits'] = intval($seller['credits']) - $total;
    $seller['items'][] = array(
        'lowId' => $wantLow,
        'highId' => $highId,
        'quality' => $quality,
        'count' => $needCount,
        'icon' => isset($matchItem['icon']) ? intval($matchItem['icon']) : $wantLow,
        'name' => $itemName,
    );
    gmi_save_vault($character, $characterId, $seller);
    echo json_encode(array('ok' => false, 'error' => 'Failed to queue buyer mail.'));
    exit;
}

$refund = $escrow - $total;
if ($refund > 0 && ($buyerName !== '' || $buyerId !== '')) {
    $buyerVault = gmi_load_vault($buyerName, $buyerId);
    $buyerVault['credits'] = (isset($buyerVault['credits']) ? intval($buyerVault['credits']) : 0) + $refund;
    gmi_save_vault($buyerName, $buyerId, $buyerVault);
}

$order['status'] = 'filled';
$order['count'] = 0;
$order['sellerCharacter'] = $character;
$order['sellerCharacterId'] = $characterId;
$order['filledAt'] = gmdate('c');
$order['filledQuality'] = $quality;
gmi_update_order($order);

$notice = 'sold ' . $needCount . ' x ' . $itemName . ' for ' . number_format($total)
    . ' credits (buyer price) — buyer receives item by mail';

gmi_append_log($character, $characterId, array(
    'type' => 'sell',
    'message' => $notice,
    'name' => $itemName,
    'count' => $needCount,
    'unitPrice' => $unitPrice,
    'total' => $total,
    'quality' => $quality,
    'lowId' => $wantLow,
    'highId' => $highId,
    'icon' => isset($matchItem['icon']) ? intval($matchItem['icon']) : $wantLow,
    'otherCharacter' => $buyerName,
    'otherCharacterId' => $buyerId,
));
gmi_append_log($buyerName, $buyerId, array(
    'type' => 'buy',
    'message' => 'purchased ' . $needCount . ' x ' . $itemName . ' for ' . number_format($total)
        . ' credits (buy order filled by ' . ($character !== '' ? $character : 'seller')
        . ') — item arrives by mail',
    'name' => $itemName,
    'count' => $needCount,
    'unitPrice' => $unitPrice,
    'total' => $total,
    'quality' => $quality,
    'lowId' => $wantLow,
    'highId' => $highId,
    'icon' => isset($matchItem['icon']) ? intval($matchItem['icon']) : $wantLow,
    'otherCharacter' => $character,
    'otherCharacterId' => $characterId,
));

echo json_encode(array(
    'ok' => true,
    'credits' => intval($seller['credits']),
    'items' => $seller['items'],
    'character' => $character,
    'characterId' => $characterId,
    'mailQueued' => true,
    'notice' => $notice,
));
