<?php
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

// Daily Login claim API — account-wide queue.
// POST arms Zone (pending-latest + claimToken). Zone grants first untaken day and writes result-{token}.json.
// GET ?token=... polls that result (works when AO browser has no CharacterID).

$total = 28;
$month = isset($_GET['month']) ? preg_replace('/[^0-9\-]/', '', $_GET['month']) : '';
$characterId = 0;
$characterName = '';

function daily_read_json_body() {
    $raw = file_get_contents('php://input');
    $body = json_decode($raw ? $raw : '{}', true);
    return is_array($body) ? $body : array();
}

function daily_load_rewards() {
    $paths = array(
        __DIR__ . DIRECTORY_SEPARATOR . 'rewards.json',
        'C:\\xampp\\htdocs\\uwg.daily.icc-rk\\rewards.json',
        'C:\\xampp\\htdocs\\daily\\rewards.json',
    );
    foreach ($paths as $path) {
        if (is_file($path)) {
            $j = json_decode(file_get_contents($path), true);
            if (is_array($j)) {
                return $j;
            }
        }
    }
    return array('freeTestMode' => false, 'days' => array());
}

function daily_safe_key($key) {
    $safe = strtolower(trim(strval($key)));
    if ($safe === '') {
        $safe = 'unknown';
    }
    return preg_replace('/[^a-z0-9:_\\-]+/', '_', $safe);
}

function daily_claim_roots() {
    return array(
        __DIR__ . DIRECTORY_SEPARATOR . 'data' . DIRECTORY_SEPARATOR . 'claims',
        'C:\\xampp\\htdocs\\uwg.daily.icc-rk\\data\\claims',
        'C:\\xampp\\htdocs\\daily\\data\\claims',
    );
}

function daily_ensure_dirs() {
    foreach (daily_claim_roots() as $root) {
        if (!is_dir($root)) {
            @mkdir($root, 0777, true);
        }
    }
}

function daily_write_all($relativeName, $json) {
    foreach (daily_claim_roots() as $root) {
        if (!is_dir($root)) {
            @mkdir($root, 0777, true);
        }
        @file_put_contents($root . DIRECTORY_SEPARATOR . $relativeName, $json);
    }
}

function daily_read_first($relativeName) {
    foreach (daily_claim_roots() as $root) {
        $path = $root . DIRECTORY_SEPARATOR . $relativeName;
        if (is_file($path)) {
            $j = json_decode(file_get_contents($path), true);
            if (is_array($j)) {
                return $j;
            }
        }
    }
    return null;
}

function daily_pdo() {
    static $pdo = null;
    if ($pdo !== null) {
        return $pdo;
    }
    foreach (array('cellao_codex_clean', 'aorebirth', 'cellao', 'ao') as $db) {
        try {
            $pdo = new PDO('mysql:host=127.0.0.1;dbname=' . $db . ';charset=utf8', 'root', '');
            $pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
            return $pdo;
        } catch (Exception $e) {
        }
    }
    return null;
}

function daily_resolve_account_key($characterId, $characterName) {
    $pdo = daily_pdo();
    if ($pdo) {
        try {
            if ($characterId > 0) {
                $st = $pdo->prepare('SELECT Username FROM characters WHERE Id = ? LIMIT 1');
                $st->execute(array($characterId));
                $row = $st->fetch(PDO::FETCH_ASSOC);
                if ($row && !empty($row['Username'])) {
                    return strtolower(trim($row['Username']));
                }
            }
            if ($characterName !== '') {
                $st = $pdo->prepare('SELECT Username FROM characters WHERE Name = ? LIMIT 1');
                $st->execute(array($characterName));
                $row = $st->fetch(PDO::FETCH_ASSOC);
                if ($row && !empty($row['Username'])) {
                    return strtolower(trim($row['Username']));
                }
            }
        } catch (Exception $e) {
        }
    }
    if ($characterId > 0) {
        return 'character:' . $characterId;
    }
    if ($characterName !== '') {
        return 'name:' . strtolower($characterName);
    }
    return '';
}

