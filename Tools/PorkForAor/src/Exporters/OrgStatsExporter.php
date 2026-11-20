<?php

declare(strict_types=1);

namespace PorkForAor\Exporters;

use PorkForAor\BaseExporter;
use PorkForAor\CharacterReader;
use PorkForAor\CharacterRecord;
use PorkForAor\Config;
use PorkForAor\Format;
use PorkForAor\GameData;
use PorkForAor\Kind;

/** Organization statistics page, XML and JSON (section 5.5). Structure confirmed on the retail `basicstats` files. */
final class OrgStatsExporter extends BaseExporter
{
	private const FILES = ['index.html', 'basicstats.xml', 'basicstats.json'];

	/** Profession id => [retail JSON key (typos kept), HTML label]; the array order is the HTML order. */
	private const PROFESSIONS = [
		6 => ['ADVENTURERCOUNT', 'Adventurers'],
		5 => ['AGENTCOUNT', 'Agents'],
		8 => ['BTCOUNT', 'Bureaucrats'],
		10 => ['DOCTORCOUNT', 'Doctors'],
		9 => ['ENFCOUNT', 'Enforcers'],
		3 => ['ENGINEEERCOUNT', 'Engineers'],
		4 => ['FIXERCOUNT', 'Fixers'],
		13 => ['KEEPERCOUNT', 'Keepers'],
		2 => ['MACOUNT', 'Martial Artists'],
		12 => ['METACOUNT', 'Meta-Physicists'],
		11 => ['NANOCOUNT', 'Nano-Technicians'],
		14 => ['SHADECOUNT', 'Shades'],
		1 => ['SOLIDERCOUNT', 'Soldiers'],
		7 => ['TRADERCOUNT', 'Traders'],
	];

	private const BREEDS = [
		1 => ['SOLITUSCOUNT', 'Solitus'],
		2 => ['OPIFEXCOUNT', 'Opifex'],
		3 => ['NANORACECOUNT', 'Nano'],
		4 => ['ATROXCOUNT', 'Atrox'],
	];

	private const SEXES = [
		1 => ['MALECOUNT', 'Male'],
		0 => ['NEUTERCOUNT', 'Neuter'],
		2 => ['FEMALECOUNT', 'Female'],
	];

	private readonly CharacterReader $reader;

	public function __construct(private readonly Config $config, private readonly GameData $gameData)
	{
		$this->reader = new CharacterReader();
	}

	public function kind(): Kind
	{
		return Kind::OrgStats;
	}

	public function keysForMetaIds(\PDO $db, array $metaIds): array
	{
		return array_map('strval', $metaIds);
	}

	public function render(\PDO $db, string $key): ?array
	{
		if (!ctype_digit($key)) {
			return null;
		}
		$statement = $db->prepare('SELECT Id, Name, LeaderId, GovernmentForm, Description, Objective, History FROM organizations WHERE Id = ?');
		$statement->execute([(int) $key]);
		$org = $statement->fetch();
		if ($org === false) {
			return null;
		}

		$view = $this->view($db, $org);
		$base = $this->folder($key);

		return [
			$base . '/index.html' => $this->html($view),
			$base . '/basicstats.xml' => $this->xml($view),
			$base . '/basicstats.json' => $this->json($view),
		];
	}

	public function existingKeys(\PDO $db): array
	{
		$keys = [];
		foreach ($db->query('SELECT Id FROM organizations')->fetchAll(\PDO::FETCH_COLUMN) as $id) {
			$keys[(string) $id] = true;
		}

		return $keys;
	}

	public function publishedKeys(string $exportRoot): array
	{
		$root = $exportRoot . DIRECTORY_SEPARATOR . 'org' . DIRECTORY_SEPARATOR . 'stats' . DIRECTORY_SEPARATOR
			. 'd' . DIRECTORY_SEPARATOR . $this->config->dimension . DIRECTORY_SEPARATOR . 'name';
		$published = [];
		if (!is_dir($root)) {
			return $published;
		}
		foreach (scandir($root) ?: [] as $key) {
			if (!ctype_digit($key) || !is_dir($root . DIRECTORY_SEPARATOR . $key)) {
				continue;
			}
			foreach (self::FILES as $file) {
				if (is_file($root . DIRECTORY_SEPARATOR . $key . DIRECTORY_SEPARATOR . $file)) {
					$published[$key][] = $this->folder($key) . '/' . $file;
				}
			}
		}

		return $published;
	}

