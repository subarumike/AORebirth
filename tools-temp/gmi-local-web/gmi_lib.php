<?php
/**
 * Shared GMI helpers — MySQL vault / orders / trade log (pending mail still JSON files).
 */

require_once __DIR__ . DIRECTORY_SEPARATOR . 'gmi_db.php';

function gmi_data_dir() {
    $dir = __DIR__ . DIRECTORY_SEPARATOR . 'data';
    if (!is_dir($dir)) {
        @mkdir($dir, 0777, true);
    }
    return $dir;
}

function gmi_orders_dir() {
    $dir = gmi_data_dir() . DIRECTORY_SEPARATOR . 'orders';
    if (!is_dir($dir)) {
        @mkdir($dir, 0777, true);
    }
    return $dir;
}

function gmi_logs_dir() {
    $dir = gmi_data_dir() . DIRECTORY_SEPARATOR . 'logs';
    if (!is_dir($dir)) {
        @mkdir($dir, 0777, true);
    }
    return $dir;
}

function gmi_strip_bom($raw) {
    if ($raw === false || $raw === null) {
        return $raw;
    }
    if (strlen($raw) >= 3 && ord($raw[0]) === 0xEF && ord($raw[1]) === 0xBB && ord($raw[2]) === 0xBF) {
        return substr($raw, 3);
    }
    return $raw;
}

function gmi_expand_char_ids($charId) {
    $ids = array();
    if ($charId === '' || $charId === null) {
        return $ids;
    }
    $ids[] = $charId;
    $ids[] = strtoupper($charId);
    $ids[] = strtolower($charId);
    if (ctype_digit($charId)) {
        $ids[] = strtoupper(dechex(intval($charId, 10)));
        $ids[] = strtolower(dechex(intval($charId, 10)));
    } elseif (preg_match('/^[0-9A-Fa-f]+$/', $charId)) {
        $ids[] = strval(hexdec($charId));
    }
    return array_values(array_unique($ids));
}

function gmi_safe_name($name) {
    $safe = strtolower(str_replace(' ', '_', $name));
    return preg_replace('/[^0-9a-z_.-]/', '', $safe);
}

function gmi_read_json_file($path) {
    if (!is_file($path)) {
        return null;
    }
    $raw = gmi_strip_bom(@file_get_contents($path));
    if ($raw === false || $raw === '') {
        return null;
    }
    $data = json_decode($raw, true);
    return is_array($data) ? $data : null;
}

function gmi_request_identity() {
    $character = '';
    $characterId = '';
    if (!empty($_SERVER['HTTP_X_ANARCHY_CHARACTERID'])) {
        $characterId = preg_replace('/[^0-9A-Za-z_-]/', '', $_SERVER['HTTP_X_ANARCHY_CHARACTERID']);
    }
    if ($characterId === '' && !empty($_REQUEST['characterId'])) {
        $characterId = preg_replace('/[^0-9A-Za-z_-]/', '', $_REQUEST['characterId']);
    }
    if ($characterId === '' && !empty($_REQUEST['id'])) {
        $characterId = preg_replace('/[^0-9A-Za-z_-]/', '', $_REQUEST['id']);
    }
    if (!empty($_SERVER['HTTP_X_ANARCHY_CHARACTERNAME'])) {
        $character = preg_replace('/[^0-9A-Za-z _.-]/', '', $_SERVER['HTTP_X_ANARCHY_CHARACTERNAME']);
    }
    if ($character === '' && !empty($_REQUEST['character'])) {
        $character = preg_replace('/[^0-9A-Za-z _.-]/', '', $_REQUEST['character']);
    }
    if ($character === '' && !empty($_REQUEST['name'])) {
        $character = preg_replace('/[^0-9A-Za-z _.-]/', '', $_REQUEST['name']);
    }
    return array($character, $characterId);
}

