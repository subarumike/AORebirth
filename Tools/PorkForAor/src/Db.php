<?php

declare(strict_types=1);

namespace PorkForAor;

final class Db
{
	public static function connect(Config $config): \PDO
	{
		$dsn = sprintf('mysql:host=%s;port=%d;dbname=%s;charset=utf8mb4', $config->dbHost, $config->dbPort, $config->dbName);
		$pdo = new \PDO($dsn, $config->dbUser, $config->dbPassword, [
			\PDO::ATTR_ERRMODE => \PDO::ERRMODE_EXCEPTION,
			\PDO::ATTR_DEFAULT_FETCH_MODE => \PDO::FETCH_ASSOC,
			\PDO::ATTR_EMULATE_PREPARES => false,
		]);
		// Defense in depth: the exporter never writes (section 10).
		$pdo->exec('SET SESSION TRANSACTION READ ONLY');

		return $pdo;
	}
}
