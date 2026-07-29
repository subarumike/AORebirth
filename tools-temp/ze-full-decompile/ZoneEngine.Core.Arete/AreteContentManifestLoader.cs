using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace ZoneEngine.Core.Arete;

public sealed class AreteContentManifestLoader
{
	public AreteContentManifestLoadResult Load(string manifestPath)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		if (string.IsNullOrWhiteSpace(manifestPath))
		{
			areteValidationResult.AddError("contentManifest", "missing content manifest file path");
			return new AreteContentManifestLoadResult(list, list2, areteValidationResult);
		}
		if (!File.Exists(manifestPath))
		{
			areteValidationResult.AddError(manifestPath, "content manifest file was not found");
			return new AreteContentManifestLoadResult(list, list2, areteValidationResult);
		}
		AreteContentManifest areteContentManifest;
		try
		{
			JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer
			{
				MaxJsonLength = int.MaxValue
			};
			areteContentManifest = javaScriptSerializer.Deserialize<AreteContentManifest>(File.ReadAllText(manifestPath));
		}
		catch (Exception ex)
		{
			areteValidationResult.AddError(manifestPath, "failed to parse JSON manifest: " + ex.GetType().Name + ": " + ex.Message);
			return new AreteContentManifestLoadResult(list, list2, areteValidationResult);
		}
		if (areteContentManifest == null)
		{
			areteValidationResult.AddError(manifestPath, "JSON manifest did not contain a content manifest");
			return new AreteContentManifestLoadResult(list, list2, areteValidationResult);
		}
		string text = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
		if (string.IsNullOrWhiteSpace(text))
		{
			text = Directory.GetCurrentDirectory();
		}
		AddResolvedPaths(list, areteContentManifest.DialoguePacks, text, manifestPath, "DialoguePacks", areteValidationResult);
		AddResolvedPaths(list2, areteContentManifest.QuestPacks, text, manifestPath, "QuestPacks", areteValidationResult);
		return new AreteContentManifestLoadResult(list, list2, areteValidationResult);
	}

	private static void AddResolvedPaths(IList<string> resolvedPaths, IEnumerable<string> manifestPaths, string baseDirectory, string manifestPath, string collectionName, AreteValidationResult validation)
	{
		int num = 0;
		foreach (string item in manifestPaths ?? Enumerable.Empty<string>())
		{
			string location = manifestPath + "." + collectionName + "[" + num + "]";
			if (string.IsNullOrWhiteSpace(item))
			{
				validation.AddError(location, "missing manifest content file path");
				num++;
				continue;
			}
			try
			{
				string path = (Path.IsPathRooted(item) ? item : Path.Combine(baseDirectory, item));
				resolvedPaths.Add(Path.GetFullPath(path));
			}
			catch (Exception ex)
			{
				validation.AddError(location, "failed to resolve manifest content file path: " + ex.GetType().Name + ": " + ex.Message);
			}
			num++;
		}
	}
}
