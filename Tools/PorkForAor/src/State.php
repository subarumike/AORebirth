<?php

declare(strict_types=1);

namespace PorkForAor;

/** Watermarks and last sweep time, kept outside the served folder (section 9). */
final class State
{
	private const INITIAL_WATERMARK = '1970-01-01 00:00:00';

	/** @var array<string,string> */
	private array $watermarks = [];
	private int $lastSweep = 0;

	public function __construct(private readonly string $file)
	{
		if (!is_file($file)) {
			return;
		}
		$data = json_decode((string) file_get_contents($file), true);
		if (!is_array($data)) {
			Log::warn('State file unreadable, starting from scratch: ' . $file);

			return;
		}
		foreach (Kind::cases() as $kind) {
			$value = $data['watermarks'][$kind->value] ?? null;
			if (is_string($value) && $value !== '') {
				$this->watermarks[$kind->value] = $value;
			}
		}
		$this->lastSweep = (int) ($data['last_sweep'] ?? 0);
	}

	public function watermark(Kind $kind): string
	{
		return $this->watermarks[$kind->value] ?? self::INITIAL_WATERMARK;
	}

	public function setWatermark(Kind $kind, string $value): void
	{
		$this->watermarks[$kind->value] = $value;
	}

	public function resetWatermark(Kind $kind): void
	{
		unset($this->watermarks[$kind->value]);
	}

	public function lastSweep(): int
	{
		return $this->lastSweep;
	}

	public function setLastSweep(int $time): void
	{
		$this->lastSweep = $time;
	}

	public function save(): void
	{
		AtomicFile::write($this->file, json_encode([
			'watermarks' => $this->watermarks,
			'last_sweep' => $this->lastSweep,
		], JSON_PRETTY_PRINT | JSON_THROW_ON_ERROR));
	}
}
