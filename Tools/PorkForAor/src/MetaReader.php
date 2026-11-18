<?php

declare(strict_types=1);

namespace PorkForAor;

/** Change detection on one meta table (sections 9 and 12.3). */
final class MetaReader
{
	public function __construct(private readonly \PDO $db, private readonly Kind $kind)
	{
	}

	/**
	 * Rows newer than the watermark, minus the overlap that covers a row committed late.
	 *
	 * @return array{ids: list<int>, max: ?string}
	 */
	public function changedSince(string $watermark, int $overlapSeconds): array
	{
		$sql = sprintf(
			'SELECT %s AS id, last_updated FROM %s WHERE last_updated >= DATE_SUB(?, INTERVAL %d SECOND) ORDER BY last_updated',
			$this->kind->metaKeyColumn(),
			$this->kind->metaTable(),
			$overlapSeconds
		);
		$statement = $this->db->prepare($sql);
		$statement->execute([$watermark]);

		$ids = [];
		$max = null;
		foreach ($statement as $row) {
			$ids[] = (int) $row['id'];
			$max = (string) $row['last_updated'];
		}

		return ['ids' => $ids, 'max' => $max];
	}
}
