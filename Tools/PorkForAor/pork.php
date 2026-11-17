<?php

declare(strict_types=1);

use PorkForAor\Config;
use PorkForAor\ConfigException;
use PorkForAor\Db;
use PorkForAor\Exporter;
use PorkForAor\Exporters\CharacterExporter;
use PorkForAor\Exporters\OnlineExporter;
use PorkForAor\Exporters\OrgStatsExporter;
use PorkForAor\GameData;
use PorkForAor\Kind;
use PorkForAor\Log;
use PorkForAor\Runner;
use PorkForAor\State;
use PorkForAor\Sweep;

if (PHP_VERSION_ID < 80200) {
	fwrite(STDERR, "PHP 8.2 or later is required.\n");
	exit(2);
}

spl_autoload_register(static function (string $class): void {
	$prefix = 'PorkForAor\\';
	if (strncmp($class, $prefix, strlen($prefix)) !== 0) {
		return;
	}
	$file = __DIR__ . DIRECTORY_SEPARATOR . 'src' . DIRECTORY_SEPARATOR
		. str_replace('\\', DIRECTORY_SEPARATOR, substr($class, strlen($prefix))) . '.php';
	if (is_file($file)) {
		require $file;
	}
});

date_default_timezone_set('UTC');

/** @return list<Exporter> */
function exporters(Config $config): array
{
	$gameData = new GameData($config->gameDataPath);

	return [
		new CharacterExporter($config, $gameData),
		new OrgStatsExporter($config, $gameData),
		new OnlineExporter($config, $gameData),
	];
}

function check(Config $config): int
{
	$failures = [];

	foreach (['export' => $config->exportPath, 'state' => $config->statePath] as $label => $path) {
		if (!is_dir($path) && !@mkdir($path, 0775, true) && !is_dir($path)) {
			$failures[] = sprintf('Cannot create the %s folder %s.', $label, $path);
		} elseif (!is_writable($path)) {
			$failures[] = sprintf('The %s folder %s is not writable.', $label, $path);
		}
	}

	$missing = (new GameData($config->gameDataPath))->missingFiles();
	if ($missing !== []) {
		$failures[] = sprintf('GameData files missing in %s: %s.', $config->gameDataPath, implode(', ', $missing));
	}

	try {
		$db = Db::connect($config);
		foreach (Kind::cases() as $kind) {
			$statement = $db->prepare('SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = ?');
			$statement->execute([$kind->metaTable()]);
			if ((int) $statement->fetchColumn() === 0) {
				$failures[] = sprintf('Table %s does not exist.', $kind->metaTable());
			}
		}
	} catch (\Throwable $e) {
		$failures[] = 'Database: ' . $e->getMessage();
	}

	foreach ($failures as $failure) {
		Log::error($failure);
	}
	if ($failures === []) {
		Log::info('Check passed.');
	}

	return $failures === [] ? 0 : 1;
}

$command = $argv[1] ?? 'run';
if (!in_array($command, ['run', 'sweep', 'check'], true)) {
	fwrite(STDERR, "Usage: php pork.php [run|sweep|check]\n");
	exit(2);
}

try {
	$config = Config::load(__DIR__);
	if ($command === 'check') {
		exit(check($config));
	}

	$state = new State($config->statePath . DIRECTORY_SEPARATOR . 'state.json');
	$sweep = new Sweep($config, exporters($config));

	if ($command === 'sweep') {
		$sweep->run(Db::connect($config));
		$state->setLastSweep(time());
		$state->save();
		exit(0);
	}

	exit((new Runner($config, $state, exporters($config), $sweep))->run());
} catch (ConfigException $e) {
	Log::error($e->getMessage());
	exit(2);
} catch (\Throwable $e) {
	Log::error(get_class($e) . ': ' . $e->getMessage());
	exit(1);
}