	/** The last_updated line is the render time (section 5.5), so it is ignored when comparing. */
	public function comparable(string $relativePath, string $content): string
	{
		return Format::withoutTimestamps($content);
	}

	private function folder(string $key): string
	{
		return sprintf('org/stats/d/%d/name/%s', $this->config->dimension, $key);
	}

	/**
	 * @param array<string,mixed> $org
	 * @return array<string,mixed>
	 */
	private function view(\PDO $db, array $org): array
	{
		$g = $this->gameData;
		$form = (int) $org['GovernmentForm'];
		$orgId = (int) $org['Id'];

		$memberIds = $this->reader->memberIds($db, $orgId);
		$records = array_values($this->reader->byIds($db, $memberIds));
		usort($records, static fn (CharacterRecord $a, CharacterRecord $b): int => [$a->orgRank(), $a->id] <=> [$b->orgRank(), $b->id]);

		$leader = $this->reader->byIds($db, [(int) $org['LeaderId']])[(int) $org['LeaderId']] ?? null;
		$side = $leader?->stat(CharacterReader::SIDE) ?? 0;

		$members = [];
		$levels = [];
		$count = ['profession' => [], 'breed' => [], 'sex' => []];
		foreach ($records as $c) {
			$profession = $c->stat(CharacterReader::PROFESSION);
			$level = $c->level();
			$rating = $c->stat(CharacterReader::PVP_RATING);
			$levels[] = $level;
			$count['profession'][$profession] = ($count['profession'][$profession] ?? 0) + 1;
			$count['breed'][$c->stat(CharacterReader::BREED)] = ($count['breed'][$c->stat(CharacterReader::BREED)] ?? 0) + 1;
			$count['sex'][$c->stat(CharacterReader::SEX)] = ($count['sex'][$c->stat(CharacterReader::SEX)] ?? 0) + 1;

			$members[] = [
				'id' => $c->id,
				'first' => $c->firstName,
				'nick' => $c->name,
				'last' => $c->lastName,
				'rank' => $c->orgRank(),
				'rank_name' => $g->orgRank($form, $c->orgRank()),
				'level' => $level,
				'profession' => $g->profession($profession),
				'profession_title' => $g->professionTitle($profession, $g->titleLevel($c->stat(CharacterReader::TITLE_LEVEL), $level)),
				'gender' => $g->sex($c->stat(CharacterReader::SEX)),
				'breed' => $g->breed($c->stat(CharacterReader::BREED)),
				'defender_rank_id' => $c->stat(CharacterReader::ALIEN_LEVEL),
				'defender_rank' => $g->defenderRank($c->stat(CharacterReader::ALIEN_LEVEL)),
				'pvp_rating' => $rating,
				'pvp_title' => $g->pvpTitle($rating),
			];
		}

		return [
			'id' => $orgId,
			'name' => (string) $org['Name'],
			'side' => $side,
			'side_name' => $g->side($side),
			'governing' => $g->governingForm($form),
			'description' => $this->text((string) $org['Description']),
			'objective' => $this->text((string) $org['Objective']),
			'history' => $this->text((string) $org['History']),
			'total' => count($members),
			'sum' => array_sum($levels),
			'min' => $levels === [] ? null : min($levels),
			'max' => $levels === [] ? null : max($levels),
			'count' => $count,
			'members' => $members,
			'updated' => new \DateTimeImmutable('now', new \DateTimeZone('UTC')),
		];
	}

	/** The database default `" "` counts as empty (section 5.5). */
	private function text(string $value): string
	{
		return trim($value, "\" \t\r\n") === '' ? '' : trim($value);
	}

	/** Percentage with one decimal, rounded up (section 5.5); integer arithmetic avoids float error. */
	private function percent(int $count, int $total): float
	{
		return intdiv($count * 1000 + $total - 1, $total) / 10;
	}

