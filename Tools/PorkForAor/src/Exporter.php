<?php

declare(strict_types=1);

namespace PorkForAor;

/**
 * One published family (characters, organization stats pages, online lists).
 * An entity key identifies what is published: the nick for a character, the id for an organization.
 */
interface Exporter
{
	public function kind(): Kind;

	/**
	 * Entity keys for the changed meta ids.
	 *
	 * @param list<int> $metaIds
	 * @return list<string>
	 */
	public function keysForMetaIds(\PDO $db, array $metaIds): array;

	/**
	 * Files of one entity as relative path => body, or null when the entity no longer exists in the database.
	 *
	 * @return array<string,string>|null
	 */
	public function render(\PDO $db, string $key): ?array;

	/** @return array<string,true> keys of every entity that exists in the database */
	public function existingKeys(\PDO $db): array;

	/** @return array<string,list<string>> entity key => relative paths of its published files */
	public function publishedKeys(string $exportRoot): array;

	/** Online files carry no history and are removed as soon as the organization is gone (section 9.1). */
	public function removeWhenAbsent(): bool;

	/** Online files are touched every tick (section 12.3). */
	public function usesHeartbeat(): bool;

	/** Body used to compare a published file with a fresh render; lets an exporter ignore a render-time line. */
	public function comparable(string $relativePath, string $content): string;
}
