<?php

declare(strict_types=1);

namespace PorkForAor;

/** Defaults shared by the exporters. */
abstract class BaseExporter implements Exporter
{
	public function removeWhenAbsent(): bool
	{
		return false;
	}

	public function usesHeartbeat(): bool
	{
		return false;
	}

	public function comparable(string $relativePath, string $content): string
	{
		return $content;
	}

	protected function pending(string $what): never
	{
		throw new NotImplementedYet($what . ' is not written yet.');
	}
}
