<?php

declare(strict_types=1);

namespace PorkForAor;

/**
 * Settings from the process environment first, then from the .env file (docs/api/character-bio-api.md, section 14).
 */
final class Config
{
	private const KEYS = [
		'PORK_DB_HOST', 'PORK_DB_PORT', 'PORK_DB_NAME', 'PORK_DB_USER', 'PORK_DB_PASSWORD',
		'PORK_EXPORT_PATH', 'PORK_STATE_PATH', 'AO_REBIRTH_GAMEDATA_PATH', 'PORK_DIMENSION',
		'PORK_TICK_SECONDS', 'PORK_OVERLAP_SECONDS', 'PORK_SWEEP_HOURS', 'PORK_QUARANTINE_DAYS',
		'PORK_SWEEP_ABORT_PERCENT', 'PORK_DEBUG_HEARTBEAT',
	];

	public readonly string $appDir;
	public readonly string $dbHost;
	public readonly int $dbPort;
	public readonly string $dbName;
	public readonly string $dbUser;
	public readonly string $dbPassword;
	public readonly string $exportPath;
	public readonly string $statePath;
	public readonly string $gameDataPath;
	public readonly int $dimension;
	public readonly int $tickSeconds;
	public readonly int $overlapSeconds;
	public readonly int $sweepHours;
	public readonly int $quarantineDays;
	public readonly int $sweepAbortPercent;
	public readonly bool $debugHeartbeat;

	/** @param array<string,string> $values */
	private function __construct(string $appDir, array $values)
	{
		$this->appDir = $appDir;
		$this->dbHost = self::required($values, 'PORK_DB_HOST');
		$this->dbPort = self::integer($values, 'PORK_DB_PORT', 3306, 1, 65535);
		$this->dbName = self::required($values, 'PORK_DB_NAME');
		$this->dbUser = self::required($values, 'PORK_DB_USER');
		$this->dbPassword = $values['PORK_DB_PASSWORD'] ?? '';
		$this->exportPath = self::path($appDir, self::required($values, 'PORK_EXPORT_PATH'));
		$this->statePath = self::path($appDir, $values['PORK_STATE_PATH'] ?? 'state');
		$this->gameDataPath = self::path($appDir, self::required($values, 'AO_REBIRTH_GAMEDATA_PATH'));
		$this->dimension = self::integer($values, 'PORK_DIMENSION', 11, 1, 65535);
		$this->tickSeconds = self::integer($values, 'PORK_TICK_SECONDS', 30, 1, 3600);
		$this->overlapSeconds = self::integer($values, 'PORK_OVERLAP_SECONDS', 60, 0, 86400);
		$this->sweepHours = self::integer($values, 'PORK_SWEEP_HOURS', 24, 1, 8760);
		$this->quarantineDays = self::integer($values, 'PORK_QUARANTINE_DAYS', 7, 0, 3650);
		$this->sweepAbortPercent = self::integer($values, 'PORK_SWEEP_ABORT_PERCENT', 5, 0, 100);
		$this->debugHeartbeat = self::integer($values, 'PORK_DEBUG_HEARTBEAT', 0, 0, 1) === 1;
	}

	public static function load(string $appDir): self
	{
		$values = [];
		$file = $appDir . DIRECTORY_SEPARATOR . '.env';
		if (is_file($file)) {
			$values = self::parseFile($file);
		}
		// The process environment wins over .env (AO_REBIRTH_GAMEDATA_PATH is the existing engine variable).
		foreach (self::KEYS as $key) {
			$env = getenv($key);
			if ($env !== false && $env !== '') {
				$values[$key] = $env;
			}
		}

		return new self($appDir, $values);
	}

	/** @return array<string,string> */
	private static function parseFile(string $file): array
	{
		$lines = file($file, FILE_IGNORE_NEW_LINES);
		if ($lines === false) {
			throw new ConfigException('Cannot read ' . $file);
		}
		$values = [];
		foreach ($lines as $number => $line) {
			$line = trim($line);
			if ($line === '' || $line[0] === '#') {
				continue;
			}
			$position = strpos($line, '=');
			if ($position === false) {
				throw new ConfigException(sprintf('%s line %d: expected KEY=VALUE.', $file, $number + 1));
			}
			$key = trim(substr($line, 0, $position));
			$value = trim(substr($line, $position + 1));
			$length = strlen($value);
			if ($length >= 2 && ($value[0] === '"' || $value[0] === "'") && $value[$length - 1] === $value[0]) {
				$value = substr($value, 1, -1);
			}
			$values[$key] = $value;
		}

		return $values;
	}

	/** @param array<string,string> $values */
	private static function required(array $values, string $key): string
	{
		$value = $values[$key] ?? '';
		if ($value === '') {
			throw new ConfigException('Missing setting ' . $key . ' (see .env.example).');
		}

		return $value;
	}

	/** @param array<string,string> $values */
	private static function integer(array $values, string $key, int $default, int $min, int $max): int
	{
		if (!isset($values[$key]) || $values[$key] === '') {
			return $default;
		}
		$filtered = filter_var($values[$key], FILTER_VALIDATE_INT);
		if ($filtered === false || $filtered < $min || $filtered > $max) {
			throw new ConfigException(sprintf('%s must be an integer from %d to %d.', $key, $min, $max));
		}

		return $filtered;
	}

	private static function path(string $appDir, string $path): string
	{
		$absolute = preg_match('~^([A-Za-z]:[\\\\/]|[\\\\/])~', $path) === 1;
		$full = $absolute ? $path : $appDir . DIRECTORY_SEPARATOR . $path;

		return rtrim($full, '\\/');
	}
}
