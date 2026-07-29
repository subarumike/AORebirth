using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete.Dialogue;

public sealed class DialogueContentPackLoader
{
	public AreteContentLoadResult<DialogueContentPack> LoadFile(string filePath)
	{
		return LoadFiles(new string[1] { filePath });
	}

	public AreteContentLoadResult<DialogueContentPack> LoadDirectory(string directoryPath)
	{
		return AreteJsonContentFileLoader.LoadDirectory<DialogueContentPack>(directoryPath, "dialogue", DialogueContentPackValidator.Validate);
	}

	public AreteContentLoadResult<DialogueContentPack> LoadFiles(IEnumerable<string> filePaths)
	{
		return AreteJsonContentFileLoader.LoadFiles<DialogueContentPack>(filePaths, "dialogue", DialogueContentPackValidator.Validate);
	}

	public AreteContentLoadResult<DialogueContentPack> LoadManifest(string manifestPath)
	{
		AreteContentManifestLoadResult areteContentManifestLoadResult = new AreteContentManifestLoader().Load(manifestPath);
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		areteValidationResult.AddErrors(areteContentManifestLoadResult.Validation);
		if (!areteContentManifestLoadResult.IsValid)
		{
			return new AreteContentLoadResult<DialogueContentPack>(Enumerable.Empty<DialogueContentPack>(), areteValidationResult);
		}
		AreteContentLoadResult<DialogueContentPack> areteContentLoadResult = LoadFiles(areteContentManifestLoadResult.DialoguePackFiles);
		areteValidationResult.AddErrors(areteContentLoadResult.Validation);
		return new AreteContentLoadResult<DialogueContentPack>(areteContentLoadResult.Packs, areteValidationResult);
	}

	public AreteContentLoadResult<DialogueContentPack> Load(IEnumerable<DialogueContentPack> packs)
	{
		List<DialogueContentPack> packs2 = new List<DialogueContentPack>(packs ?? Enumerable.Empty<DialogueContentPack>());
		AreteValidationResult validation = DialogueContentPackValidator.Validate(packs2);
		return new AreteContentLoadResult<DialogueContentPack>(packs2, validation);
	}

	public AreteContentLoadResult<DialogueContentPack> LoadEmpty()
	{
		return Load(Enumerable.Empty<DialogueContentPack>());
	}
}
