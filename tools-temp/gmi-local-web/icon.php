<?php
/**
 * Serve GMI item icons from icons/{id}.png (or ITEM {id}.png / ITEM{id}.png).
 * Falls back to ?low= item LowID, then an SVG badge.
 */
header('Cache-Control: public, max-age=86400');

function gmi_serve_icon_file($path) {
    $ext = strtolower(pathinfo($path, PATHINFO_EXTENSION));
    $types = array(
        'png' => 'image/png',
        'gif' => 'image/gif',
        'jpg' => 'image/jpeg',
        'jpeg' => 'image/jpeg',
    );
    if (!isset($types[$ext]) || !is_file($path)) {
        return false;
    }
    header('Content-Type: ' . $types[$ext]);
    readfile($path);
    return true;
}

function gmi_find_icon($iconsDir, $id) {
    if ($id <= 0) {
        return null;
    }
    $names = array(
        $id . '.png',
        $id . '.PNG',
        $id . '.gif',
        $id . '.GIF',
        'ITEM ' . $id . '.png',
        'ITEM ' . $id . '.PNG',
        'ITEM' . $id . '.png',
        'ITEM' . $id . '.PNG',
        'item_' . $id . '.png',
        'Item_' . $id . '.png',
    );
    foreach ($names as $name) {
        $path = $iconsDir . DIRECTORY_SEPARATOR . $name;
        if (is_file($path)) {
            return $path;
        }
    }
    return null;
}

$iconsDir = __DIR__ . DIRECTORY_SEPARATOR . 'icons';
$id = 0;
$low = 0;
if (!empty($_GET['id'])) {
    $id = intval($_GET['id']);
}
if (!empty($_GET['low'])) {
    $low = intval($_GET['low']);
}

$path = gmi_find_icon($iconsDir, $id);
if ($path && gmi_serve_icon_file($path)) {
    exit;
}
if ($low > 0 && $low !== $id) {
    $path = gmi_find_icon($iconsDir, $low);
    if ($path && gmi_serve_icon_file($path)) {
        exit;
    }
}

// Deterministic colour badge (works offline in AO browser).
$badge = $id > 0 ? $id : $low;
$r = 40 + ($badge * 37) % 160;
$g = 50 + ($badge * 17) % 140;
$b = 70 + ($badge * 53) % 140;
$label = $badge > 0 ? (string)$badge : '?';
if (strlen($label) > 5) {
    $label = substr($label, -4);
}

header('Content-Type: image/svg+xml; charset=utf-8');
echo '<?xml version="1.0" encoding="UTF-8"?>';
echo '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24">';
echo '<rect width="24" height="24" rx="3" fill="rgb(' . $r . ',' . $g . ',' . $b . ')" stroke="#111" stroke-width="1"/>';
echo '<text x="12" y="16" text-anchor="middle" font-family="Verdana,sans-serif" font-size="7" font-weight="bold" fill="#fff">'
    . htmlspecialchars($label, ENT_QUOTES, 'UTF-8') . '</text>';
echo '</svg>';
