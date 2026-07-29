<?php
/**
 * Statistics from gmi_trade_log + open gmi_order rows.
 * Sales (left)  = units sold this month (log type=sell)
 * Buys (right)  = units bought this month (log type=buy)
 * Also returns open sell/buy listing counts.
 */
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

require_once __DIR__ . DIRECTORY_SEPARATOR . 'gmi_lib.php';

$monthAgo = time() - (30 * 86400);
$byKey = array();

function gmi_stats_key($lowId, $name) {
    $low = intval($lowId);
    if ($low > 0) {
        return 'id:' . $low;
    }
    $n = strtolower(trim($name));
    return 'name:' . $n;
}

function gmi_stats_row(&$byKey, $key, $name, $icon, $low, $high) {
    if (!isset($byKey[$key])) {
        $byKey[$key] = array(
            'name' => $name !== '' ? $name : ('Item ' . $low),
            'icon' => intval($icon),
            'lowId' => intval($low),
            'highId' => intval($high) > 0 ? intval($high) : intval($low),
            'open' => 0,
            'openSell' => 0,
            'openBuy' => 0,
            'sales' => 0,
            'orders' => 0,
            'monthSales' => 0,
            'monthBuys' => 0,
        );
    } else {
        if ($name !== '') {
            $byKey[$key]['name'] = $name;
        }
        if (intval($icon) > 0) {
            $byKey[$key]['icon'] = intval($icon);
        }
        if (intval($low) > 0) {
            $byKey[$key]['lowId'] = intval($low);
        }
        if (intval($high) > 0) {
            $byKey[$key]['highId'] = intval($high);
        }
    }
}

try {
    $db = gmi_db();
    $since = gmdate('Y-m-d H:i:s', $monthAgo);
    $stmt = $db->prepare(
        "SELECT event_type, item_name, low_id, high_id, icon, count
         FROM gmi_trade_log
         WHERE event_type IN ('sell','buy') AND created_at >= ?"
    );
    $stmt->bind_param('s', $since);
    $stmt->execute();
    $res = $stmt->get_result();
    while ($res && ($row = $res->fetch_assoc())) {
        $type = isset($row['event_type']) ? $row['event_type'] : '';
        $name = isset($row['item_name']) ? $row['item_name'] : '';
        $low = isset($row['low_id']) ? intval($row['low_id']) : 0;
        $high = isset($row['high_id']) ? intval($row['high_id']) : $low;
        $icon = isset($row['icon']) ? intval($row['icon']) : $low;
        $cnt = isset($row['count']) ? intval($row['count']) : 1;
        if ($cnt < 1) {
            $cnt = 1;
        }
        if ($name === '' && $low <= 0) {
            continue;
        }
        $key = gmi_stats_key($low, $name);
        gmi_stats_row($byKey, $key, $name, $icon, $low, $high);
        if ($type === 'sell') {
            $byKey[$key]['monthSales'] += $cnt;
            $byKey[$key]['sales'] += $cnt;
        } else {
            $byKey[$key]['monthBuys'] += $cnt;
        }
    }
    $stmt->close();
} catch (Exception $e) {
}

foreach ($byKey as $k => &$row) {
    if ($row['monthSales'] <= 0 && $row['monthBuys'] > 0) {
        $row['monthSales'] = $row['monthBuys'];
        $row['sales'] = $row['monthBuys'];
    }
}
unset($row);

foreach (gmi_list_orders('open') as $order) {
    $low = isset($order['lowId']) ? intval($order['lowId']) : 0;
    $high = isset($order['highId']) ? intval($order['highId']) : $low;
    $name = isset($order['name']) ? $order['name'] : ('Item ' . $low);
    $icon = isset($order['icon']) ? intval($order['icon']) : $low;
    $count = isset($order['count']) ? intval($order['count']) : 1;
    if ($count < 1) {
        $count = 1;
    }
    $otype = isset($order['orderType']) ? $order['orderType'] : 'sell';
    $key = gmi_stats_key($low, $name);
    gmi_stats_row($byKey, $key, $name, $icon, $low, $high);
    if ($otype === 'buy') {
        $byKey[$key]['openBuy'] += $count;
        $byKey[$key]['open'] += $count;
        $byKey[$key]['orders'] += 1;
    } else {
        $byKey[$key]['openSell'] += $count;
        $byKey[$key]['open'] += $count;
    }
}

$all = array_values($byKey);

$sellList = $all;
usort($sellList, function ($a, $b) {
    $sa = isset($a['monthSales']) ? intval($a['monthSales']) : 0;
    $sb = isset($b['monthSales']) ? intval($b['monthSales']) : 0;
    if ($sa === $sb) {
        $oa = isset($a['openSell']) ? intval($a['openSell']) : 0;
        $ob = isset($b['openSell']) ? intval($b['openSell']) : 0;
        if ($oa === $ob) {
            return 0;
        }
        return ($oa < $ob) ? 1 : -1;
    }
    return ($sa < $sb) ? 1 : -1;
});
$sellOut = array();
foreach ($sellList as $r) {
    if ((isset($r['monthSales']) && $r['monthSales'] > 0)
        || (isset($r['openSell']) && $r['openSell'] > 0)) {
        $sellOut[] = $r;
    }
}

$buyList = $all;
usort($buyList, function ($a, $b) {
    $sa = isset($a['monthBuys']) ? intval($a['monthBuys']) : 0;
    $sb = isset($b['monthBuys']) ? intval($b['monthBuys']) : 0;
    if ($sa === $sb) {
        $oa = isset($a['openBuy']) ? intval($a['openBuy']) : 0;
        $ob = isset($b['openBuy']) ? intval($b['openBuy']) : 0;
        if ($oa === $ob) {
            return 0;
        }
        return ($oa < $ob) ? 1 : -1;
    }
    return ($sa < $sb) ? 1 : -1;
});
$buyOut = array();
foreach ($buyList as $r) {
    if ((isset($r['monthBuys']) && $r['monthBuys'] > 0)
        || (isset($r['openBuy']) && $r['openBuy'] > 0)) {
        $buyOut[] = $r;
    }
}

echo json_encode(array(
    'ok' => true,
    'sell' => array_slice($sellOut, 0, 50),
    'buy' => array_slice($buyOut, 0, 50),
));