function gmi_id_matches($fileId, $requestIds) {
    if ($fileId === '' || empty($requestIds)) {
        return false;
    }
    foreach (gmi_expand_char_ids($fileId) as $a) {
        foreach ($requestIds as $b) {
            if (strcasecmp($a, $b) === 0) {
                return true;
            }
        }
    }
    return false;
}

function gmi_same_character($aName, $aId, $bName, $bId) {
    $ai = gmi_char_id_int($aId);
    $bi = gmi_char_id_int($bId);
    if ($ai > 0 && $bi > 0) {
        return $ai === $bi;
    }
    if ($aId !== '' && $bId !== '') {
        return gmi_id_matches($aId, gmi_expand_char_ids($bId));
    }
    if ($aName !== '' && $bName !== '') {
        return strcasecmp(gmi_safe_name($aName), gmi_safe_name($bName)) === 0;
    }
    return false;
}

function gmi_resolve_character_id($character, $characterId) {
    $cid = gmi_char_id_int($characterId);
    if ($cid > 0) {
        return $cid;
    }
    if ($character === '') {
        return 0;
    }
    try {
        $db = gmi_db();
        $stmt = $db->prepare('SELECT character_id FROM gmi_vault WHERE character_name = ? LIMIT 1');
        if (!$stmt) {
            return 0;
        }
        $stmt->bind_param('s', $character);
        $stmt->execute();
        $res = $stmt->get_result();
        $row = $res ? $res->fetch_assoc() : null;
        $stmt->close();
        if ($row && isset($row['character_id'])) {
            return intval($row['character_id']);
        }
    } catch (Exception $e) {
    }
    return 0;
}

function gmi_append_log($character, $characterId, $fields) {
    if (!is_array($fields)) {
        return false;
    }
    try {
        $db = gmi_db();
        $cid = gmi_char_id_int($characterId);
        $cname = $character !== '' ? $character : '';
        $otype = isset($fields['type']) ? substr((string)$fields['type'], 0, 16) : 'trade';
        $msg = isset($fields['message']) ? substr((string)$fields['message'], 0, 512) : '';
        $iname = isset($fields['name']) ? substr((string)$fields['name'], 0, 128) : '';
        $cnt = isset($fields['count']) ? intval($fields['count']) : 0;
        $up = isset($fields['unitPrice']) ? intval($fields['unitPrice']) : 0;
        $tot = isset($fields['total']) ? intval($fields['total']) : 0;
        $ql = isset($fields['quality']) ? intval($fields['quality']) : 0;
        $low = isset($fields['lowId']) ? intval($fields['lowId']) : 0;
        $high = isset($fields['highId']) ? intval($fields['highId']) : 0;
        $icon = isset($fields['icon']) ? intval($fields['icon']) : 0;
        $otherName = isset($fields['otherCharacter']) ? substr((string)$fields['otherCharacter'], 0, 32) : '';
        $otherId = gmi_char_id_int(isset($fields['otherCharacterId']) ? $fields['otherCharacterId'] : '');
        $stmt = $db->prepare(
            'INSERT INTO gmi_trade_log
            (character_id, character_name, other_character_id, other_character_name,
             event_type, message, item_name, low_id, high_id, icon, quality,
             count, unit_price, total)
             VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
        );
        if (!$stmt) {
            return false;
        }
        $stmt->bind_param(
            'isissssiiiiiii',
            $cid,
            $cname,
            $otherId,
            $otherName,
            $otype,
            $msg,
            $iname,
            $low,
            $high,
            $icon,
            $ql,
            $cnt,
            $up,
            $tot
        );
        $ok = $stmt->execute();
        $stmt->close();
        return $ok;
    } catch (Exception $e) {
        return false;
    }
}

