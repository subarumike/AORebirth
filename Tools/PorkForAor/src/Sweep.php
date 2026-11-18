<?php

declare(strict_types=1);

namespace PorkForAor;

/** Daily reconciliation of published files with the database (section 9.1). */
final class Sweep
{
	/** @param list<Exporter> $exporters */
	public function __construct(private readonly Config $config, private readonly array $exporters)
	{
	}

	public function run(\PDO $db): void
	{
		$started = microtime(true);
		foreach ($this->exporters as $exporter) {
			try {
				$this->sweepKind($db, $exporter);
			} catch (NotImplementedYet $e) {
				Log::warn(sprintf('Sweep skipped for %s: %s', $exporter->kind()->value, $e->getMessage()));
			}
		}
		$this->purgeQuarantine();
		Log::info(sprintf('Sweep finished in %.1f s.', microtime(true) - $started));
	}

	private function sweepKind(\PDO $db, Exporter $exporter): void
	{
		$kind = $exporter->kind()->value;
		$published = $exporter->publishedKeys($this->config->exportPath);
		$existing = $exporter->existingKeys($db);
		$orphans = array_diff_key($published, $existing);

		if ($orphans !== [] && $this->removalIsSafe(count($orphans), count($published), count($existing), $kind)) {
			foreach ($orphans as $files) {
				foreach ($files as $relative) {
					$this->quarantine($kind, $relative);
				}
			}
			Log::info(sprintf('Sweep %s: %d orphan entities removed.', $kind, count($orphans)));
		}

		$rewritten = 0;
		foreach (array_keys($existing) as $key) {
			try {
				$files = $exporter->render($db, (string) $key);
			} catch (EntityExportException $e) {
				Log::error($e->getMessage());
				continue;
			}
			if ($files === null) {
				continue;
			}
			foreach ($files as $relative => $body) {
				$path = AtomicFile::join($this->config->exportPath, $relative);
				if (is_file($path)) {
					$current = (string) file_get_contents($path);
					if ($exporter->comparable($relative, $current) === $exporter->comparable($relative, $body)) {
						continue;
					}
					// A real difference with no meta change means an engine hook is missing (section 5.5).
					Log::warn(sprintf('Sweep %s: %s differs from the database (missing hook?).', $kind, $relative));
				}
				AtomicFile::write($path, $body);
				++$rewritten;
			}
		}
		Log::info(sprintf('Sweep %s: %d files written.', $kind, $rewritten));
	}

	private function removalIsSafe(int $orphans, int $published, int $existing, string $kind): bool
	{
		if ($existing === 0) {
			Log::error(sprintf('Sweep %s: the database returned no entity, removal aborted.', $kind));

			return false;
		}
		$percent = $orphans * 100 / max(1, $published);
		if ($percent > $this->config->sweepAbortPercent) {
			Log::error(sprintf(
				'Sweep %s: %d of %d entities (%.1f%%) would be removed, above %d%%. Removal aborted.',
				$kind,
				$orphans,
				$published,
				$percent,
				$this->config->sweepAbortPercent
			));

			return false;
		}

		return true;
	}

	private function quarantine(string $kind, string $relative): void
	{
		$source = AtomicFile::join($this->config->exportPath, $relative);
		if (!is_file($source)) {
			return;
		}
		if ($this->config->quarantineDays === 0) {
			unlink($source);

			return;
		}
		$target = AtomicFile::join($this->config->statePath . DIRECTORY_SEPARATOR . 'quarantine' . DIRECTORY_SEPARATOR . $kind, $relative);
		$dir = dirname($target);
		if (!is_dir($dir) && !mkdir($dir, 0775, true) && !is_dir($dir)) {
			throw new \RuntimeException('Cannot create ' . $dir);
		}
		if (!rename($source, $target)) {
			throw new \RuntimeException('Cannot move to quarantine: ' . $source);
		}
		// The retention period counts from the move, not from the last render.
		touch($target);
	}

	private function purgeQuarantine(): void
	{
		$root = $this->config->statePath . DIRECTORY_SEPARATOR . 'quarantine';
		if (!is_dir($root)) {
			return;
		}
		$limit = time() - $this->config->quarantineDays * 86400;
		$files = new \RecursiveIteratorIterator(new \RecursiveDirectoryIterator($root, \FilesystemIterator::SKIP_DOTS));
		foreach ($files as $file) {
			if ($file->isFile() && $file->getMTime() < $limit) {
				unlink($file->getPathname());
			}
		}
	}
}
