using AORebirth.Core.Entities;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace ZoneEngine.Core;

internal static class PetSummonScfuExtensions
{
	private static readonly byte[] CapturedHealingPetUnknown1 = new byte[27]
	{
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 3, 1, 0, 1, 0, 1, 0, 1,
		0, 1, 0, 0, 0, 2, 0
	};

	private static readonly byte[] CapturedHealingPetExtendedTextureOverrideData = new byte[125]
	{
		0, 0, 2, 225, 0, 0, 7, 226, 109, 101,
		116, 97, 112, 101, 116, 95, 104, 101, 97, 108,
		105, 110, 103, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		4, 103, 218, 0, 0, 0, 0, 0, 0, 0,
		1, 0, 0, 3, 241, 0, 0, 23, 166, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 1, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 2, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 3, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 4,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		63, 16, 0, 0, 2
	};

	private static readonly byte[] CapturedGuardianExtendedTextureOverrideData = new byte[133]
	{
		15, 196, 104, 101, 108, 108, 102, 97, 99, 101,
		50, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 3, 108, 85, 0, 0,
		0, 0, 0, 0, 0, 1, 104, 101, 108, 108,
		50, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 3, 108,
		86, 0, 0, 0, 0, 0, 0, 0, 0, 104,
		101, 108, 108, 49, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 3, 108, 86, 0, 0, 0, 0, 0,
		0, 0, 0
	};

	public static void ApplyCapturedMpPetMetadata(SimpleCharFullUpdateMessage message, int petSlotStrain, ICharacter owner = null, string spawnPetHash = null)
	{
		if (message != null && petSlotStrain == 1016)
		{
			message.Unknown1 = (byte[])CapturedHealingPetUnknown1.Clone();
			byte[] array = (byte[])CapturedHealingPetExtendedTextureOverrideData.Clone();
			int textureId = SoothingSpiritsHealPetLadder.ResolveTextureIdFromSpawnHash(spawnPetHash, owner);
			SoothingSpiritsHealPetLadder.TryPatchMetapetHealingTexture(array, textureId);
			message.ExtendedTextureOverrideData = array;
			message.VisualFlags = 31;
			message.Expansions = 1;
		}
	}

	public static void ApplyCapturedGuardianPetMetadata(SimpleCharFullUpdateMessage message, int summonNanoId)
	{
		if (message != null && PetBureaucratGuardianAppearance.IsGuardianNano(summonNanoId))
		{
			message.Unknown1 = (byte[])CapturedHealingPetUnknown1.Clone();
			message.ExtendedTextureOverrideData = (byte[])CapturedGuardianExtendedTextureOverrideData.Clone();
			message.VisualFlags = 31;
			message.Expansions = 0;
		}
	}

	public static byte[] CloneGuardianExtendedTextureOverrideData()
	{
		return (byte[])CapturedGuardianExtendedTextureOverrideData.Clone();
	}
}
