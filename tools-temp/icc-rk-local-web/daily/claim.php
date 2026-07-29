<?php
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
// Local stub only. Live AO claims via CharacterAction 0x107 to the game server.
echo json_encode(array(
    'ok' => true,
    'message' => '1 rewards claimed.',
    'host' => 'uwg.daily.icc-rk',
    'note' => 'AO Rebirth localhost stub'
));
