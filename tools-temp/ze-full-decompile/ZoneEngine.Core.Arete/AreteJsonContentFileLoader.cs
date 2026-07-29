using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace ZoneEngine.Core.Arete;

public static class AreteJsonContentFileLoader
{
	public static AreteContentLoadResult<TPack> LoadDirectory<TPack>(string directoryPath, string contentType, Func<IEnumerable<TPack>, AreteValidationResult> validate)
	{
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		string directoryLocation = GetDirectoryLocation(contentType, directoryPath);
		if (string.IsNullOrWhiteSpace(directoryPath))
		{
			areteValidationResult.AddError(directoryLocation, "missing JSON content directory path");
			return new AreteContentLoadResult<TPack>(Enumerable.Empty<TPack>(), areteValidationResult);
		}
		if (!Directory.Exists(directoryPath))
		{
			areteValidationResult.AddError(directoryLocation, "JSON content directory was not found");
			return new AreteContentLoadResult<TPack>(Enumerable.Empty<TPack>(), areteValidationResult);
		}
		string[] array = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly).OrderBy((string filePath) => filePath, StringComparer.OrdinalIgnoreCase).ToArray();
		if (array.Length == 0)
		{
			areteValidationResult.AddError(directoryLocation, "JSON content directory did not contain JSON content files");
			return new AreteContentLoadResult<TPack>(Enumerable.Empty<TPack>(), areteValidationResult);
		}
		return LoadFiles(array, contentType, validate);
	}

	public static AreteContentLoadResult<TPack> LoadFiles<TPack>(IEnumerable<string> filePaths, string contentType, Func<IEnumerable<TPack>, AreteValidationResult> validate)
	{
		List<TPack> list = new List<TPack>();
		AreteValidationResult areteValidationResult = new AreteValidationResult();
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer
		{
			MaxJsonLength = int.MaxValue
		};
		int num = 0;
		foreach (string item in filePaths ?? Enumerable.Empty<string>())
		{
			string location = GetLocation(contentType, item, num);
			if (string.IsNullOrWhiteSpace(item))
			{
				areteValidationResult.AddError(location, "missing JSON content file path");
				num++;
				continue;
			}
			if (!File.Exists(item))
			{
				areteValidationResult.AddError(location, "JSON content file was not found");
				num++;
				continue;
			}
			try
			{
				string input = File.ReadAllText(item);
				TPack val = javaScriptSerializer.Deserialize<TPack>(input);
				if (val == null)
				{
					areteValidationResult.AddError(location, "JSON content file did not contain a content pack");
				}
				else
				{
					list.Add(val);
				}
			}
			catch (Exception ex)
			{
				areteValidationResult.AddError(location, "failed to parse JSON content file: " + ex.GetType().Name + ": " + ex.Message);
			}
			num++;
		}
		if (validate != null)
		{
			areteValidationResult.AddErrors(validate(list));
		}
		return new AreteContentLoadResult<TPack>(list, areteValidationResult);
	}

	private static string GetDirectoryLocation(string contentType, string directoryPath)
	{
		if (!string.IsNullOrWhiteSpace(directoryPath))
		{
			return directoryPath;
		}
		if (string.IsNullOrWhiteSpace(contentType))
		{
			contentType = "content";
		}
		return contentType + "Directory";
	}

	private static string GetLocation(string contentType, string filePath, int fileIndex)
	{
		if (!string.IsNullOrWhiteSpace(filePath))
		{
			return filePath;
		}
		if (string.IsNullOrWhiteSpace(contentType))
		{
			contentType = "content";
		}
		return contentType + "File[" + fileIndex + "]";
	}
}
