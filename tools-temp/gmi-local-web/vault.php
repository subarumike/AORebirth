<?php
/**
 * Serve THIS character's GMI vault JSON only (MySQL).
 * Identity: X-Anarchy-CharacterID / ?id= / ?name=
 */
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

require_once __DIR__ . DIRECTORY_SEPARATOR . 'gmi_lib.php';

list($name, $charId) = gmi_request_identity();
if ($charId === '' && !empty($_GET['id'])) {
    $charId = preg_replace('/[^0-9A-Za-z_-]/', '', $_GET['id']);
}
if ($name === '' && !empty($_GET['name'])) {
    $name = preg_replace('/[^0-9A-Za-z _.-]/', '', $_GET['name']);
}

if ($charId === '' && $name === '') {
    echo json_encode(array(
        'credits' => 0,
        'items' => array(),
        'character' => '',
        'characterId' => '',
        'note' => 'Character identity missing. Open Market from in-game so vault is per-character.',
    ));
    exit;
}

$vault = gmi_load_vault($name, $charId);
if (!is_array($vault)) {
    echo json_encode(array(
        'credits' => 0,
        'items' => array(),
        'character' => $name,
        'characterId' => $charId,
        'note' => 'No vault for this character yet. Deposit via Deposit to Market, then Refresh.',
    ));
    exit;
}

$credits = isset($vault['credits']) ? intval($vault['credits']) : 0;
$items = isset($vault['items']) && is_array($vault['items']) ? $vault['items'] : array();
$hasData = ($credits > 0) || (count($items) > 0);
$out = array(
    'credits' => $credits,
    'items' => $items,
    'character' => isset($vault['character']) ? $vault['character'] : $name,
    'characterId' => isset($vault['characterId']) ? $vault['characterId'] : $charId,
);
if (!$hasData) {
    $out['note'] = 'No vault for this character yet. Deposit via Deposit to Market, then Refresh.';
}
echo json_encode($out);
