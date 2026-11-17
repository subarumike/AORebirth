<?php

declare(strict_types=1);

namespace PorkForAor;

/**
 * One loop. Each tick reads the three meta tables, renders the changed entities and touches the online files
 * that did not change (sections 9, 12.3 and 12.4). The daily sweep runs from the same loop.
 */
final class Runner
{
	private bool $stop = false;
	private bool $started = false;
	private ?\PDO $db = null;

	/** @var array<string,true> */
	private array $warnedPending = [];

	/** @param list<Exporter> $exporters */
	public function __construct(
		private readonly Config $config,
		private readonly State $state,
		private readonly array $exporters,
		private readonly Sweep $sweep
	) {
	}

	public function run(): int
	{
		$this->installStopHandlers();
		Log::info(sprintf(
			'Started: tick %d s, overlap %d s, sweep %d h, export %s.',
			$this->config->tickSeconds,
			$this->config->overlapSeconds,
			$this->config->sweepHours,
			$this->config->exportPath
		));

		while (!$this->stop) {
			$begin = microtime(true);
			try {
				$db = $this->db ??= Db::connect($this->config);
				if (!$this->started) {
					$this->startup();
					$this->started = true;
				}
				$stats = $this->pass($db);
				$stats['pass_ms'] = (int) round((microtime(true) - $begin) * 1000);
				$this->report($stats);
				if ($this->sweepIsDue()) {
					$this->sweep->run($db);
					$this->state->setLastSweep(time());
					$this->state->save();
				}
			} catch (\Throwable $e) {
				Log::error(get_class($e) . ': ' . $e->getMessage());
				$this->db = null;
			}
			$this->sleepUntil($begin + $this->config->tickSeconds);
		}
		Log::info('Stopped.');

		return 0;
	}

	/** At start, every online list is rendered again from the database (section 12.4). */
	private function startup(): void
	{
		foreach ($this->exporters as $exporter) {
			if ($exporter->usesHeartbeat()) {
				$this->state->resetWatermark($exporter->kind());
			}
		}
	}

	/** @return array<string,mixed> */
	private function pass(\PDO $db): array
	{
		$stats = ['tables' => [], 'touched' => 0, 'touch_ms' => 0, 'renders' => 0, 'render_ms' => 0];
		foreach ($this->exporters as $exporter) {
			$kind = $exporter->kind();
			try {
				$queryStart = microtime(true);
				$changes = (new MetaReader($db, $kind))->changedSince($this->state->watermark($kind), $this->config->overlapSeconds);
				$stats['tables'][$kind->value] = [
					'rows' => count($changes['ids']),
					'query_ms' => (int) round((microtime(true) - $queryStart) * 1000),
				];

				$renderStart = microtime(true);
				$keys = $changes['ids'] === [] ? [] : $exporter->keysForMetaIds($db, $changes['ids']);
				foreach ($keys as $key) {
					$this->renderKey($db, $exporter, $key);
				}
				$stats['renders'] += count($keys);
				$stats['render_ms'] += (int) round((microtime(true) - $renderStart) * 1000);

				if ($changes['max'] !== null) {
					$this->state->setWatermark($kind, $changes['max']);
				}
				if ($exporter->usesHeartbeat()) {
					$touchStart = microtime(true);
					$stats['touched'] += $this->heartbeat($exporter, array_flip($keys));
					$stats['touch_ms'] += (int) round((microtime(true) - $touchStart) * 1000);
				}
			} catch (NotImplementedYet $e) {
				if (!isset($this->warnedPending[$kind->value])) {
					$this->warnedPending[$kind->value] = true;
					Log::warn(sprintf('%s skipped: %s', $kind->value, $e->getMessage()));
				}
			}
		}
		$this->state->save();

		return $stats;
	}

	private function renderKey(\PDO $db, Exporter $exporter, string $key): void
	{
		try {
			$files = $exporter->render($db, $key);
		} catch (EntityExportException $e) {
			Log::error($e->getMessage());

			return;
		}
		if ($files === null) {
			if ($exporter->removeWhenAbsent()) {
				foreach ($exporter->publishedKeys($this->config->exportPath)[$key] ?? [] as $relative) {
					$path = AtomicFile::join($this->config->exportPath, $relative);
					if (is_file($path)) {
						unlink($path);
					}
				}
			}

			return;
		}
		foreach ($files as $relative => $body) {
			AtomicFile::publish(AtomicFile::join($this->config->exportPath, $relative), $body);
		}
	}

	/**
	 * Touches the published files of the entities that were not rendered in this pass.
	 *
	 * @param array<string,int> $rendered keys rendered in this pass
	 */
	private function heartbeat(Exporter $exporter, array $rendered): int
	{
		$touched = 0;
		foreach ($exporter->publishedKeys($this->config->exportPath) as $key => $files) {
			if (isset($rendered[$key])) {
				continue;
			}
			foreach ($files as $relative) {
				$path = AtomicFile::join($this->config->exportPath, $relative);
				if (is_file($path)) {
					AtomicFile::touch($path);
					++$touched;
				}
			}
		}

		return $touched;
	}

	private function sweepIsDue(): bool
	{
		return time() - $this->state->lastSweep() >= $this->config->sweepHours * 3600;
	}

	/** @param array<string,mixed> $stats */
	private function report(array $stats): void
	{
		$passMs = (int) $stats['pass_ms'];
		if ($this->config->debugHeartbeat) {
			$tables = [];
			foreach ($stats['tables'] as $name => $table) {
				$tables[] = sprintf('%s rows=%d query=%dms', $name, $table['rows'], $table['query_ms']);
			}
			Log::info(sprintf(
				'heartbeat pass=%dms touched=%d (%dms) renders=%d (%dms) | %s',
				$passMs,
				$stats['touched'],
				$stats['touch_ms'],
				$stats['renders'],
				$stats['render_ms'],
				implode(' | ', $tables)
			));
		}
		if ($passMs > $this->config->tickSeconds * 1000) {
			Log::warn(sprintf('Pass took %d ms, longer than the %d s tick.', $passMs, $this->config->tickSeconds));
		}
	}

	private function sleepUntil(float $deadline): void
	{
		while (!$this->stop && microtime(true) < $deadline) {
			usleep(200000);
			if (function_exists('pcntl_signal_dispatch')) {
				pcntl_signal_dispatch();
			}
		}
	}

	private function installStopHandlers(): void
	{
		$stop = function (): void {
			$this->stop = true;
		};
		if (function_exists('pcntl_signal')) {
			pcntl_async_signals(true);
			pcntl_signal(SIGTERM, $stop);
			pcntl_signal(SIGINT, $stop);
		} elseif (function_exists('sapi_windows_set_ctrl_handler')) {
			sapi_windows_set_ctrl_handler($stop);
		}
	}
}
