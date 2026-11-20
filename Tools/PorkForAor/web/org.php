<?php

declare(strict_types=1);

/**
 * Organization directory, modeled on the retail people/lookup/orgs.html page.
 * Copy this file to the root of the export folder (PORK_EXPORT_PATH). It reads only the published
 * org/stats/d/{dim}/name/{id}/basicstats.json files, never the database, and loads no external resource.
 *
 *   org.php                 letters of every dimension
 *   org.php?l=b&dim=10      organizations of dimension 10 whose name starts with B
 *   org.php?l=others&dim=10 names that do not start with a letter
 */

/** Dimension => label. Each one needs the same PORK_DIMENSION in the exporter that publishes it. */
const DIMENSIONS = [11 => 'AO Rebirth', 10 => 'AO Rebirth (test)'];

function letterOf(string $name): string
{
	$first = strtolower(substr(ltrim($name), 0, 1));

	return ($first >= 'a' && $first <= 'z') ? $first : 'others';
}

/**
 * Organizations of a dimension, sorted by name; null when this server publishes nothing for it.
 * The list is cached in the system temp folder and rebuilt when a published file changes.
 *
 * @return list<array{id:int,name:string,members:int,max:?int,side:string,government:string}>|null
 */
function organizations(int $dim): ?array
{
	$root = __DIR__ . '/org/stats/d/' . $dim . '/name';
	if (!is_dir($root)) {
		return null;
	}

	$files = [];
	$signature = '';
	foreach (scandir($root) ?: [] as $key) {
		$path = $root . '/' . $key . '/basicstats.json';
		$time = ctype_digit($key) ? @filemtime($path) : false;
		if ($time !== false) {
			$files[(int) $key] = $path;
			$signature .= $key . ':' . $time . ';';
		}
	}
	$signature = md5($signature);

	$cache = sys_get_temp_dir() . DIRECTORY_SEPARATOR . 'pork-orgs-' . md5(__DIR__) . '-' . $dim . '.json';
	$cached = is_file($cache) ? json_decode((string) @file_get_contents($cache), true) : null;
	if (is_array($cached) && ($cached['signature'] ?? null) === $signature && is_array($cached['orgs'] ?? null)) {
		return $cached['orgs'];
	}

	$orgs = [];
	foreach ($files as $id => $path) {
		$data = json_decode((string) @file_get_contents($path), true);
		$header = is_array($data) ? ($data[0] ?? null) : null;
		if (!is_array($header)) {
			continue;
		}
		$orgs[] = [
			'id' => $id,
			'name' => (string) ($header['NAME'] ?? ''),
			'members' => (int) ($header['NUMMEMBERS'] ?? 0),
			'max' => isset($header['MAXLVL']) ? (int) $header['MAXLVL'] : null,
			'side' => (string) ($header['SIDE_NAME'] ?? ''),
			'government' => (string) ($header['GOVERNINGNAME'] ?? ''),
		];
	}
	usort($orgs, static fn (array $a, array $b): int => strcasecmp($a['name'], $b['name']) ?: $a['id'] <=> $b['id']);

	// A read-only temp folder only costs the cache.
	$temp = $cache . '.' . bin2hex(random_bytes(4));
	if (@file_put_contents($temp, json_encode(['signature' => $signature, 'orgs' => $orgs])) !== false && !@rename($temp, $cache)) {
		@unlink($temp);
	}

	return $orgs;
}

function h(string|int|null $value): string
{
	return htmlspecialchars((string) $value, ENT_QUOTES | ENT_SUBSTITUTE | ENT_HTML5, 'UTF-8');
}

function letterBox(int $dim, string $label): string
{
	$links = [];
	foreach (range('a', 'z') as $letter) {
		$links[] = '<a href="?l=' . $letter . '&amp;dim=' . $dim . '"><b>' . strtoupper($letter) . '</b></a>';
	}
	$links[] = '<a href="?l=others&amp;dim=' . $dim . '">Others</a>';

	return '<div class="box"><h4>' . h($label) . ' : Clans &amp; Omni-Tek Factions:</h4><p>' . implode(' ', $links) . "</p></div>\n";
}

$dim = isset($_GET['dim']) && ctype_digit((string) $_GET['dim']) ? (int) $_GET['dim'] : array_key_first(DIMENSIONS);
$letter = isset($_GET['l']) ? strtolower((string) $_GET['l']) : '';
$letter = preg_match('/^[a-z]$/', $letter) === 1 || $letter === 'others' ? $letter : '';

$body = '';
foreach (DIMENSIONS as $id => $label) {
	$body .= letterBox($id, $label);
}

if ($letter !== '') {
	$orgs = isset(DIMENSIONS[$dim]) ? organizations($dim) : null;
	$body .= '<h2>' . h($letter === 'others' ? 'Others' : strtoupper($letter)) . '</h2>' . "\n";
	if ($orgs === null) {
		$body .= "<p>No data is published for this dimension.</p>\n";
		$body .= "<tbody></tbody>\n";
	} else {
		$rows = '';
		foreach ($orgs as $org) {
			if (letterOf($org['name']) !== $letter) {
				continue;
			}
			$rows .= '
			<tr>
			<td align="left"><a href="/org/stats/d/' . $dim . '/name/' . $org['id'] . '"> 
			' . h($org['name']) . '</a></td>'
				. '
				<td align="right">' . h($org['members']) . '</td>
				<td align="right">' . h($org['max']) . '</td>'
				. '
				<td align="left">' . h($org['side']) . '</td>
				<td align="left">' . h($org['government']) . '</td>
				<td align="left" class="dim">RK' . $dim . "</td>
				</tr>";
		}
		$body .= $rows === ''
			? '<p>No organization.</p>\n<tbody></tbody>\n'
			: '<table>
			<thead>
			<tr>
			<th align="left">Name</th>
			<th align="right">Members</th>
			<th align="right">Max&nbsp;Lv</th>
			<th align="left">Side</th>
			<th align="left">Government</th>
			<th align="left" class="dim">Dim</th>
			</tr>
			</thead>
			
			<tbody>'
				. $rows . '
				</tbody>
				</table>';
	}
}

header('Content-Type: text/html; charset=utf-8');
echo "<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<title>Organizations</title>\n"
	. "<style>body{font-family:Arial,sans-serif;background:#0b1620;color:#d7e3ea;margin:2em}"
	. "h4{margin:.2em 0}.box{margin-bottom:1em}a{color:#7fd0ff}"
	. "table{border-collapse:collapse}th{text-align:left;color:#8fb3c4;padding:.2em 1em .2em 0}"
	. "td{padding:.15em 1em .15em 0}.dim{color:#8fb3c4}</style>\n</head>\n<body>\n"
	. "<h2>People of Rubi-Ka</h2>\n" . $body . "</body>\n</html>\n";