function daily_empty_state($month) {
    return array(
        'Month' => $month,
        'ClaimedCount' => 0,
        'LastClaimUtc' => '',
        'LastGrantedUtc' => '',
        'CycleCompletedOn' => '',
        'Taken' => array(),
        'LastCharacterId' => 0,
    );
}

function daily_normalize_taken($taken) {
    $ints = array();
    if (!is_array($taken)) {
        return $ints;
    }
    foreach ($taken as $t) {
        $n = intval($t);
        if ($n >= 1 && $n <= 28 && !in_array($n, $ints, true)) {
            $ints[] = $n;
        }
    }
    sort($ints);
    return $ints;
}

function daily_load_account_state($accountKey, $month) {
    if ($accountKey === '') {
        return daily_empty_state($month);
    }
    $state = daily_read_first('account-' . daily_safe_key($accountKey) . '.json');
    if (!is_array($state)) {
        return daily_empty_state($month);
    }
    if (!isset($state['Taken']) || !is_array($state['Taken'])) {
        $state['Taken'] = array();
    }
    $state['Taken'] = daily_normalize_taken($state['Taken']);
    $today = gmdate('Y-m-d');
    $completed = isset($state['CycleCompletedOn']) ? strval($state['CycleCompletedOn']) : '';
    $lastClaim = isset($state['LastClaimUtc']) ? strval($state['LastClaimUtc']) : '';

    if ($completed !== '' && strcmp($today, $completed) > 0) {
        $state = daily_empty_state($month);
        daily_write_all('account-' . daily_safe_key($accountKey) . '.json', json_encode($state));
        return $state;
    }
    if (count($state['Taken']) >= 28 && $lastClaim !== '' && strcmp($today, $lastClaim) > 0) {
        $state = daily_empty_state($month);
        daily_write_all('account-' . daily_safe_key($accountKey) . '.json', json_encode($state));
        return $state;
    }

    $state['Month'] = $month;
    $state['ClaimedCount'] = count($state['Taken']);
    return $state;
}

function daily_write_pending($accountKey, $characterId, $day, $itemId, $amount, $quality, $claimToken) {
    $pending = array(
        'Day' => intval($day),
        'ItemId' => intval($itemId),
        'Amount' => intval($amount),
        'Quality' => intval($quality),
        'CharacterId' => intval($characterId),
        'AccountKey' => strval($accountKey),
        'ClaimToken' => strval($claimToken),
        'RequestedUtc' => gmdate('c'),
    );
    $json = json_encode($pending);
    if ($characterId > 0) {
        daily_write_all('pending-' . intval($characterId) . '.json', $json);
    }
    if ($accountKey !== '') {
        daily_write_all('pending-account-' . daily_safe_key($accountKey) . '.json', $json);
    }
    daily_write_all('pending-latest.json', $json);
}

function daily_read_result($claimToken) {
    $safe = daily_safe_key($claimToken);
    if ($safe === '' || $safe === 'unknown') {
        return null;
    }
    return daily_read_first('result-' . $safe . '.json');
}

function daily_next_queue_day($takenInts) {
    $takenInts = daily_normalize_taken($takenInts);
    for ($d = 1; $d <= 28; $d++) {
        if (!in_array($d, $takenInts, true)) {
            return $d;
        }
    }
    return 0;
}

function daily_append_test_log($row) {
    $line = json_encode($row) . "\n";
    foreach (daily_claim_roots() as $root) {
        if (!is_dir($root)) {
            @mkdir($root, 0777, true);
        }
        @file_put_contents($root . DIRECTORY_SEPARATOR . 'test-grants.jsonl', $line, FILE_APPEND);
    }
}

$body = array();
if (isset($_SERVER['REQUEST_METHOD']) && $_SERVER['REQUEST_METHOD'] === 'POST') {
    $body = daily_read_json_body();
}

if ($month === '' && isset($body['month'])) {
    $month = preg_replace('/[^0-9\-]/', '', $body['month']);
}
if ($month === '' || !preg_match('/^\d{4}-\d{2}$/', $month)) {
    $month = gmdate('Y-m');
}

