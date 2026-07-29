using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine.Core.Arete.Quests;

namespace ZoneEngine.Core.Arete;

public static class AreteFrameworkBootstrap
{
	private static readonly object SyncRoot = new object();

	private static readonly string[] CheckedInManifestRelativePaths = new string[7]
	{
		Path.Combine("Content", "Arete", "rex-larsson", "manifest.json"),
		Path.Combine("Content", "Arete", "marcus-stone", "manifest.json"),
		Path.Combine("Content", "Arete", "flint-novak", "manifest.json"),
		Path.Combine("Content", "Subway", "windcaller-karrec", "manifest.json"),
		Path.Combine("Content", "Subway", "tailor", "manifest.json"),
		Path.Combine("Content", "Thrak", "garden-key", "manifest.json"),
		Path.Combine("Content", "Thrak", "garden-vendors", "manifest.json")
	};

	private static AreteFrameworkRegistries current;

	public static AreteFrameworkRegistries Current
	{
		get
		{
			lock (SyncRoot)
			{
				if (current == null)
				{
					current = CreateCheckedInContent(AppDomain.CurrentDomain.BaseDirectory);
				}
				return current;
			}
		}
	}

	public static AreteFrameworkRegistries InitializeCheckedInContent()
	{
		return InitializeCheckedInContent(AppDomain.CurrentDomain.BaseDirectory);
	}

	public static AreteFrameworkRegistries InitializeCheckedInContent(string runtimeBaseDirectory)
	{
		AreteFrameworkRegistries areteFrameworkRegistries = CreateCheckedInContent(runtimeBaseDirectory);
		lock (SyncRoot)
		{
			current = areteFrameworkRegistries;
			return current;
		}
	}

	public static AreteFrameworkRegistries LoadManifestSet(IEnumerable<string> manifestPaths)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		List<string> list3 = new List<string>(manifestPaths ?? Enumerable.Empty<string>());
		if (list3.Count == 0)
		{
			areteValidationResult.AddError("contentManifests", "no content manifest paths were provided");
		}
		AreteContentManifestLoader areteContentManifestLoader = new AreteContentManifestLoader();
		foreach (string item in list3)
		{
			AreteContentManifestLoadResult areteContentManifestLoadResult = areteContentManifestLoader.Load(item);
			areteValidationResult.AddErrors(areteContentManifestLoadResult.Validation);
			if (areteContentManifestLoadResult.IsValid)
			{
				list.AddRange(areteContentManifestLoadResult.DialoguePackFiles);
				list2.AddRange(areteContentManifestLoadResult.QuestPackFiles);
			}
		}
		AreteContentLoadResult<DialogueContentPack> areteContentLoadResult = new DialogueContentPackLoader().LoadFiles(list);
		AreteContentLoadResult<QuestContentPack> areteContentLoadResult2 = new QuestContentPackLoader().LoadFiles(list2);
		areteValidationResult.AddErrors(areteContentLoadResult.Validation);
		areteValidationResult.AddErrors(areteContentLoadResult2.Validation);
		DialogueContentRegistry dialogueContentRegistry = new DialogueContentRegistry();
		QuestContentRegistry questContentRegistry = new QuestContentRegistry();
		if (!areteValidationResult.IsValid)
		{
			return new AreteFrameworkRegistries(dialogueContentRegistry, questContentRegistry, areteValidationResult);
		}
		areteValidationResult.AddErrors(dialogueContentRegistry.Load(areteContentLoadResult.Packs));
		areteValidationResult.AddErrors(questContentRegistry.Load(areteContentLoadResult2.Packs));
		if (areteValidationResult.IsValid)
		{
			areteValidationResult.AddErrors(DialogueActionReferenceValidator.Validate(areteContentLoadResult.Packs, questContentRegistry));
			areteValidationResult.AddErrors(AreteConditionReferenceValidator.Validate(areteContentLoadResult.Packs, areteContentLoadResult2.Packs, dialogueContentRegistry, questContentRegistry));
		}
		if (!areteValidationResult.IsValid)
		{
			dialogueContentRegistry = new DialogueContentRegistry();
			questContentRegistry = new QuestContentRegistry();
		}
		return new AreteFrameworkRegistries(dialogueContentRegistry, questContentRegistry, areteValidationResult);
	}

	public static AreteFrameworkRegistries InitializeEmptyRegistries()
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		DialogueContentRegistry dialogueContentRegistry = new DialogueContentRegistry();
		QuestContentRegistry questContentRegistry = new QuestContentRegistry();
		areteValidationResult.AddErrors(dialogueContentRegistry.Load(Enumerable.Empty<DialogueContentPack>()));
		areteValidationResult.AddErrors(questContentRegistry.Load(Enumerable.Empty<QuestContentPack>()));
		return new AreteFrameworkRegistries(dialogueContentRegistry, questContentRegistry, areteValidationResult);
	}

	private static AreteFrameworkRegistries CreateCheckedInContent(string runtimeBaseDirectory)
	{
		if (string.IsNullOrWhiteSpace(runtimeBaseDirectory))
		{
			throw new ArgumentException("A runtime base directory is required.", "runtimeBaseDirectory");
		}
		string fullBaseDirectory = Path.GetFullPath(runtimeBaseDirectory);
		AreteFrameworkRegistries areteFrameworkRegistries = LoadManifestSet(CheckedInManifestRelativePaths.Select((string relativePath) => Path.Combine(fullBaseDirectory, relativePath)));
		if (!areteFrameworkRegistries.IsValid)
		{
			throw new InvalidDataException("Checked-in dialogue and quest content failed validation:" + Environment.NewLine + string.Join(Environment.NewLine, areteFrameworkRegistries.Validation.Errors));
		}
		return areteFrameworkRegistries;
	}
}
