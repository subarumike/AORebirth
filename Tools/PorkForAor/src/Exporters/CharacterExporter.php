<?php

declare(strict_types=1);

namespace PorkForAor\Exporters;

use PorkForAor\BaseExporter;
use PorkForAor\CharacterReader;
use PorkForAor\CharacterRecord;
use PorkForAor\Config;
use PorkForAor\EntityExportException;
use PorkForAor\Format;
use PorkForAor\GameData;
use PorkForAor\Kind;
use PorkForAor\Log;

/** Bio XML, JSON and HTML of a character (sections 5.2 to 5.6, 7). */
final class CharacterExporter extends BaseExporter
{
	private const FILES = ['index.html', 'bio.xml', 'bio.json'];

	private readonly CharacterReader $reader;

	public function __construct(private readonly Config $config, private readonly GameData $gameData)
	{
		$this->reader = new CharacterReader();
	}

	public function kind(): Kind
	{
		return Kind::Character;
	}

	public function keysForMetaIds(\PDO $db, array $metaIds): array
	{
		$keys = [];
		foreach ($this->reader->byIds($db, $metaIds) as $character) {
			$key = $this->keyOf($character->name);
			if ($key !== null) {
				$keys[$key] = $key;
			}
		}

		return array_values($keys);
	}

	public function render(\PDO $db, string $key): ?array
	{
		$statement = $db->prepare('SELECT Id FROM characters WHERE Name = ?');
		$statement->execute([$key]);
		$ids = array_map('intval', $statement->fetchAll(\PDO::FETCH_COLUMN));
		if ($ids === []) {
			return null;
		}
		if (count($ids) > 1) {
			throw new EntityExportException(sprintf('Nick "%s" matches %d characters; not exported (section 8).', $key, count($ids)));
		}
		$character = $this->reader->byIds($db, $ids)[$ids[0]] ?? null;
		if ($character === null) {
			return null;
		}

		$view = $this->view($db, $character);
		$base = $this->folder($key);

		return [
			$base . '/index.html' => $this->html($view, $key),
			$base . '/bio.xml' => $this->xml($view),
			$base . '/bio.json' => $this->json($view),
		];
	}

	public function existingKeys(\PDO $db): array
	{
		$keys = [];
		foreach ($db->query('SELECT Name FROM characters')->fetchAll(\PDO::FETCH_COLUMN) as $name) {
			$key = $this->keyOf((string) $name);
			if ($key !== null) {
				$keys[$key] = true;
			}
		}

		return $keys;
	}

