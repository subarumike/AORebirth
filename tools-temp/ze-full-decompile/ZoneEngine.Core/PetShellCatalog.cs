using SmokeLounge.AOtomation.Messaging.GameData;

namespace ZoneEngine.Core;

internal static class PetShellCatalog
{
	private static readonly PetShellDefinition EngineerShell = new PetShellDefinition(PetShellKind.Engineer, 43328, 1, 43324, "PT50", 1);

	private static readonly PetShellDefinition BureaucratShell = new PetShellDefinition(PetShellKind.Bureaucrat, 96235, 1, 46397, "A020", 2, 150722);

	private static readonly PetShellDefinition MetaPhysicistShell = new PetShellDefinition(PetShellKind.MetaPhysicist, 204709, 1, 43723, "PT52", 52);

	public static bool TryGet(PetShellKind kind, out PetShellDefinition definition)
	{
		switch (kind)
		{
		case PetShellKind.Engineer:
			definition = EngineerShell;
			return true;
		case PetShellKind.Bureaucrat:
			definition = BureaucratShell;
			return true;
		case PetShellKind.MetaPhysicist:
			definition = MetaPhysicistShell;
			return true;
		default:
			definition = null;
			return false;
		}
	}

	public static bool TryGetShellItemForProfession(int profession, out int shellItemId, out int shellQuality)
	{
		if (!TryGet(ResolveKind(profession), out var definition))
		{
			shellItemId = 0;
			shellQuality = 0;
			return false;
		}
		shellItemId = definition.DisplayItemLowId;
		shellQuality = definition.DisplayQuality;
		return true;
	}

	public static bool UsesShellOnSummon(int profession)
	{
		return UsesShellOnSummon(profession, 0);
	}

	public static bool UsesShellOnSummon(int profession, int nanoId)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Invalid comparison between Unknown and I4
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Invalid comparison between Unknown and I4
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Invalid comparison between Unknown and I4
		if (PetSummonNanoCatalog.IsDirectSummonNano(nanoId))
		{
			return false;
		}
		Profession val = (Profession)profession;
		Profession val2 = val;
		if ((int)val2 != 3 && (int)val2 != 8)
		{
			if ((int)val2 == 12)
			{
				return false;
			}
			return true;
		}
		return true;
	}

	public static PetShellKind ResolveKind(int profession)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Invalid comparison between Unknown and I4
		Profession val = (Profession)profession;
		Profession val2 = val;
		if ((int)val2 != 3)
		{
			if ((int)val2 != 8)
			{
				if ((int)val2 == 12)
				{
					return PetShellKind.MetaPhysicist;
				}
				return PetShellKind.Unknown;
			}
			return PetShellKind.Bureaucrat;
		}
		return PetShellKind.Engineer;
	}

	public static bool TryGetByDisplayLowId(int lowId, out PetShellDefinition definition)
	{
		if (lowId == EngineerShell.DisplayItemLowId)
		{
			definition = EngineerShell;
			return true;
		}
		if (lowId == BureaucratShell.DisplayItemLowId || PetSummonNanoCatalog.IsBureaucratShellItemLowId(lowId))
		{
			definition = BureaucratShell;
			return true;
		}
		if (lowId == MetaPhysicistShell.DisplayItemLowId)
		{
			definition = MetaPhysicistShell;
			return true;
		}
		definition = null;
		return false;
	}

	public static bool TryGetBureaucratFallback(out PetShellDefinition definition)
	{
		definition = BureaucratShell;
		return true;
	}
}