function gmi_read_logs($character, $characterId, $limit) {
    $limit = intval($limit);
    if ($limit <= 0) {
        $limit = 100;
    }
    $entries = array();
    try {
        $db = gmi_db();
        $cid = gmi_char_id_int($characterId);
        if ($cid > 0) {
            $stmt = $db->prepare(
                'SELECT * FROM gmi_trade_log WHERE character_id = ? ORDER BY created_at DESC, id DESC LIMIT ?'
            );
            $stmt->bind_param('ii', $cid, $limit);
        } elseif ($character !== '') {
            $stmt = $db->prepare(
                'SELECT * FROM gmi_trade_log WHERE character_name = ? ORDER BY created_at DESC, id DESC LIMIT ?'
            );
            $stmt->bind_param('si', $character, $limit);
        } else {
            return $entries;
        }
        $stmt->execute();
        $res = $stmt->get_result();
        while ($res && ($row = $res->fetch_assoc())) {
            $created = isset($row['created_at']) ? $row['created_at'] : '';
            $ts = $created !== '' ? strtotime($created . ' UTC') : false;
            $at = $ts ? gmdate('c', $ts) : gmdate('c');
            $entries[] = array(
                'at' => $at,
                'date' => $created !== '' ? ($created . ' UTC') : (gmdate('Y-m-d H:i:s') . ' UTC'),
                'type' => isset($row['event_type']) ? $row['event_type'] : 'trade',
                'message' => isset($row['message']) ? $row['message'] : '',
                'name' => isset($row['item_name']) ? $row['item_name'] : '',
                'count' => isset($row['count']) ? intval($row['count']) : 0,
                'unitPrice' => isset($row['unit_price']) ? intval($row['unit_price']) : 0,
                'total' => isset($row['total']) ? intval($row['total']) : 0,
                'quality' => isset($row['quality']) ? intval($row['quality']) : 0,
                'lowId' => isset($row['low_id']) ? intval($row['low_id']) : 0,
                'highId' => isset($row['high_id']) ? intval($row['high_id']) : 0,
                'icon' => isset($row['icon']) ? intval($row['icon']) : 0,
                'character' => isset($row['character_name']) ? $row['character_name'] : $character,
                'characterId' => isset($row['character_id']) ? strval($row['character_id']) : $characterId,
                'otherCharacter' => isset($row['other_character_name']) ? $row['other_character_name'] : '',
                'otherCharacterId' => isset($row['other_character_id']) ? strval($row['other_character_id']) : '',
            );
        }
        $stmt->close();
    } catch (Exception $e) {
    }
    return $entries;
}

function gmi_load_vault($character, $characterId) {
    $cid = gmi_resolve_character_id($character, $characterId);
    $empty = array(
        'character' => $character,
        'characterId' => $characterId !== '' ? $characterId : ($cid > 0 ? strval($cid) : ''),
        'credits' => 0,
        'items' => array(),
    );
    if ($cid <= 0 && $character === '') {
        return $empty;
    }
    try {
        $db = gmi_db();
        $vault = $empty;
        if ($cid > 0) {
            $stmt = $db->prepare('SELECT character_id, character_name, credits FROM gmi_vault WHERE character_id = ? LIMIT 1');
            $stmt->bind_param('i', $cid);
            $stmt->execute();
            $res = $stmt->get_result();
            $row = $res ? $res->fetch_assoc() : null;
            $stmt->close();
            if (!$row) {
                return $vault;
            }
            $vault['character'] = $character !== '' ? $character : (isset($row['character_name']) ? $row['character_name'] : '');
            $vault['characterId'] = $characterId !== '' ? $characterId : strval(intval($row['character_id']));
            $vault['credits'] = isset($row['credits']) ? intval($row['credits']) : 0;
            $stmt = $db->prepare(
                'SELECT low_id, high_id, quality, stack_count, icon, item_name, slot_index
                 FROM gmi_vault_item WHERE character_id = ? ORDER BY slot_index ASC, id ASC'
            );
            $stmt->bind_param('i', $cid);
            $stmt->execute();
            $res = $stmt->get_result();
            $items = array();
            while ($res && ($ir = $res->fetch_assoc())) {
                $items[] = array(
                    'lowId' => intval($ir['low_id']),
                    'highId' => intval($ir['high_id']),
                    'quality' => intval($ir['quality']),
                    'count' => intval($ir['stack_count']),
                    'icon' => intval($ir['icon']),
                    'name' => isset($ir['item_name']) ? $ir['item_name'] : '',
                );
            }
            $stmt->close();
            $vault['items'] = $items;
            return $vault;
        }
    } catch (Exception $e) {
    }
    return $empty;
}

