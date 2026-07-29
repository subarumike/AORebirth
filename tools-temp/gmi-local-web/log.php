<?php
/**
 * Recent buy/sell log lines for the caller's character.
 */
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

require_once __DIR__ . DIRECTORY_SEPARATOR . 'gmi_lib.php';

list($character, $characterId) = gmi_request_identity();
if ($character === '' && $characterId === '') {
    echo json_encode(array('ok' => false, 'error' => 'Character identity missing.', 'logs' => array()));
    exit;
}

$limit = isset($_REQUEST['limit']) ? intval($_REQUEST['limit']) : 100;
$logs = gmi_read_logs($character, $characterId, $limit);

echo json_encode(array(
    'ok' => true,
    'logs' => $logs,
    'character' => $character,
    'characterId' => $characterId,
));