	public function publishedKeys(string $exportRoot): array
	{
		$root = $exportRoot . DIRECTORY_SEPARATOR . 'character' . DIRECTORY_SEPARATOR . 'bio' . DIRECTORY_SEPARATOR
			. 'd' . DIRECTORY_SEPARATOR . $this->config->dimension . DIRECTORY_SEPARATOR . 'name';
		$published = [];
		if (!is_dir($root)) {
			return $published;
		}
		foreach (scandir($root) ?: [] as $key) {
			if ($key === '.' || $key === '..' || !is_dir($root . DIRECTORY_SEPARATOR . $key)) {
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

	/** The generation time of a character with no meta row changes at each render, so it is not compared. */
	public function comparable(string $relativePath, string $content): string
	{
		return Format::withoutTimestamps($content);
	}

	private function keyOf(string $nick): ?string
	{
		$key = Format::nickKey($nick);
		if ($key === null) {
			Log::warn(sprintf('Nick "%s" is not a safe folder name; not exported.', $nick));
		}

		return $key;
	}

	private function folder(string $key): string
	{
		return sprintf('character/bio/d/%d/name/%s', $this->config->dimension, $key);
	}

	/** @return array<string,mixed> */
	private function view(\PDO $db, CharacterRecord $c): array
	{
		$g = $this->gameData;
		$level = $c->level();
		$rating = $c->stat(CharacterReader::PVP_RATING);

		$organization = null;
		if ($c->orgId() > 0) {
			$statement = $db->prepare('SELECT Name, GovernmentForm FROM organizations WHERE Id = ?');
			$statement->execute([$c->orgId()]);
			$row = $statement->fetch();
			if ($row !== false) {
				$organization = [
					'id' => $c->orgId(),
					'name' => (string) $row['Name'],
					'rank_id' => $c->orgRank(),
					'rank' => $g->orgRank((int) $row['GovernmentForm'], $c->orgRank()),
				];
			}
		}

		$statement = $db->prepare('SELECT last_updated FROM character_bio_meta WHERE id_char = ?');
		$statement->execute([$c->id]);
		$updated = $statement->fetchColumn();

		return [
			'id' => $c->id,
			'first' => $c->firstName,
			'nick' => $c->name,
			'last' => $c->lastName,
			'level' => $level,
			'breed' => $g->breed($c->stat(CharacterReader::BREED)),
			'gender' => $g->sex($c->stat(CharacterReader::SEX)),
			'faction' => $g->side($c->stat(CharacterReader::SIDE)),
			'profession_id' => $c->stat(CharacterReader::PROFESSION),
			'profession' => $g->profession($c->stat(CharacterReader::PROFESSION)),
			'profession_title' => $g->professionTitle(
				$c->stat(CharacterReader::PROFESSION),
				$g->titleLevel($c->stat(CharacterReader::TITLE_LEVEL), $level)
			),
			'defender_rank_id' => $c->stat(CharacterReader::ALIEN_LEVEL),
			'defender_rank' => $g->defenderRank($c->stat(CharacterReader::ALIEN_LEVEL)),
			'pvp_rating' => $rating,
			'pvp_title' => $g->pvpTitle($rating),
			'organization' => $organization,
			'updated' => is_string($updated) ? Format::utc($updated) : new \DateTimeImmutable('now', new \DateTimeZone('UTC')),
		];
	}

	/** @param array<string,mixed> $v */
	private function xml(array $v): string
	{
		$x = static fn (mixed $value): string => Format::xml((string) $value);
		$out = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<character>\n"
			. "\t<name>\n"
			. "\t<firstname>{$x($v['first'])}</firstname>\n"
			. "\t<nick>{$x($v['nick'])}</nick>\n"
			. "\t<lastname>{$x($v['last'])}</lastname>\n"
			. "\t</name>\n"
			. "\t<basic_stats>\n"
			. "\t\t<level>{$x($v['level'])}</level>\n"
			. "\t\t<breed>{$x($v['breed'])}</breed>\n"
			. "\t\t<gender>{$x($v['gender'])}</gender>\n"
			. "\t\t<faction>{$x($v['faction'])}</faction>\n"
			. "\t\t<profession>{$x($v['profession'])}</profession>\n"
			. "\t\t<profession_title>{$x($v['profession_title'])}</profession_title>\n"
			. "\t\t<defender_rank_id>{$x($v['defender_rank_id'])}</defender_rank_id>\n"
			. "\t\t<defender_rank>{$x($v['defender_rank'])}</defender_rank>\n"
			. "\t</basic_stats>\n"
			. "\t<pictureurl>\n\t</pictureurl>\n"
			. "\t<smallpictureurl>\n\t</smallpictureurl>\n";
		if ($v['organization'] !== null) {
			$o = $v['organization'];
			$out .= "\t<organization_membership>\n"
				. "\t\t<organization_id>{$x($o['id'])}</organization_id>\n"
				. "\t\t<organization_name>{$x($o['name'])}</organization_name>\n"
				. "\t\t<rank>{$x($o['rank'])}</rank>\n"
				. "\t\t<rank_id>{$x($o['rank_id'])}</rank_id>\n"
				. "\t</organization_membership>\n";
		}

		return $out . "\t<last_updated>" . Format::stamp($v['updated']) . " Universal</last_updated>\n</character>\n";
	}

	/** @param array<string,mixed> $v */
	private function json(array $v): string
	{
		$o = $v['organization'];

		return Format::json([
			[
				'SEX' => $v['gender'],
				'BREED' => $v['breed'],
				'PVPRATING' => $v['pvp_rating'],
				'NAME' => $v['nick'],
				'FIRSTNAME' => $v['first'],
				'PVPTITLE' => $v['pvp_title'],
				'LASTNAME' => $v['last'],
				'CHAR_DIMENSION' => $this->config->dimension,
				'ALIENLEVEL' => $v['defender_rank_id'],
				'RANK_name' => $v['defender_rank'],
				'HEADID' => 0,
				'PROFNAME' => $v['profession_title'],
				'LEVELX' => $v['level'],
				'PROF' => $v['profession'],
				'CHAR_INSTANCE' => $v['id'],
				'SIDE' => $v['faction'],
			],
			$o === null ? null : [
				'ORG_DIMENSION' => $this->config->dimension,
				'RANK_TITLE' => $o['rank'],
				'ORG_INSTANCE' => $o['id'],
				'NAME' => $o['name'],
				'RANK' => $o['rank_id'],
			],
			Format::stamp($v['updated']),
		]) . "\n";
	}

	/** @param array<string,mixed> $v */
	private function html(array $v, string $key): string
	{
		$h = static fn (mixed $value): string => Format::html((string) $value);
		$base = '/' . $this->folder($key);

		$name = [];
		if ($v['first'] !== '') {
			$name[] = $h($v['first']);
		}
		$name[] = '&quot;' . $h($v['nick']) . '&quot;';
		if ($v['last'] !== '') {
			$name[] = $h($v['last']);
		}
		$title = $v['pvp_title'] !== '' ? '<i>' . $h($v['pvp_title']) . '</i> ' : '';

		$profession = $h($v['profession']) . ($v['profession_title'] !== '' ? ' (' . $h($v['profession_title']) . ')' : '');
		$rows = [
			'Level' => $h($v['level']),
			'Breed' => $h($v['breed']),
			'Gender' => $h($v['gender']),
			'Faction' => $h($v['faction']),
			'Profession' => $profession,
		];
		if ($v['organization'] !== null) {
			$o = $v['organization'];
			$link = sprintf('/org/stats/d/%d/name/%d', $this->config->dimension, $o['id']);
			$rows['Organization'] = $h($o['rank']) . ' of <a href="' . $h($link) . '">' . $h($o['name']) . '</a>';
		}
		$rows['Defender Rank'] = '(' . $h($v['defender_rank_id']) . ')';

		$table = '';
		foreach ($rows as $label => $value) {
			$table .= "\t\t<tr><th>" . ($label === 'Defender Rank' ? 'Defender Rank :' : $label) . "</th><td>$value</td></tr>\n";
		}

		return "<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n"
			. '<title>' . $h($v['nick']) . " - Character</title>\n"
			. "<style>body{font-family:Arial,sans-serif;background:#0b1620;color:#d7e3ea;margin:2em}"
			. "h1{font-size:1.4em;font-weight:normal}th{text-align:left;padding:.2em 1em .2em 0;color:#8fb3c4}"
			. "a{color:#7fd0ff}.links{margin-top:1.5em}</style>\n</head>\n<body>\n"
			. '<h1>' . $title . implode(' ', $name) . "</h1>\n"
			. "<table>\n$table</table>\n"
			. '<p class="links"><a href="' . $h($base . '/bio.xml') . '">bio.xml</a> | <a href="'
			. $h($base . '/bio.xml?data_type=json') . "\">bio.xml?data_type=json</a></p>\n"
			. "</body>\n</html>\n";
	}
}