function gmi_save_vault($character, $characterId, $vault) {
    $cid = gmi_resolve_character_id($character, $characterId);
    if ($cid <= 0) {
        $cid = gmi_char_id_int($characterId);
    }
    if ($cid <= 0) {
        return false;
    }
    if (!is_array($vault)) {
        return false;
    }
    $credits = isset($vault['credits']) ? intval($vault['credits']) : 0;
    $name = $character !== '' ? $character : (isset($vault['character']) ? $vault['character'] : '');
    $items = isset($vault['items']) && is_array($vault['items']) ? $vault['items'] : array();
    try {
        $db = gmi_db();
        gmi_db_begin();
        $stmt = $db->prepare(
            'INSERT INTO gmi_vault (character_id, character_name, credits)
             VALUES (?,?,?)
             ON DUPLICATE KEY UPDATE character_name = VALUES(character_name), credits = VALUES(credits)'
        );
        $stmt->bind_param('isi', $cid, $name, $credits);
        if (!$stmt->execute()) {
            $stmt->close();
            gmi_db_rollback();
            return false;
        }
        $stmt->close();

        $del = $db->prepare('DELETE FROM gmi_vault_item WHERE character_id = ?');
        $del->bind_param('i', $cid);
        if (!$del->execute()) {
            $del->close();
            gmi_db_rollback();
            return false;
        }
        $del->close();

        if (count($items) > 0) {
            $ins = $db->prepare(
                'INSERT INTO gmi_vault_item
                 (character_id, low_id, high_id, quality, stack_count, icon, item_name, slot_index)
                 VALUES (?,?,?,?,?,?,?,?)'
            );
            $slot = 0;
            foreach ($items as $item) {
                $low = isset($item['lowId']) ? intval($item['lowId']) : 0;
                $high = isset($item['highId']) ? intval($item['highId']) : $low;
                $ql = isset($item['quality']) ? intval($item['quality']) : 1;
                $cnt = isset($item['count']) ? intval($item['count']) : 1;
                $icon = isset($item['icon']) ? intval($item['icon']) : $low;
                $iname = isset($item['name']) ? substr((string)$item['name'], 0, 128) : '';
                $ins->bind_param('iiiiiisi', $cid, $low, $high, $ql, $cnt, $icon, $iname, $slot);
                if (!$ins->execute()) {
                    $ins->close();
                    gmi_db_rollback();
                    return false;
                }
                $slot++;
            }
            $ins->close();
        }
        gmi_db_commit();
        return true;
    } catch (Exception $e) {
        gmi_db_rollback();
        return false;
    }
}

