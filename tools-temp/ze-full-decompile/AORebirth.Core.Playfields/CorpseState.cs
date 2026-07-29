using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Entities;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core;

namespace AORebirth.Core.Playfields;

internal sealed class CorpseState
{
	internal Identity CorpseIdentity { get; set; }

	internal Identity DeadNpcIdentity { get; set; }

	internal int PlayfieldId { get; set; }

	internal ICharacter VisualSource { get; set; }

	internal HashSet<Identity> VisibleRecipients { get; set; }

	internal string Name { get; set; }

	internal CombatCorpseLootClass LootClass { get; set; }

	internal DateTime CreatedAtUtc { get; set; }

	internal DateTime LastMutationAtUtc { get; set; }

	internal DateTime SpawnsAtUtc { get; set; }

	internal DateTime ExpiresAtUtc { get; set; }

	internal TimeSpan ItemLootLifetime { get; set; }

	internal TimeSpan EmptyCleanupDelay { get; set; }

	internal int InventoryHandle { get; set; }

	internal List<CorpseLootItem> LootItems { get; set; }

	internal int Credits { get; set; }

	internal bool CreditsLooted { get; set; }

	internal bool Opened { get; set; }

	internal CorpseLootRightsPolicy RightsPolicy { get; set; }

	internal LootGenerationResult GenerationResult { get; set; }

	internal bool LootUnresolved { get; set; }

	internal bool HasUnlootedItems => LootItems != null && LootItems.Any((CorpseLootItem x) => !x.Looted);

	internal bool IsEmpty => !HasUnlootedItems && (Credits <= 0 || CreditsLooted);
}
