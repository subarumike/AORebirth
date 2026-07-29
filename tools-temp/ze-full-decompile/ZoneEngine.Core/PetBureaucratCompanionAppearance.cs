using System;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using AORebirth.Core.Textures;
using AORebirth.Database.Dao;

namespace ZoneEngine.Core;

internal static class PetBureaucratCompanionAppearance
{
	private const int CarloTorsoMeshId = 204896;

	private const int CarlitaTorsoMeshId = 204913;

	private const int CompanionBackMeshId = 29084;

	private const int CompanionBriefcaseItemId = 258219;

	private const int CompanionBriefcaseQuality = 100;

	public static void Apply(Character petCharacter, DBMobTemplate mobTemplate)
	{
		if (petCharacter != null && mobTemplate != null)
		{
			((Dynel)petCharacter).Textures.Clear();
			AddTexture(petCharacter, 0, mobTemplate.TextureHands);
			AddTexture(petCharacter, 1, mobTemplate.TextureBody);
			AddTexture(petCharacter, 2, mobTemplate.TextureFeet);
			AddTexture(petCharacter, 3, mobTemplate.TextureArms);
			AddTexture(petCharacter, 4, mobTemplate.TextureLegs);
			if (mobTemplate.HeadMesh > 0)
			{
				((Dynel)petCharacter).MeshLayer.Clear();
				petCharacter.SocialMeshLayer.Clear();
				((Dynel)petCharacter).MeshLayer.AddMesh(0, mobTemplate.HeadMesh, 0, 4);
				petCharacter.SocialMeshLayer.AddMesh(0, mobTemplate.HeadMesh, 0, 4);
			}
			if (TryResolveTorsoMeshId(mobTemplate.Hash, out var torsoMeshId))
			{
				((Dynel)petCharacter).MeshLayer.AddMesh(2, torsoMeshId, 0, 0);
				petCharacter.SocialMeshLayer.AddMesh(2, torsoMeshId, 0, 0);
				((Dynel)petCharacter).MeshLayer.AddMesh(1, 29084, 0, 2);
				petCharacter.SocialMeshLayer.AddMesh(1, 29084, 0, 2);
				TryEquipBriefcase(petCharacter);
			}
		}
	}

	public static bool TryEquipBriefcase(Character petCharacter)
	{
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected O, but got Unknown
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Invalid comparison between Unknown and I4
		if (petCharacter == null || ((Dynel)petCharacter).BaseInventory == null)
		{
			return false;
		}
		if (!ItemLoader.ItemList.ContainsKey(258219))
		{
			return false;
		}
		if (!((Dynel)petCharacter).BaseInventory.Pages.TryGetValue(101, out var value))
		{
			return false;
		}
		int num = 6;
		if (!value.ValidSlot(num) || value[num] != null)
		{
			return false;
		}
		Item val = new Item(100, 258219, 258219)
		{
			MultipleCount = 1
		};
		return (int)value.Add(num, (IItem)(object)val) == 0;
	}

	private static bool TryResolveTorsoMeshId(string mobHash, out int torsoMeshId)
	{
		if (string.Equals(mobHash, "A142", StringComparison.OrdinalIgnoreCase))
		{
			torsoMeshId = 204896;
			return true;
		}
		if (string.Equals(mobHash, "CRLT", StringComparison.OrdinalIgnoreCase))
		{
			torsoMeshId = 204913;
			return true;
		}
		torsoMeshId = 0;
		return false;
	}

	private static void AddTexture(Character petCharacter, int place, int textureId)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		if (textureId > 0)
		{
			((Dynel)petCharacter).Textures.Add(new AOTextures(place, textureId));
		}
	}
}