	/** @param array<string,mixed> $v */
	private function json(array $v): string
	{
		$counts = [];
		foreach (self::BREEDS as $id => [$jsonKey]) {
			$counts[$jsonKey] = $v['count']['breed'][$id] ?? 0;
		}
		foreach (self::SEXES as $id => [$jsonKey]) {
			$counts[$jsonKey] = $v['count']['sex'][$id] ?? 0;
		}
		foreach (self::PROFESSIONS as $id => [$jsonKey]) {
			$counts[$jsonKey] = $v['count']['profession'][$id] ?? 0;
		}

		$header = [
			'ORG_INSTANCE' => $v['id'],
			'ORG_DIMENSION' => $this->config->dimension,
			'NAME' => $v['name'],
			'SIDE' => $v['side'],
			'SIDE_NAME' => $v['side_name'],
			'GOVERNINGNAME' => $v['governing'],
			'DESCRIPTION' => $v['description'],
			'OBJECTIVE' => $v['objective'],
			'HISTORY' => $v['history'],
			'NUMMEMBERS' => $v['total'],
			'MINLVL' => $v['min'],
			'AVGLVL' => $v['total'] === 0 ? null : round($v['sum'] / $v['total'], 4),
			'MAXLVL' => $v['max'],
			'MONSTERCOUNT' => 0,
		] + $counts;

		$members = [];
		foreach ($v['members'] as $m) {
			$members[] = [
				'SEX' => $m['gender'],
				'BREED' => $m['breed'],
				'PVPRATING' => $m['pvp_rating'],
				'NAME' => $m['nick'],
				'FIRSTNAME' => $m['first'],
				'PVPTITLE' => $m['pvp_title'],
				'LASTNAME' => $m['last'],
				'CHAR_DIMENSION' => $this->config->dimension,
				'RANK_TITLE' => $m['rank_name'],
				'RANK' => $m['rank'],
				'ALIENLEVEL' => $m['defender_rank_id'],
				'PROF_TITLE' => $m['profession_title'],
				'HEADID' => 0,
				'LEVELX' => $m['level'],
				'PROF' => $m['profession'],
				'CHAR_INSTANCE' => $m['id'],
				'DEFENDER_RANK_TITLE' => $m['defender_rank'],
			];
		}

		return Format::json([$header, $members, Format::stamp($v['updated'])]) . "\n";
	}

	/** @param array<string,mixed> $v */
	private function xml(array $v): string
	{
		$x = static fn (mixed $value): string => Format::xml((string) $value);
		$out = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n<organization>\n"
			. "\t<id>{$x($v['id'])}</id>\n"
			. "\t<name>{$x($v['name'])}</name>\n"
			. "\t<side>{$x($v['side_name'])}</side>\n"
			. "\t<last_updated>" . Format::stamp($v['updated']) . " Universal</last_updated>\n"
			. "<members>\n";
		foreach ($v['members'] as $m) {
			$out .= "\t<member>\n"
				. "\t<firstname>{$x($m['first'])}</firstname>\n"
				. "\t<nickname>{$x($m['nick'])}</nickname>\n"
				. "\t<lastname>{$x($m['last'])}</lastname>\n"
				. "\t<rank>{$x($m['rank'])}</rank>\n"
				. "\t<rank_name>{$x($m['rank_name'])}</rank_name>\n"
				. "\t<level>{$x($m['level'])}</level>\n"
				. "\t<profession>{$x($m['profession'])}</profession>\n"
				. "\t<profession_title>{$x($m['profession_title'])}</profession_title>\n"
				. "\t<gender>{$x($m['gender'])}</gender>\n"
				. "\t<breed>{$x($m['breed'])}</breed>\n"
				. "\t\t<defender_rank_id>{$x($m['defender_rank_id'])}</defender_rank_id>\n"
				. "\t\t<defender_rank>{$x($m['defender_rank'])}</defender_rank>\n"
				. "\t</member>\n";
		}

		return $out . "\t</members>\n\t</organization>\n";
	}

