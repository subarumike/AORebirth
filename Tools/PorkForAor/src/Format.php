<?php

declare(strict_types=1);

namespace PorkForAor;

/** Text escaping and the timestamp formats of sections 5.4 and 7. */
final class Format
{
	public static function xml(string $value): string
	{
		return htmlspecialchars($value, ENT_XML1 | ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8');
	}

	public static function html(string $value): string
	{
		return htmlspecialchars($value, ENT_QUOTES | ENT_SUBSTITUTE | ENT_HTML5, 'UTF-8');
	}

	/** yyyy/MM/dd HH:mm:ss in UTC, the JSON form; XML adds the " Universal" suffix. */
	public static function stamp(\DateTimeInterface $time): string
	{
		return \DateTimeImmutable::createFromInterface($time)->setTimezone(new \DateTimeZone('UTC'))->format('Y/m/d H:i:s');
	}

	/** A meta table value is stored in UTC by the engines (section 6). */
	public static function utc(string $databaseValue): \DateTimeImmutable
	{
		return new \DateTimeImmutable($databaseValue, new \DateTimeZone('UTC'));
	}

	/** Removes the yyyy/MM/dd HH:mm:ss stamps, so a render-time line does not count as a difference. */
	public static function withoutTimestamps(string $content): string
	{
		return (string) preg_replace('~\d{4}/\d\d/\d\d \d\d:\d\d:\d\d~', '', $content);
	}

	public static function json(mixed $value): string
	{
		return json_encode($value, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_THROW_ON_ERROR);
	}

	/** Folder name of a character: the nick in lower case (section 3.1). Null when the nick is not a safe folder name. */
	public static function nickKey(string $nick): ?string
	{
		return preg_match('/^[A-Za-z0-9_-]{1,32}$/', $nick) === 1 ? strtolower($nick) : null;
	}
}