function gmi_order_from_row($row) {
    if (!is_array($row)) {
        return null;
    }
    $otype = isset($row['order_type']) ? $row['order_type'] : 'sell';
    $qmin = isset($row['quality_min']) ? intval($row['quality_min']) : 1;
    $qmax = isset($row['quality_max']) ? intval($row['quality_max']) : $qmin;
    $qty = isset($row['quantity_remaining']) ? intval($row['quantity_remaining']) : intval($row['quantity']);
    $order = array(
        'id' => strval($row['id']),
        'orderType' => $otype,
        'status' => isset($row['status']) ? $row['status'] : 'open',
        'sellerCharacter' => isset($row['seller_character_name']) ? $row['seller_character_name'] : '',
        'sellerCharacterId' => isset($row['seller_character_id']) && intval($row['seller_character_id']) > 0
            ? strval($row['seller_character_id']) : '',
        'buyerCharacter' => isset($row['buyer_character_name']) ? $row['buyer_character_name'] : '',
        'buyerCharacterId' => isset($row['buyer_character_id']) && $row['buyer_character_id'] !== null
            && intval($row['buyer_character_id']) > 0 ? strval($row['buyer_character_id']) : '',
        'lowId' => isset($row['low_id']) ? intval($row['low_id']) : 0,
        'highId' => isset($row['high_id']) ? intval($row['high_id']) : 0,
        'quality' => $otype === 'buy' ? $qmin : $qmin,
        'minQl' => $qmin,
        'maxQl' => $qmax,
        'count' => $qty,
        'icon' => isset($row['icon']) ? intval($row['icon']) : 0,
        'name' => isset($row['item_name']) ? $row['item_name'] : '',
        'unitPrice' => isset($row['unit_price']) ? intval($row['unit_price']) : 0,
        'createdAt' => isset($row['created_at']) ? gmdate('c', strtotime($row['created_at'] . ' UTC')) : gmdate('c'),
        'expiresAt' => isset($row['expires_at']) && $row['expires_at'] !== null
            ? gmdate('c', strtotime($row['expires_at'] . ' UTC')) : null,
    );
    if ($otype === 'buy') {
        $order['escrow'] = intval($order['unitPrice']) * intval($order['count']);
    }
    return $order;
}

function gmi_list_orders($statusFilter) {
    $out = array();
    try {
        $db = gmi_db();
        if ($statusFilter !== '') {
            $stmt = $db->prepare('SELECT * FROM gmi_order WHERE status = ? ORDER BY created_at DESC, id DESC');
            $stmt->bind_param('s', $statusFilter);
            $stmt->execute();
            $res = $stmt->get_result();
        } else {
            $res = $db->query('SELECT * FROM gmi_order ORDER BY created_at DESC, id DESC');
            $stmt = null;
        }
        while ($res && ($row = $res->fetch_assoc())) {
            $order = gmi_order_from_row($row);
            if ($order !== null) {
                $out[] = $order;
            }
        }
        if ($stmt) {
            $stmt->close();
        }
    } catch (Exception $e) {
    }
    return $out;
}

function gmi_find_order($orderId) {
    $id = intval(preg_replace('/[^0-9]/', '', (string)$orderId));
    if ($id <= 0) {
        return null;
    }
    try {
        $db = gmi_db();
        $stmt = $db->prepare('SELECT * FROM gmi_order WHERE id = ? LIMIT 1');
        $stmt->bind_param('i', $id);
        $stmt->execute();
        $res = $stmt->get_result();
        $row = $res ? $res->fetch_assoc() : null;
        $stmt->close();
        return gmi_order_from_row($row);
    } catch (Exception $e) {
        return null;
    }
}

function gmi_public_order($order) {
    if (!is_array($order)) {
        return null;
    }
    unset($order['_path']);
    return $order;
}

/**
 * Insert open order; returns new id (int) or 0 on failure.
 * $fields: orderType, sellerCharacter, sellerCharacterId, buyerCharacter, buyerCharacterId,
 * lowId, highId, quality|minQl|maxQl, count, icon, name, unitPrice, expiresAt (ISO optional)
 */
