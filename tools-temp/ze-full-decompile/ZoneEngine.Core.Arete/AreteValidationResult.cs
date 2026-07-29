using System.Collections.Generic;

namespace ZoneEngine.Core.Arete;

public sealed class AreteValidationResult
{
	private readonly List<string> errors = new List<string>();

	public IEnumerable<string> Errors => errors;

	public int ErrorCount => errors.Count;

	public bool IsValid => errors.Count == 0;

	public void AddError(string location, string message)
	{
		if (string.IsNullOrWhiteSpace(location))
		{
			location = "arete";
		}
		errors.Add(location + ": " + message);
	}

	public void AddErrors(AreteValidationResult result)
	{
		if (result == null)
		{
			return;
		}
		foreach (string error in result.Errors)
		{
			errors.Add(error);
		}
	}
}
