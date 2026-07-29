using System;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using AORebirth.Core.Textures;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Packets;

namespace ZoneEngine.Core;

internal static class PetBureaucratGuardianAppearance
{
	private const int CorporateGuardianNanoId = 235386;

	private const int CeoGuardianNanoId = 273300;

	private const int GuardianBodyMeshId = 273304;

	private const int CorporateGuardianWeaponId = 154505;

	private const int CeoGuardianWeaponId = 273306;

	private const int GuardianWeaponQuality = 200;

	private const int CorporateGuardianWeaponFlags = 1027;

	private const int CeoGuardianWeaponFlags = 1025;

	private const int GuardianBodyMeshPosition = 1;

	private const int GuardianBodyMeshLayer = 2;

	public static bool IsGuardianNano(int summonNanoId)
	{
		return summonNanoId == 235386 || summonNanoId == 273300;
	}

	public static bool IsGuardianPet(ICharacter pet)
	{
		return ResolveSummonNanoId(pet) > 0;
	}

	public static int ResolveSummonNanoId(ICharacter pet)
	{
		if (pet == null || !PetCombatRules.IsPlayerOwnedPet(pet))
		{
			return 0;
		}
		if (string.Equals(((INamedEntity)pet).Name, "CEO Guardian", StringComparison.Ordinal))
		{
			return 273300;
		}
		if (string.Equals(((INamedEntity)pet).Name, "Corporate Guardian", StringComparison.Ordinal))
		{
			return 235386;
		}
		return 0;
	}

	public static void Apply(Character petCharacter, int summonNanoId)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Expected O, but got Unknown
		if (petCharacter != null && IsGuardianNano(summonNanoId))
		{
			((Dynel)petCharacter).Textures.Clear();
			for (int i = 0; i < 5; i++)
			{
				((Dynel)petCharacter).Textures.Add(new AOTextures(i, 0));
			}
			((Dynel)petCharacter).MeshLayer.Clear();
			petCharacter.SocialMeshLayer.Clear();
			((Dynel)petCharacter).MeshLayer.AddMesh(1, 273304, 0, 2);
			petCharacter.SocialMeshLayer.AddMesh(1, 273304, 0, 2);
			TryEquipGuardianWeapon(petCharacter, summonNanoId);
		}
	}

	public static SimpleCharFullUpdateMessage BuildAnnounceVisualUpdate(Character petCharacter, int summonNanoId)
	{
		SimpleCharFullUpdateMessage val = SimpleCharFullUpdate.ConstructMessage(petCharacter);
		PetSummonScfuExtensions.ApplyCapturedGuardianPetMetadata(val, summonNanoId);
		return val;
	}

	private static bool TryEquipGuardianWeapon(Character petCharacter, int summonNanoId)
	{
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Expected O, but got Unknown
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Invalid comparison between Unknown and I4
		if (petCharacter == null || ((Dynel)petCharacter).BaseInventory == null)
		{
			return false;
		}
		int num = ((summonNanoId == 273300) ? 273306 : 154505);
		int flags = ((summonNanoId == 273300) ? 1025 : 1027);
		PetShellDisplayItemCatalog.EnsureRegistered(num, num);
		if (!ItemLoader.ItemList.ContainsKey(num))
		{
			return false;
		}
		if (!((Dynel)petCharacter).BaseInventory.Pages.TryGetValue(101, out var value))
		{
			return false;
		}
		int num2 = 8;
		if (value.ValidSlot(num2) && value[num2] != null)
		{
			value.Remove(num2);
		}
		int num3 = 6;
		if (!value.ValidSlot(num3))
		{
			return false;
		}
		if (value[num3] != null)
		{
			value.Remove(num3);
		}
		Item val = new Item(200, num, num)
		{
			MultipleCount = 1,
			Flags = flags
		};
		return (int)value.Add(num3, (IItem)(object)val) == 0;
	}
}
