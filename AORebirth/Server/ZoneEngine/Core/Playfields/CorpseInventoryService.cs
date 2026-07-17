namespace AORebirth.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Enums;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using ZoneEngine.Core;

    internal sealed class CorpseLootItem
    {
        internal int Slot { get; set; }
        internal Item Item { get; set; }
        internal Identity LootIdentity { get; set; }
        internal bool Looted { get; set; }
    }

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
        internal TimeSpan? CloseWithLootCleanupDelay { get; set; }
        internal int InventoryHandle { get; set; }
        internal List<CorpseLootItem> LootItems { get; set; }
        internal int Credits { get; set; }
        internal bool CreditsLooted { get; set; }
        internal bool Opened { get; set; }
        internal CorpseLootRightsPolicy RightsPolicy { get; set; }
        internal LootGenerationResult GenerationResult { get; set; }
        internal bool LootUnresolved { get; set; }

        internal bool HasUnlootedItems
        {
            get { return this.LootItems != null && this.LootItems.Any(x => !x.Looted); }
        }

        internal bool IsEmpty
        {
            get { return !this.HasUnlootedItems && (this.Credits <= 0 || this.CreditsLooted); }
        }
    }

    internal sealed class CorpseInventoryService
    {
        private readonly Dictionary<int, CorpseState> states = new Dictionary<int, CorpseState>();
        private readonly object sync = new object();

        internal IDictionary<int, CorpseState> States { get { return this.states; } }

        internal CorpseState Create(CorpseState state)
        {
            if (state == null || state.CorpseIdentity.Type != IdentityType.Corpse)
                throw new ArgumentException("A corpse identity is required.", "state");
            lock (this.sync)
            {
                if (this.states.ContainsKey(state.CorpseIdentity.Instance))
                    throw new InvalidOperationException("Duplicate corpse identity: " + state.CorpseIdentity);
                state.LootItems = state.LootItems ?? new List<CorpseLootItem>();
                state.VisibleRecipients = state.VisibleRecipients ?? new HashSet<Identity>();
                state.LastMutationAtUtc = state.CreatedAtUtc;
                this.states.Add(state.CorpseIdentity.Instance, state);
                return state;
            }
        }

        internal bool TryGet(Identity corpseIdentity, out CorpseState state)
        {
            lock (this.sync) return this.states.TryGetValue(corpseIdentity.Instance, out state);
        }

        internal CorpseState Get(Identity corpseIdentity)
        {
            CorpseState state;
            return this.TryGet(corpseIdentity, out state) ? state : null;
        }

        internal CorpseLootItem[] EnumerateItems(Identity corpseIdentity)
        {
            lock (this.sync)
            {
                CorpseState state;
                return this.states.TryGetValue(corpseIdentity.Instance, out state)
                    ? state.LootItems.Where(x => !x.Looted).OrderBy(x => x.Slot).ToArray()
                    : new CorpseLootItem[0];
            }
        }

        internal bool RemoveItem(Identity corpseIdentity, int slot, DateTime mutationAtUtc)
        {
            lock (this.sync)
            {
                CorpseState state;
                if (!this.states.TryGetValue(corpseIdentity.Instance, out state)) return false;
                CorpseLootItem item = state.LootItems.FirstOrDefault(x => x.Slot == slot && !x.Looted);
                if (item == null) return false;
                item.Looted = true;
                state.LastMutationAtUtc = mutationAtUtc;
                return true;
            }
        }

        internal bool RemoveCredits(Identity corpseIdentity, DateTime mutationAtUtc)
        {
            lock (this.sync)
            {
                CorpseState state;
                if (!this.states.TryGetValue(corpseIdentity.Instance, out state)
                    || state.CreditsLooted || state.Credits <= 0) return false;
                state.CreditsLooted = true;
                state.LastMutationAtUtc = mutationAtUtc;
                return true;
            }
        }

        internal void MarkOpened(Identity corpseIdentity, bool opened, DateTime mutationAtUtc)
        {
            lock (this.sync)
            {
                CorpseState state;
                if (!this.states.TryGetValue(corpseIdentity.Instance, out state)) return;
                state.Opened = opened;
                state.LastMutationAtUtc = mutationAtUtc;
            }
        }

        internal bool IsEmpty(Identity corpseIdentity)
        {
            lock (this.sync)
            {
                CorpseState state;
                return this.states.TryGetValue(corpseIdentity.Instance, out state) && state.IsEmpty;
            }
        }

        internal bool Remove(int corpseInstance)
        {
            lock (this.sync) return this.states.Remove(corpseInstance);
        }

        internal int ClearPlayfield(int playfieldId)
        {
            lock (this.sync)
            {
                int[] keys = this.states.Where(x => x.Value.PlayfieldId == playfieldId).Select(x => x.Key).ToArray();
                foreach (int key in keys) this.states.Remove(key);
                return keys.Length;
            }
        }

        internal void ClearAll()
        {
            lock (this.sync) this.states.Clear();
        }
    }
}
