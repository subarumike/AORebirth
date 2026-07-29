using System.Collections.Generic;

namespace ZoneEngine.Core.Arete;

public sealed class AreteAggregateValidationStageReport
{
	private readonly List<string> messages = new List<string>();

	public string Name { get; private set; }

	public bool Executed { get; private set; }

	public IEnumerable<string> Messages => messages;

	public int ErrorCount => messages.Count;

	public int WarningCount => 0;

	public bool IsValid => ErrorCount == 0;

	public AreteAggregateValidationStageReport(string name)
	{
		Name = name;
	}

	public void MarkExecuted()
	{
		Executed = true;
	}

	public void AddMessage(string message)
	{
		MarkExecuted();
		if (string.IsNullOrWhiteSpace(message))
		{
			message = "validation error";
		}
		messages.Add(message);
	}
}