if (isset($_GET['characterId'])) {
    $characterId = intval($_GET['characterId']);
}
if (isset($_GET['character'])) {
    $characterName = trim(strval($_GET['character']));
}
if (isset($body['characterId'])) {
    $characterId = intval($body['characterId']);
}
if (isset($body['character'])) {
    $characterName = trim(strval($body['character']));
}

$rewards = daily_load_rewards();
$freeTest = !empty($rewards['freeTestMode']) ? true : false;

daily_ensure_dirs();

$accountKey = daily_resolve_account_key($characterId, $characterName);
$hasIdentity = ($accountKey !== '');
$state = $hasIdentity ? daily_load_account_state($accountKey, $month) : daily_empty_state($month);
$takenInts = daily_normalize_taken(isset($state['Taken']) ? $state['Taken'] : array());

$payload = array(
    'ok' => true,
    'month' => $month,
    'accountKey' => $accountKey,
    'hasIdentity' => $hasIdentity,
    'freeTestMode' => $freeTest,
    'claimedCount' => count($takenInts),
    'lastClaimUtc' => isset($state['LastClaimUtc']) ? strval($state['LastClaimUtc']) : '',
    'lastGrantedUtc' => isset($state['LastGrantedUtc']) ? strval($state['LastGrantedUtc']) : '',
    'cycleCompletedOn' => isset($state['CycleCompletedOn']) ? strval($state['CycleCompletedOn']) : '',
    'taken' => $takenInts,
    'nextDay' => daily_next_queue_day($takenInts),
    'claimedToday' => (isset($state['LastGrantedUtc']) && $state['LastGrantedUtc'] === gmdate('Y-m-d')),
    'grantedToday' => (isset($state['LastGrantedUtc']) && $state['LastGrantedUtc'] === gmdate('Y-m-d')),
);

if (!isset($_SERVER['REQUEST_METHOD']) || $_SERVER['REQUEST_METHOD'] === 'GET') {
    $pollToken = '';
    if (isset($_GET['token'])) {
        $pollToken = preg_replace('/[^a-zA-Z0-9_\\-]+/', '', strval($_GET['token']));
    }
    if ($pollToken !== '') {
        $result = daily_read_result($pollToken);
        if (is_array($result)) {
            echo json_encode($result);
        } else {
            echo json_encode(array('ok' => null, 'pending' => true, 'message' => 'Waiting for game server...'));
        }
        exit;
    }
    echo json_encode($payload);
    exit;
}

$claimToken = '';
if (isset($body['claimToken'])) {
    $claimToken = preg_replace('/[^a-zA-Z0-9_\\-]+/', '', strval($body['claimToken']));
}
if ($claimToken === '') {
    $claimToken = 'c' . gmdate('YmdHis') . substr(md5(uniqid('', true)), 0, 8);
}

if ($hasIdentity && !empty($payload['claimedToday'])) {
    echo json_encode(array(
        'ok' => false,
        'message' => 'You already claimed today on this account. Come back tomorrow.',
        'taken' => $takenInts,
        'accountKey' => $accountKey,
        'claimedToday' => true,
        'nextDay' => daily_next_queue_day($takenInts),
        'claimToken' => $claimToken
    ));
    exit;
}

// Arm Zone. Dummy day/item — Zone grants the first untaken day for this account.
daily_write_pending($accountKey, $characterId, 1, 1, 1, 1, $claimToken);

daily_append_test_log(array(
    'utc' => gmdate('c'),
    'source' => 'claim.php',
    'accountKey' => $accountKey,
    'characterId' => $characterId,
    'characterName' => $characterName,
    'day' => 0,
    'itemId' => 0,
    'itemName' => 'pending-next-untaken',
    'claimToken' => $claimToken,
    'freeTestMode' => $freeTest
));

echo json_encode(array(
    'ok' => true,
    'armed' => true,
    'message' => 'Claim armed. Granting next available reward...',
    'month' => $month,
    'accountKey' => $accountKey,
    'hasIdentity' => $hasIdentity,
    'claimToken' => $claimToken,
    'freeTestMode' => $freeTest,
    'taken' => $takenInts,
    'claimedCount' => count($takenInts),
    'nextDay' => $hasIdentity ? daily_next_queue_day($takenInts) : 0,
    'claimedToday' => !empty($payload['claimedToday'])
));
