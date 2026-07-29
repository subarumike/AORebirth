<?php
/**
 * List / cancel sell or buy orders.
 * mine=1 → only caller's open orders
 * orderType=sell|buy (optional)
 * lowId=N → filter by item
 * cancel=orderId → cancel own open order (sell returns item; buy refunds escrow)
 */
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

require_once __DIR__ . DIRECTORY_SEPARATOR . 'gmi_lib.php';

list($character, $characterId) = gmi_request_identity();
$mine = !empty($_REQUEST['mine']);
$cancelId = isset($_REQUEST['cancel']) ? $_REQUEST['cancel'] : '';
$orderTypeFilter = isset($_REQUEST['orderType']) ? strtolower(trim($_REQUEST['orderType'])) : '';
$lowIdFilter = isset($_REQUEST['lowId']) ? intval($_REQUEST['lowId']) : 0;

if ($cancelId !== '') {
    if ($character === '' && $characterId === '') {
        echo json_encode(array('ok' => false, 'error' => 'Character identity missing.'));
        exit;
    }
    $order = gmi_find_order($cancelId);
    if (!is_array($order) || $order['status'] !== 'open') {
        echo json_encode(array('ok' => false, 'error' => 'Order not found or not open.'));
        exit;
    }
    $otype = isset($order['orderType']) ? $order['orderType'] : 'sell';

    if ($otype === 'buy') {
        $buyerName = isset($order['buyerCharacter']) ? $order['buyerCharacter'] : '';
        $buyerId = isset($order['buyerCharacterId']) ? $order['buyerCharacterId'] : '';
        if (!gmi_same_character($character, $characterId, $buyerName, $buyerId)) {
            echo json_encode(array('ok' => false, 'error' => 'Not your buy order.'));
            exit;
        }
        $vault = gmi_load_vault($character, $characterId);
        $escrow = isset($order['escrow']) ? intval($order['escrow']) : 0;
        if ($escrow <= 0) {
            $up = isset($order['unitPrice']) ? intval($order['unitPrice']) : 0;
            $cnt = isset($order['count']) ? intval($order['count']) : 1;
            $escrow = $up * $cnt;
        }
        $vault['credits'] = (isset($vault['credits']) ? intval($vault['credits']) : 0) + $escrow;
        gmi_save_vault($character, $characterId, $vault);
        $order['status'] = 'cancelled';
        $order['cancelledAt'] = gmdate('c');
        gmi_update_order($order);
        echo json_encode(array(
            'ok' => true,
            'credits' => intval($vault['credits']),
            'items' => $vault['items'],
            'notice' => 'buy order cancelled — credits returned',
        ));
        exit;
    }

    // sell cancel
    $sellerName = isset($order['sellerCharacter']) ? $order['sellerCharacter'] : '';
    $sellerId = isset($order['sellerCharacterId']) ? $order['sellerCharacterId'] : '';
    if (!gmi_same_character($character, $characterId, $sellerName, $sellerId)) {
        echo json_encode(array('ok' => false, 'error' => 'Not your sell order.'));
        exit;
    }
    $vault = gmi_load_vault($character, $characterId);
    if (count($vault['items']) >= 21) {
        echo json_encode(array('ok' => false, 'error' => 'Market inventory full — free a slot to cancel.'));
        exit;
    }
    $vault['items'][] = array(
        'lowId' => isset($order['lowId']) ? intval($order['lowId']) : 0,
        'highId' => isset($order['highId']) ? intval($order['highId']) : 0,
        'quality' => isset($order['quality']) ? intval($order['quality']) : 1,
        'count' => isset($order['count']) ? intval($order['count']) : 1,
        'icon' => isset($order['icon']) ? intval($order['icon']) : 0,
        'name' => isset($order['name']) ? $order['name'] : 'Item',
    );
    gmi_save_vault($character, $characterId, $vault);
    $order['status'] = 'cancelled';
    $order['cancelledAt'] = gmdate('c');
    gmi_update_order($order);
    echo json_encode(array(
        'ok' => true,
        'credits' => intval($vault['credits']),
        'items' => $vault['items'],
        'notice' => 'sell order cancelled — item returned to inventory',
    ));
    exit;
}

$orders = gmi_list_orders('open');
$now = time();
$filtered = array();
foreach ($orders as $order) {
    if (!empty($order['expiresAt'])) {
        $exp = strtotime($order['expiresAt']);
        if ($exp !== false && $exp < $now) {
            continue;
        }
    }
    $otype = isset($order['orderType']) ? $order['orderType'] : 'sell';
    if ($orderTypeFilter !== '' && $otype !== $orderTypeFilter) {
        continue;
    }
    if ($lowIdFilter > 0) {
        $olow = isset($order['lowId']) ? intval($order['lowId']) : 0;
        if ($olow !== $lowIdFilter) {
            continue;
        }
    }
    if ($mine) {
        if ($character === '' && $characterId === '') {
            echo json_encode(array('ok' => false, 'error' => 'Character identity missing.', 'orders' => array()));
            exit;
        }
        if ($otype === 'buy') {
            $bName = isset($order['buyerCharacter']) ? $order['buyerCharacter'] : '';
            $bId = isset($order['buyerCharacterId']) ? $order['buyerCharacterId'] : '';
            if (!gmi_same_character($character, $characterId, $bName, $bId)) {
                continue;
            }
        } else {
            $sName = isset($order['sellerCharacter']) ? $order['sellerCharacter'] : '';
            $sId = isset($order['sellerCharacterId']) ? $order['sellerCharacterId'] : '';
            if (!gmi_same_character($character, $characterId, $sName, $sId)) {
                continue;
            }
        }
    }
    $filtered[] = gmi_public_order($order);
}

echo json_encode(array(
    'ok' => true,
    'orders' => $filtered,
    'character' => $character,
    'characterId' => $characterId,
));
