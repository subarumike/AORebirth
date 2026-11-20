<?php

declare(strict_types=1);

namespace PorkForAor;

/** A character and the stats the pages use (sections 4.1 and 4.2). A missing stat reads as 0 (section 8). */
final class CharacterRecord
{
	/** @param array<int,int> $stats */
	public function __construct(
		public readonly int $id,
		public readonly string $name,
		public readonly string $firstName,
		public readonly string $lastName,
		private readonly array $stats
	) {
	}

	public function stat(int $statId): int
	{
		return $this->stats[$statId] ?? 0;
	}

	public function level(): int
	{
		return $this->stat(CharacterReader::LEVEL);
	}

	public function orgId(): int
	{
		return $this->stat(CharacterReader::CLAN);
	}

	public function orgRank(): int
	{
		return $this->stat(CharacterReader::CLAN_LEVEL);
	}
}
