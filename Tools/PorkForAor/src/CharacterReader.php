<?php

declare(strict_types=1);

namespace PorkForAor;

/** Reads characters and their stats (`Type = 50000`, `Instance = characters.Id`, section 4.1). */
final class CharacterReader
{
	public const BREED = 4;
	public const CLAN = 5;
	public const SIDE = 33;
	public const TITLE_LEVEL = 37;
	public const CLAN_LEVEL = 48;
	public const LEVEL = 54;
	public const SEX = 59;
	public const PROFESSION = 60;
	public const ALIEN_LEVEL = 169;
	public const PVP_RATING = 333;

	private const STAT_IDS = [4, 5, 33, 37, 48, 54, 59, 60, 169, 333];
	private const CHARACTER_STAT_TYPE = 50000;
	private const CHUNK = 500;

	/**
	 * @param list<int> $ids
	 * @return array<int,CharacterRecord> by character id; ids that no longer exist are absent
	 */
	public function byIds(\PDO $db, array $ids): array
	{
		$records = [];
		foreach (array_chunk(array_values(array_unique($ids)), self::CHUNK) as $chunk) {
			$marks = implode(',', array_fill(0, count($chunk), '?'));

			$statement = $db->prepare("SELECT Id, Name, FirstName, LastName FROM characters WHERE Id IN ($marks)");
			$statement->execute($chunk);
			$rows = $statement->fetchAll();

			$stats = $this->statsFor($db, $chunk);
			foreach ($rows as $row) {
				$id = (int) $row['Id'];
				$records[$id] = new CharacterRecord($id, (string) $row['Name'], (string) $row['FirstName'], (string) $row['LastName'], $stats[$id] ?? []);
			}
		}

		return $records;
	}

	/** @return list<int> ids of the characters whose stat 5 is the organization id */
	public function memberIds(\PDO $db, int $orgId): array
	{
		$statement = $db->prepare('SELECT Instance FROM stats WHERE Type = ? AND StatId = ? AND StatValue = ?');
		$statement->execute([self::CHARACTER_STAT_TYPE, self::CLAN, $orgId]);

		return array_map('intval', $statement->fetchAll(\PDO::FETCH_COLUMN));
	}

	/**
	 * @param list<int> $ids
	 * @return array<int,array<int,int>> character id => stat id => value
	 */
	private function statsFor(\PDO $db, array $ids): array
	{
		$idMarks = implode(',', array_fill(0, count($ids), '?'));
		$statMarks = implode(',', array_fill(0, count(self::STAT_IDS), '?'));
		$statement = $db->prepare("SELECT Instance, StatId, StatValue FROM stats WHERE Type = ? AND StatId IN ($statMarks) AND Instance IN ($idMarks)");
		$statement->execute([self::CHARACTER_STAT_TYPE, ...self::STAT_IDS, ...$ids]);

		$stats = [];
		foreach ($statement as $row) {
			$stats[(int) $row['Instance']][(int) $row['StatId']] = (int) $row['StatValue'];
		}

		return $stats;
	}
}
