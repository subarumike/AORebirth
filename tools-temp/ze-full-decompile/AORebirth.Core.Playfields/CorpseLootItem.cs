using AORebirth.Core.Items;
using SmokeLounge.AOtomation.Messaging.GameData;

namespace AORebirth.Core.Playfields;

internal sealed class CorpseLootItem
{
	internal int Slot { get; set; }

	internal Item Item { get; set; }

	internal Identity LootIdentity { get; set; }

	internal bool Looted { get; set; }
}
