using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete.Quests;

public sealed class QuestContentPackLoader
{
	public AreteContentLoadResult<QuestContentPack> LoadFile(string filePath)
	{
		return LoadFiles(new string[1] { filePath });
	}

	public AreteContentLoadResult<QuestContentPack> LoadDirectory(string directoryPath)
	{
		return AreteJsonContentFileLoader.LoadDirectory<QuestContentPack>(directoryPath, "quest", QuestContentPackValidator.Validate);
	}

	public AreteContentLoadResult<QuestContentPack> LoadFiles(IEnumerable<string> filePaths)
	{
		return AreteJsonContentFileLoader.LoadFiles<QuestContentPack>(filePaths, "quest", QuestContentPackValidator.Validate);
	}

	public AreteContentLoadResult<QuestContentPack> LoadManifest(string manifestPath)
	{
		AreteContentManifestLoadResult areteContentManifestLoadResult = new AreteContentManifestLoader().Load(manifestPath);
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		areteValidationResult.AddErrors(areteContentManifestLoadResult.Validation);
		if (!areteContentManifestLoadResult.IsValid)
		{
			return new AreteContentLoadResult<QuestContentPack>(Enumerable.Empty<QuestContentPack>(), areteValidationResult);
		}
		AreteContentLoadResult<QuestContentPack> areteContentLoadResult = LoadFiles(areteContentManifestLoadResult.QuestPackFiles);
		areteValidationResult.AddErrors(areteContentLoadResult.Validation);
		return new AreteContentLoadResult<QuestContentPack>(areteContentLoadResult.Packs, areteValidationResult);
	}

	public AreteContentLoadResult<QuestContentPack> Load(IEnumerable<QuestContentPack> packs)
	{
		List<QuestContentPack> packs2 = new List<QuestContentPack>(packs ?? Enumerable.Empty<QuestContentPack>());
		AreteValidationResult validation = QuestContentPackValidator.Validate(packs2);
		return new AreteContentLoadResult<QuestContentPack>(packs2, validation);
	}

	public AreteContentLoadResult<QuestContentPack> LoadEmpty()
	{
		return Load(Enumerable.Empty<QuestContentPack>());
	}
}
