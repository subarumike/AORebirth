<?php
declare(strict_types=1);

function daily_local_host(string $value): bool
{
    $host = strtolower(trim($value, " \t"));
    if (str_ends_with($host, ':80')) {
        $host = rtrim(substr($host, 0, -3), " \t");
    }
    // These are the exact native client's Daily Login host names.
    return in_array($host, ['uwg.daily.icc-rk', 'www.daily.icc-rk', 'dailyrewards.anarchy-online.com'], true);
}

function daily_local_route(string $method, string $target, string $host, string $remote): array
{
    if ($remote !== '127.0.0.1' || !daily_local_host($host)) {
        return ['status' => 403];
    }
    if ($target === '' || strlen($target) > 8192 || preg_match('/[\x00-\x20\x7f]/', $target)) {
        return ['status' => 404];
    }
    $url = parse_url($target);
    if ($url === false || isset($url['fragment']) || isset($url['user']) || isset($url['pass'])) {
        return ['status' => 404];
    }
    if ((isset($url['scheme']) || isset($url['host'])) &&
        (strtolower($url['scheme'] ?? '') !== 'http' || ($url['port'] ?? 80) !== 80 || !daily_local_host($url['host'] ?? ''))) {
        return ['status' => 403];
    }

    $path = $url['path'] ?? '/';
    if ($path === '/') $path = '/index.app';
    $types = [
        '/index.app' => 'text/html; charset=utf-8', '/index.html' => 'text/html; charset=utf-8',
        '/rewards.json' => 'application/json; charset=utf-8', '/claim.php' => 'application/json; charset=utf-8',
        '/css/icc.css' => 'text/css; charset=utf-8', '/js/daily.js' => 'application/javascript; charset=utf-8',
    ];
    $type = $types[$path] ?? null;
    if ($type === null && preg_match('~\A/assets/(?:[A-Za-z0-9_-]+/)*[A-Za-z0-9_-]+[.]png\z~D', $path)) {
        $type = 'image/png';
    }
    if ($type === null) return ['status' => 404];

    $methods = $path === '/claim.php' ? ['GET', 'POST'] : ['GET', 'HEAD'];
    if (!in_array($method, $methods, true)) return ['status' => 405, 'allow' => implode(', ', $methods)];
    return ['status' => 200, 'file' => substr($path, 1), 'type' => $type];
}

if (PHP_SAPI !== 'cli-server') return;

$method = $_SERVER['REQUEST_METHOD'] ?? '';
$route = daily_local_route($method, $_SERVER['REQUEST_URI'] ?? '', $_SERVER['HTTP_HOST'] ?? '', $_SERVER['REMOTE_ADDR'] ?? '');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store');
http_response_code($route['status']);
if ($route['status'] !== 200) {
    if (isset($route['allow'])) header('Allow: ' . $route['allow']);
    return true;
}

$stop = getenv('AOREBIRTH_LOCAL_DAILY_STOP_FILE');
if ($stop === false || $stop === '' || is_file($stop)) {
    http_response_code(503);
    if ($stop !== false && $stop !== '') header('X-AORebirth-Daily-Drained: 1');
    return true;
}
if ($method === 'POST' && (int)($_SERVER['CONTENT_LENGTH'] ?? 0) > 32768) {
    http_response_code(413);
    return true;
}

$root = realpath(__DIR__ . DIRECTORY_SEPARATOR . 'daily');
if ($root === false) {
    http_response_code(503);
    return true;
}
$expected = $root . DIRECTORY_SEPARATOR . str_replace('/', DIRECTORY_SEPARATOR, $route['file']);
$file = realpath($expected);
if ($file === false || !is_file($file) ||
    (DIRECTORY_SEPARATOR === '\\' ? strcasecmp($file, $expected) !== 0 : $file !== $expected)) {
    http_response_code(404);
    return true;
}

header('Content-Type: ' . $route['type']);
if ($route['file'] === 'claim.php') {
    require $file;
} elseif ($method !== 'HEAD') {
    readfile($file);
}
return true;