function gmi_create_order($fields) {
    if (!is_array($fields)) {
        return 0;
    }
    $otype = isset($fields['orderType']) ? $fields['orderType'] : 'sell';
    if ($otype !== 'buy' && $otype !== 'sell') {
        $otype = 'sell';
    }
    $sellerId = gmi_char_id_int(isset($fields['sellerCharacterId']) ? $fields['sellerCharacterId'] : '');
    $buyerId = gmi_char_id_int(isset($fields['buyerCharacterId']) ? $fields['buyerCharacterId'] : '');
    $sellerName = isset($fields['sellerCharacter']) ? substr((string)$fields['sellerCharacter'], 0, 32) : '';
    $buyerName = isset($fields['buyerCharacter']) ? substr((string)$fields['buyerCharacter'], 0, 32) : '';
    $low = isset($fields['lowId']) ? intval($fields['lowId']) : 0;
    $high = isset($fields['highId']) ? intval($fields['highId']) : $low;
    $qmin = isset($fields['minQl']) ? intval($fields['minQl']) : (isset($fields['quality']) ? intval($fields['quality']) : 1);
    $qmax = isset($fields['maxQl']) ? intval($fields['maxQl']) : $qmin;
    $qty = isset($fields['count']) ? intval($fields['count']) : 1;
    $icon = isset($fields['icon']) ? intval($fields['icon']) : $low;
    $iname = isset($fields['name']) ? substr((string)$fields['name'], 0, 128) : '';
    $price = isset($fields['unitPrice']) ? intval($fields['unitPrice']) : 0;
    $expires = null;
    if (!empty($fields['expiresAt'])) {
        $ts = strtotime($fields['expiresAt']);
        if ($ts !== false) {
            $expires = gmdate('Y-m-d H:i:s', $ts);
        }
    }
    if ($expires === null) {
        $expires = gmdate('Y-m-d H:i:s', time() + 7 * 86400);
    }
    // buy orders: no seller yet
    if ($otype === 'buy' && $sellerId <= 0) {
        $sellerId = 0;
    }
    $buyerIdParam = $buyerId > 0 ? $buyerId : 0;
    try {
        $db = gmi_db();
        $status = 'open';
        $stmt = $db->prepare(
            'INSERT INTO gmi_order
             (seller_character_id, seller_character_name, buyer_character_id, buyer_character_name,
              order_type, status, low_id, high_id, item_name, icon, quality_min, quality_max,
              unit_price, quantity, quantity_remaining, expires_at)
             VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
        );
        // i s i s s s i i s i i i i i i s
        $stmt->bind_param(
            'isisssiisiiiiiis',
            $sellerId,
            $sellerName,
            $buyerIdParam,
            $buyerName,
            $otype,
            $status,
            $low,
            $high,
            $iname,
            $icon,
            $qmin,
            $qmax,
            $price,
            $qty,
            $qty,
            $expires
        );
        if (!$stmt->execute()) {
            $stmt->close();
            return 0;
        }
        $newId = intval($stmt->insert_id);
        $stmt->close();
        return $newId;
    } catch (Exception $e) {
        return 0;
    }
}

/**
 * Update order fields after fill/cancel. $fields keys match gmi_order_from_row output where possible.
 */
function gmi_update_order($order) {
    if (!is_array($order) || empty($order['id'])) {
        return false;
    }
    $id = intval($order['id']);
    if ($id <= 0) {
        return false;
    }
    $status = isset($order['status']) ? $order['status'] : 'open';
    $qty = isset($order['count']) ? intval($order['count']) : 0;
    $sellerId = gmi_char_id_int(isset($order['sellerCharacterId']) ? $order['sellerCharacterId'] : '');
    $buyerId = gmi_char_id_int(isset($order['buyerCharacterId']) ? $order['buyerCharacterId'] : '');
    $sellerName = isset($order['sellerCharacter']) ? substr((string)$order['sellerCharacter'], 0, 32) : '';
    $buyerName = isset($order['buyerCharacter']) ? substr((string)$order['buyerCharacter'], 0, 32) : '';
    $buyerIdParam = $buyerId > 0 ? $buyerId : 0;
    try {
        $db = gmi_db();
        $stmt = $db->prepare(
            'UPDATE gmi_order SET
             status = ?,
             quantity_remaining = ?,
             seller_character_id = ?,
             seller_character_name = ?,
             buyer_character_id = ?,
             buyer_character_name = ?
             WHERE id = ?'
        );
        $stmt->bind_param(
            'siisisi',
            $status,
            $qty,
            $sellerId,
            $sellerName,
            $buyerIdParam,
            $buyerName,
            $id
        );
        $ok = $stmt->execute();
        $stmt->close();
        return $ok;
    } catch (Exception $e) {
        return false;
    }
}
