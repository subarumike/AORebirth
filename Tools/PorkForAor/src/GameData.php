<?php

declare(strict_types=1);

namespace PorkForAor;

/** Read-only access to the GameData JSON selected by AO_REBIRTH_GAMEDATA_PATH. Private data is never copied out. */
final class GameData
{
	/** Files the renderers need (section 4.3). */
	public const REQUIRED = [
		'Breed.json', 'Sex.json', 'Side.json', 'Profession.json', 'GoverningForms.json', 'Text.json', 'PvpLegacyTitles.json',
	];

	private const PROFESSION_TITLE_CATEGORY = 1060;
	private const DEFENDER_RANK_CATEGORY = 1169;

	/** Level from which each TitleLevel starts (section 5.1). */
	private const TITLE_LEVELS = [205 => 7, 190 => 6, 150 => 5, 100 => 4, 40 => 3, 15 => 2, 1 => 1];

	/** @var array<string,mixed> */
	private array $cache = [];

	/** @var array<int,array<int,string>>|null category => id => text */
	private ?array $texts = null;

	/** @var array<string,true> */
	private array $warned = [];

	public function __construct(private readonly string $root)
	{
	}

	/** @return list<string> the required files that are missing */
	public function missingFiles(): array
	{
		return array_values(array_filter(
			self::REQUIRED,
			fn (string $name): bool => !is_file($this->root . DIRECTORY_SEPARATOR . $name)
		));
	}

	public function load(string $name): mixed
	{
		if (!array_key_exists($name, $this->cache)) {
			$path = $this->root . DIRECTORY_SEPARATOR . $name;
			$json = is_file($path) ? file_get_contents($path) : false;
			if ($json === false) {
				throw new \RuntimeException('GameData file not found: ' . $path);
			}
			$this->cache[$name] = json_decode($json, true, 512, JSON_THROW_ON_ERROR);
		}

		return $this->cache[$name];
	}

	public function breed(int $id): string
	{
		return $this->name('Breed.json', 'Breeds', $id);
	}

	public function sex(int $id): string
	{
		return $this->name('Sex.json', 'Sex', $id);
	}

	public function side(int $id): string
	{
		return $this->name('Side.json', 'Sides', $id);
	}

	public function profession(int $id): string
	{
		return $this->name('Profession.json', 'Professions', $id);
	}

	public function governingForm(int $form): string
	{
		return $this->name('GoverningForms.json', 'Forms', $form);
	}

	/** Rank names of a form are listed by rank index (section 5.1). */
	public function orgRank(int $form, int $rank): string
	{
		$ranks = $this->load('GoverningForms.json')['Forms'][$form]['Ranks'] ?? null;
		$name = is_array($ranks) ? ($ranks[$rank] ?? null) : null;
		if (!is_string($name)) {
			$this->warn(sprintf('GoverningForms.json has no rank %d for form %d', $rank, $form));

			return '';
		}

		return $name;
	}

	/** TitleLevel from stat 37, or derived from the level when the stat is absent (section 5.1). */
	public function titleLevel(int $stat37, int $level): int
	{
		if ($stat37 > 0) {
			return $stat37;
		}
		foreach (self::TITLE_LEVELS as $from => $title) {
			if ($level >= $from) {
				return $title;
			}
		}

		return 0;
	}

	public function professionTitle(int $profession, int $titleLevel): string
	{
		if ($profession === 0 || $titleLevel === 0) {
			return '';
		}

		return $this->text(self::PROFESSION_TITLE_CATEGORY, $profession * 100 + $titleLevel);
	}

	public function defenderRank(int $alienLevel): string
	{
		return $this->text(self::DEFENDER_RANK_CATEGORY, $alienLevel * 100);
	}

	/** Legacy PvP title: the highest band reached, empty below the first band (section 5.2a). */
	public function pvpTitle(int $rating): string
	{
		$data = $this->load('PvpLegacyTitles.json');
		$category = (int) ($data['TextCategory'] ?? 0);
		$title = '';
		$reached = -1;
		foreach ($data['Bands'] ?? [] as $band) {
			$min = (int) $band['MinRating'];
			if ($rating < $min || $min <= $reached) {
				continue;
			}
			$reached = $min;
			$text = $band['TextId'] === null ? '' : $this->texts()[$category][(int) $band['TextId']] ?? '';
			$title = $text !== '' ? $text : (string) ($band['Fallback'] ?? '');
		}

		return $title;
	}

	private function text(int $category, int $id): string
	{
		$text = $this->texts()[$category][$id] ?? null;
		if ($text === null) {
			$this->warn(sprintf('Text.json has no entry %d in category %d', $id, $category));

			return '';
		}

		return $text;
	}

	/** @return array<int,array<int,string>> */
	private function texts(): array
	{
		if ($this->texts === null) {
			$this->texts = [];
			foreach ($this->load('Text.json') as $row) {
				$this->texts[(int) $row['category']][(int) $row['id']] = (string) $row['text'];
			}
		}

		return $this->texts;
	}

	private function name(string $file, string $section, int $id): string
	{
		$name = $this->load($file)[$section][$id]['Name'] ?? null;
		if (!is_string($name)) {
			// Id 0 is the normal value of an absent stat: empty, not a warning (section 8).
			if ($id !== 0) {
				$this->warn(sprintf('%s has no entry %d', $file, $id));
			}

			return '';
		}

		return $name;
	}

	/** One warning per missing entry, so a render loop does not flood the log. */
	private function warn(string $message): void
	{
		if (!isset($this->warned[$message])) {
			$this->warned[$message] = true;
			Log::warn('GameData: ' . $message);
		}
	}
}
