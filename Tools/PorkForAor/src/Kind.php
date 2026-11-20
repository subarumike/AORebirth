<?php

declare(strict_types=1);

namespace PorkForAor;

/** The three published families and the meta table that tracks each one (sections 6, 5.5 and 12.2). */
enum Kind: string
{
	case Character = 'character';
	case OrgStats = 'org_stats';
	case OrgOnline = 'org_online';

	public function metaTable(): string
	{
		return match ($this) {
			self::Character => 'character_bio_meta',
			self::OrgStats => 'org_stats_meta',
			self::OrgOnline => 'org_online_meta',
		};
	}

	public function metaKeyColumn(): string
	{
		return match ($this) {
			self::Character => 'id_char',
			self::OrgStats, self::OrgOnline => 'id_org',
		};
	}
}
