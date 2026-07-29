using System.Collections.Generic;
using System.Linq;

namespace ZoneEngine.Core.Arete;

public sealed class AreteContentLoadResult<TPack>
{
	public IList<TPack> Packs { get; private set; }

	public AreteValidationResult Validation { get; private set; }

	public bool IsValid => Validation.IsValid;

	public AreteContentLoadResult(IEnumerable<TPack> packs, AreteValidationResult validation)
	{
		Packs = new List<TPack>(packs ?? Enumerable.Empty<TPack>());
		Validation = validation ?? new AreteValidationResult();
	}
}
