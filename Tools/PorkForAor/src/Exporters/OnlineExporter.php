<?php

declare(strict_types=1);

namespace PorkForAor\Exporters;

use PorkForAor\BaseExporter;
use PorkForAor\CharacterReader;
use PorkForAor\Config;
use PorkForAor\Format;
use PorkForAor\GameData;
use PorkForAor\Kind;

/** Online members per organization, XML and JSON (sections 12.3 and 12.6). */
final class OnlineExporter extends BaseExporter
{
	private const FILES = ['online.xml', 'online.json'];

	private readonly CharacterReader $reader;

	public function __construct(private readonly Config $config, private readonly GameData $gameData)
	{
		$this->reader = new CharacterReader();
	}

	public function kind(): Kind
	{
		return Kind::OrgOnline;
	}

	public function removeWhenAbsent(): bool
	{
		return true;
	}

	public function usesHeartbeat(): bool
	{
		return true;
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
		$statement = $db->prepare('SELECT GovernmentForm FROM organizations WHERE Id = ?');
		$statement->execute([(int) $key]);
		$form = $statement->fetchColumn();
		if ($form === false) {
			return null;
		}

		$statement = $db->prepare(
			'SELECT c.Id FROM characters c JOIN stats s ON s.Type = 50000 AND s.Instance = c.Id AND s.StatId = 5 AND s.StatValue = ? WHERE c.Online <> 0'
		);
		$statement->execute([(int) $key]);
		$records = array_values($this->reader->byIds($db, array_map('intval', $statement->fetchAll(\PDO::FETCH_COLUMN))));
		usort($records, static fn ($a, $b): int => strcasecmp($a->name, $b->name) ?: strcmp($a->name, $b->name));

		$g = $this->gameData;
		$members = [];
		foreach ($records as $c) {
			$members[] = [
				'nickname' => $c->name,
				'level' => $c->level(),
				'defender_rank_id' => $c->stat(CharacterReader::ALIEN_LEVEL),
				'profession' => $g->profession($c->stat(CharacterReader::PROFESSION)),
				'rank_name' => $g->orgRank((int) $form, $c->orgRank()),
			];
		}

		$base = $this->folder($key);

		return [
			$base . '/online.xml' => $this->xml($members),
			$base . '/online.json' => $this->json($members),
		];
	}

	/** The published set is the organizations of `org_online_meta` that still exist (section 12.3). */
	public function existingKeys(\PDO $db): array
	{
		$keys = [];
		$sql = 'SELECT m.id_org FROM org_online_meta m JOIN organizations o ON o.Id = m.id_org';
		foreach ($db->query($sql)->fetchAll(\PDO::FETCH_COLUMN) as $id) {
			$keys[(string) $id] = true;
		}

		return $keys;
	}

	public function publishedKeys(string $exportRoot): array
	{
		$root = $exportRoot . DIRECTORY_SEPARATOR . 'org' . DIRECTORY_SEPARATOR . 'online' . DIRECTORY_SEPARATOR
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

	private function folder(string $key): string
	{
		return sprintf('org/online/d/%d/name/%s', $this->config->dimension, $key);
	}

	/** @param list<array<string,mixed>> $members */
	private function xml(array $members): string
	{
		$head = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n";
		if ($members === []) {
			return $head . "<online_members/>\n";
		}
		$x = static fn (mixed $value): string => Format::xml((string) $value);
		$out = $head . "<online_members>\n";
		foreach ($members as $m) {
			$out .= "\t<member>\n"
				. "\t\t<nickname>{$x($m['nickname'])}</nickname>\n"
				. "\t\t<level>{$x($m['level'])}</level>\n"
				. "\t\t<defender_rank_id>{$x($m['defender_rank_id'])}</defender_rank_id>\n"
				. "\t\t<profession>{$x($m['profession'])}</profession>\n"
				. "\t\t<rank_name>{$x($m['rank_name'])}</rank_name>\n"
				. "\t</member>\n";
		}

		return $out . "</online_members>\n";
	}

	/** @param list<array<string,mixed>> $members */
	private function json(array $members): string
	{
		return Format::json(array_map(static fn (array $m): array => [
			'NAME' => $m['nickname'],
			'LEVELX' => $m['level'],
			'ALIENLEVEL' => $m['defender_rank_id'],
			'PROF' => $m['profession'],
			'RANK_TITLE' => $m['rank_name'],
		], $members)) . "\n";
	}
}
