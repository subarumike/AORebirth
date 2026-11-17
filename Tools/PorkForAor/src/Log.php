<?php

declare(strict_types=1);

namespace PorkForAor;

/** Standard output and standard error only, so any supervisor can capture the log. */
final class Log
{
	public static function info(string $message): void
	{
		self::write(STDOUT, 'INFO', $message);
	}

	public static function warn(string $message): void
	{
		self::write(STDERR, 'WARN', $message);
	}

	public static function error(string $message): void
	{
		self::write(STDERR, 'ERROR', $message);
	}

	/** @param resource $stream */
	private static function write($stream, string $level, string $message): void
	{
		fwrite($stream, sprintf("%s %s %s\n", gmdate('Y-m-d\TH:i:s\Z'), $level, $message));
	}
}
