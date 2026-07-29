using System;
using System.Collections.Generic;
using System.Text;
using AORebirth.Core.Entities;

namespace ZoneEngine.Core;

internal static class SoothingSpiritsHealPetLadder
{
	public const int BaseMetapetHealingTextureId = 288730;

	private const int SoothingSpirits1PacketId = 720;

	private static readonly byte[] MetapetHealingName = Encoding.ASCII.GetBytes("metapet_healing\0");

	private static readonly HashSet<string> SoothingSpiritsUpgradeHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LYNX", "JBOB", "DKEL", "QRMT", "MNKW", "RHEF" };

	private static readonly Dictionary<string, int> TextureTierBySpawnHash = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
	{
		{ "MT01", 0 },
		{ "MT02", 0 },
		{ "MT03", 0 },
		{ "MT04", 0 },
		{ "BSLX", 0 },
		{ "LYNX", 1 },
		{ "JBOB", 2 },
		{ "DKEL", 3 },
		{ "QRMT", 4 },
		{ "MNKW", 5 },
		{ "RHEF", 6 }
	};

	public static bool IsSoothingSpiritsUpgradeHash(string petHash)
	{
		return !string.IsNullOrWhiteSpace(petHash) && SoothingSpiritsUpgradeHashes.Contains(petHash);
	}

	public static int GetHighestRank(ICharacter owner)
	{
		Character val = (Character)(object)((owner is Character) ? owner : null);
		if (val == null)
		{
			return 0;
		}
		val.EnsureTrainedPerks();
		for (int num = 10; num >= 1; num--)
		{
			if (val.HasPerk(720 + num - 1))
			{
				return num;
			}
		}
		return 0;
	}

	public static int ResolveTextureTierFromRank(int soothingSpiritsRank)
	{
		if (soothingSpiritsRank <= 0)
		{
			return 0;
		}
		if (soothingSpiritsRank <= 2)
		{
			return 1;
		}
		if (soothingSpiritsRank <= 4)
		{
			return 2;
		}
		if (soothingSpiritsRank <= 6)
		{
			return 3;
		}
		if (soothingSpiritsRank <= 8)
		{
			return 4;
		}
		if (soothingSpiritsRank == 9)
		{
			return 5;
		}
		return 6;
	}

	public static int ResolveTextureIdFromRank(int soothingSpiritsRank)
	{
		return 288730 + ResolveTextureTierFromRank(soothingSpiritsRank);
	}

	public static int ResolveTextureId(ICharacter owner)
	{
		return ResolveTextureIdFromRank(GetHighestRank(owner));
	}

	public static int ResolveTextureIdFromSpawnHash(string petHash, ICharacter owner)
	{
		if (!string.IsNullOrWhiteSpace(petHash) && TextureTierBySpawnHash.TryGetValue(petHash, out var value) && (value > 0 || IsBaseHealPetHash(petHash)))
		{
			int val = 288730 + value;
			int val2 = ResolveTextureId(owner);
			return Math.Max(val, val2);
		}
		return ResolveTextureId(owner);
	}

	public static bool TryPatchMetapetHealingTexture(byte[] data, int textureId)
	{
		if (data == null || textureId <= 0)
		{
			return false;
		}
		int num = IndexOf(data, MetapetHealingName);
		if (num < 0)
		{
			return false;
		}
		int num2 = num + MetapetHealingName.Length + 16;
		if (num2 + 4 > data.Length)
		{
			return false;
		}
		data[num2] = (byte)((uint)(textureId >> 24) & 0xFFu);
		data[num2 + 1] = (byte)((uint)(textureId >> 16) & 0xFFu);
		data[num2 + 2] = (byte)((uint)(textureId >> 8) & 0xFFu);
		data[num2 + 3] = (byte)((uint)textureId & 0xFFu);
		return true;
	}

	private static bool IsBaseHealPetHash(string petHash)
	{
		return string.Equals(petHash, "MT01", StringComparison.OrdinalIgnoreCase) || string.Equals(petHash, "MT02", StringComparison.OrdinalIgnoreCase) || string.Equals(petHash, "MT03", StringComparison.OrdinalIgnoreCase) || string.Equals(petHash, "MT04", StringComparison.OrdinalIgnoreCase) || string.Equals(petHash, "BSLX", StringComparison.OrdinalIgnoreCase);
	}

	private static int IndexOf(byte[] haystack, byte[] needle)
	{
		if (haystack == null || needle == null || needle.Length == 0 || haystack.Length < needle.Length)
		{
			return -1;
		}
		for (int i = 0; i <= haystack.Length - needle.Length; i++)
		{
			bool flag = true;
			for (int j = 0; j < needle.Length; j++)
			{
				if (haystack[i + j] != needle[j])
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				return i;
			}
		}
		return -1;
	}
}
