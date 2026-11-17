<?php

declare(strict_types=1);

namespace PorkForAor;

final class AtomicFile
{
    /** Joins a root and a relative path written with '/', and refuses any path that escapes the root. */
    public static function join(string $root, string $relative): string
    {
        if ($relative === '' || $relative[0] === '/' || $relative[0] === '\\' || preg_match('~(^|[\\\\/])\.\.([\\\\/]|$)|:~', $relative) === 1) {
            throw new \RuntimeException('Refused path outside the export root: ' . $relative);
        }

        return $root . DIRECTORY_SEPARATOR . str_replace('/', DIRECTORY_SEPARATOR, $relative);
    }

	/**
	 * Publishes a file. An identical body is only touched, so the modification time keeps meaning
	 * "last validated by the exporter" (section 12.4). A different body replaces the file by rename.
	 *
	 * @return bool true when the body was written, false when it was only touched
	 */
	public static function publish(string $path, string $content): bool
	{
		if (is_file($path) && file_get_contents($path) === $content) {
			self::touch($path);

			return false;
		}
		self::write($path, $content);

		return true;
	}

	public static function write(string $path, string $content): void
	{
		$dir = dirname($path);
		if (!is_dir($dir) && !mkdir($dir, 0775, true) && !is_dir($dir)) {
			throw new \RuntimeException('Cannot create ' . $dir);
		}
		$temp = $dir . DIRECTORY_SEPARATOR . '.pork-tmp-' . bin2hex(random_bytes(6));
		if (file_put_contents($temp, $content) === false) {
			throw new \RuntimeException('Cannot write ' . $temp);
		}
		if (!rename($temp, $path)) {
			@unlink($temp);
			throw new \RuntimeException('Cannot replace ' . $path);
		}
	}

	public static function touch(string $path): void
	{
		if (!touch($path)) {
			throw new \RuntimeException('Cannot touch ' . $path);
		}
	}
}