	/** @param array<string,mixed> $v */
	private function html(array $v): string
	{
		$h = static fn (mixed $value): string => Format::html((string) $value);
		$total = (int) $v['total'];
		$base = '/' . $this->folder((string) $v['id']);

		$section = function (string $title, array $labels, array $counts) use ($h, $total): string {
			$html = "<h3>$title</h3>\n<table class=\"stats\">\n<tr><th>$title</th><th>#</th><th>% of total</th><th></th></tr>\n";
			foreach ($labels as $id => $label) {
				$n = (int) ($counts[$id] ?? 0);
				$pct = $total === 0 ? null : $this->percent($n, $total);
				$html .= '<tr><td>' . $h($label) . "</td><td>$n</td><td>" . ($pct === null ? '' : number_format($pct, 1, '.', ''))
					. '</td><td>' . ($pct === null ? '' : '<div class="bar" style="width:' . number_format($pct, 1, '.', '') . '%"></div>')
					. "</td></tr>\n";
			}

			return $html . "</table>\n";
		};
		$labelsOf = static fn (array $map): array => array_map(static fn (array $entry): string => $entry[1], $map);

		$stats = "<h2>Statistics:</h2>\n<p>Number of members: <b>$total</b></p>\n"
			. '<p>Min. level: <b>' . ($v['min'] ?? '') . "</b></p>\n"
			. '<p>Avg. level: <b>' . ($total === 0 ? '' : intdiv($v['sum'] + $total - 1, $total)) . "</b></p>\n"
			. '<p>Max. level: <b>' . ($v['max'] ?? '') . "</b></p>\n"
			. $section('Profession', $labelsOf(self::PROFESSIONS), $v['count']['profession'])
			. $section('Breed', $labelsOf(self::BREEDS), $v['count']['breed'])
			. $section('Gender', $labelsOf(self::SEXES), $v['count']['sex'])
			. '<p>Last updated : <b>' . Format::stamp($v['updated']) . " Universal</b></p>\n";

		$text = static fn (string $label, string $value, string $empty): string
			=> "<p>$label:</p>\n<p>" . ($value === '' ? $empty : nl2br($h($value), false)) . "</p>\n";
		$header = '<h2>' . $h($v['name']) . "</h2>\n"
			. '<p><b>Faction:</b> ' . $h($v['side_name']) . "</p>\n"
			. '<p><b>Government:</b> ' . $h($v['governing']) . "</p>\n"
			. $text('Description', $v['description'], 'No description added.')
			. $text('Objective', $v['objective'], 'No objective added.')
			. $text('History', $v['history'], 'No history added.');

		$rows = '';
		foreach ($v['members'] as $m) {
			$link = sprintf('/character/bio/d/%d/name/%s', $this->config->dimension, strtolower($m['nick']));
			$rows .= '<tr><td><a href="' . $h($link) . '">' . $h($m['nick']) . '</a></td><td>' . $h($m['rank'] . ' ' . $m['rank_name'])
				. '</td><td>' . $h($m['level']) . '</td><td>' . $h($m['profession']) . '</td><td>' . $h($m['gender'])
				. '</td><td>' . $h($m['breed']) . "</td></tr>\n";
		}
		$members = "<h2>Organisation Members:</h2>\n<table class=\"members\">\n"
			. "<tr><th>Name</th><th>Rank</th><th>LVL</th><th>Profession</th><th>Gender</th><th>Breed</th></tr>\n$rows</table>\n";

		return "<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n"
			. '<title>' . $h($v['name']) . " - Organization</title>\n"
			. "<style>body{font-family:Arial,sans-serif;background:#0b1620;color:#d7e3ea;margin:2em}"
			. "th{text-align:left;padding:.2em 1em .2em 0;color:#8fb3c4}td{padding:.2em 1em .2em 0}"
			. "a{color:#7fd0ff}.bar{height:.7em;background:#3f8fbf}.stats td:last-child{width:12em}</style>\n</head>\n<body>\n"
			. $header . $stats . $members
			. '<p>There is also an <a href="' . $h($base . '/basicstats.xml') . '">XML version of this membership roster</a> available.</p>' . "\n"
			. '<p>There is also a <a href="' . $h($base . '/basicstats.xml?data_type=json') . '">JSON version of this membership roster</a>.</p>' . "\n"
			. "</body>\n</html>\n";
	}
}
