using System;

namespace AORebirth.Core.Playfields;

internal sealed class LootDefinitionValidationException : Exception
{
	internal LootDefinitionValidationException(string message)
		: base(message)
	{
	}
}
