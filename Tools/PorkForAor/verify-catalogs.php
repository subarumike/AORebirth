<?php

declare(strict_types=1);

use PorkForAor\GameData;

require __DIR__ . '/src/Log.php';
require __DIR__ . '/src/GameData.php';

function requireCatalog(bool $condition, string $message): void
{
	if (!$condition) {
		throw new RuntimeException($message);
	}
}

if (PHP_SAPI !== 'cli' || $argc !== 2 || !is_dir($argv[1])) {
	fwrite(STDERR, "Usage: php verify-catalogs.php <selected GameData root>\n");
	exit(64);
}

try {
	$data = new GameData($argv[1]);
	$missing = $data->missingFiles();
	requireCatalog($missing === [], 'Missing PORK catalogs: ' . implode(', ', $missing));
	foreach (GameData::REQUIRED as $name) {
		requireCatalog(is_array($data->load($name)), $name . ' did not load as a catalog');
	}

	$names = [
		['Breed.json', 'Breeds', $data->breed(...)],
		['Sex.json', 'Sex', $data->sex(...)],
		['Side.json', 'Sides', $data->side(...)],
		['Profession.json', 'Professions', $data->profession(...)],
		['GoverningForms.json', 'Forms', $data->governingForm(...)],
	];
	foreach ($names as [$file, $section, $reader]) {
		$rows = $data->load($file)[$section] ?? [];
		requireCatalog(is_array($rows) && $rows !== [], $file . ' has no named identities');
		foreach ($rows as $id => $row) {
			$name = $row['Name'] ?? '';
			requireCatalog(is_int($id) && trim($name) !== '' && $reader($id) === $name, $file . ' lookup failed');
			if ($section === 'Forms') {
				$ranks = $row['Ranks'] ?? [];
				requireCatalog(is_array($ranks) && $ranks !== [], 'Organization rank list is missing');
				foreach ($ranks as $rank => $rankName) {
					requireCatalog($data->orgRank($id, $rank) === $rankName, 'Organization rank lookup failed');
				}
			}
		}
	}

	$texts = [];
	foreach ($data->load('Text.json') as $row) {
		$category = $row['category'];
		$id = $row['id'];
		requireCatalog(!isset($texts[$category][$id]), 'Text.json has duplicate text identities');
		$texts[$category][$id] = $row['text'];
	}
	$titles = $data->load('PvpLegacyTitles.json');
	$bands = $titles['Bands'] ?? [];
	requireCatalog($bands !== [], 'Legacy PvP title bands are missing');
	foreach ($bands as $band) {
		$id = $band['TextId'];
		$expected = $id === null ? $band['Fallback'] : ($texts[$titles['TextCategory']][$id] ?? '');
		requireCatalog(trim($expected) !== '' && $data->pvpTitle($band['MinRating']) === $expected,
			'Legacy PvP title failed to resolve at its rating boundary');
	}

	$reflection = new ReflectionClass(GameData::class);
	$professionTitles = $texts[$reflection->getConstant('PROFESSION_TITLE_CATEGORY')] ?? [];
	$defenderRanks = $texts[$reflection->getConstant('DEFENDER_RANK_CATEGORY')] ?? [];
	$professions = $data->load('Profession.json')['Professions'];
	$titleCount = 0;
	$rankCount = 0;
	foreach ($professionTitles as $id => $expected) {
		$profession = intdiv($id, 100);
		$title = $id % 100;
		if (isset($professions[$profession]) && $title > 0 && $expected !== '') {
			requireCatalog($data->professionTitle($profession, $title) === $expected, 'Profession title lookup failed');
			$titleCount++;
		}
	}
	foreach ($defenderRanks as $id => $expected) {
		if ($id >= 0 && $id % 100 === 0 && $expected !== '') {
			requireCatalog($data->defenderRank(intdiv($id, 100)) === $expected, 'Defender rank lookup failed');
			$rankCount++;
		}
	}
	requireCatalog($titleCount > 0 && $rankCount > 0, 'PORK text lookup categories have no readable titles or ranks');
	printf("PORK_CATALOG_LOADER=PASS catalogs=%d pvp_bands=%d profession_titles=%d defender_ranks=%d\n",
		count(GameData::REQUIRED), count($bands), $titleCount, $rankCount);
} catch (Throwable $error) {
	fwrite(STDERR, 'PORK_CATALOG_LOADER=FAIL ' . $error->getMessage() . "\n");
	exit(1);
}
