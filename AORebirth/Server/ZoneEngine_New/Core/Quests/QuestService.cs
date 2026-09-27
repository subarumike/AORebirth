namespace ZoneEngine_New.Core.Quests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;

    using AORebirth.Database.Domain.Quests;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;

    /// <summary>
    /// Assigns quests to players and runs them. Template quests come from Quests.json and never expire;
    /// generated quests live once in <c>generatedquests</c> (definition, expiry, ACG data) and can be shared.
    /// Each player's state is a <c>characterquests</c> row, written straight away on every change. A player's log is only touched on that player's playfield.
    /// Kill and KillMultiple objectives advance from NPC kill credit; other actions are held but not driven yet.
    /// </summary>
    public sealed class QuestService
    {
        readonly QuestCatalog _catalog;
        readonly ICharacterQuestStore _store;
        readonly IItemBuilder _items;
        readonly InventoryFlushService _flush;
        readonly IZoneLogger _logger;
        readonly Func<DateTime> _now;

        public QuestService(
            QuestCatalog catalog,
            ICharacterQuestStore store,
            IItemBuilder items,
            InventoryFlushService flush,
            IZoneLogger logger)
            : this(catalog, store, items, flush, logger, () => DateTime.UtcNow)
        {
        }

        public QuestService(
            QuestCatalog catalog,
            ICharacterQuestStore store,
            IItemBuilder items,
            InventoryFlushService flush,
            IZoneLogger logger,
            Func<DateTime> now)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _flush = flush ?? throw new ArgumentNullException(nameof(flush));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _now = now ?? throw new ArgumentNullException(nameof(now));
        }

        public QuestCatalog Catalog => _catalog;

        /// <summary>The player's quests, loaded on first use. Expired generated quests are closed here.</summary>
        public QuestLog GetLog(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (player.QuestLog == null)
                player.QuestLog = Load(player);

            ExpireDue(player, player.QuestLog);
            return player.QuestLog;
        }

        /// <summary>
        /// The player deleted a journal entry. The client sends the delete request (QuestIIR_t, Gamecode.dll
        /// N3Msg_RemoveQuest) but leaves the entry until the server removes it. False when the id is not one of
        /// this player's active quests, so other quest systems can take it.
        /// </summary>
        public bool TryAbandon(Player player, int journalInstance)
        {
            ArgumentNullException.ThrowIfNull(player);
            foreach (PlayerQuest quest in GetLog(player).Quests.Values)
            {
                if (!quest.IsActive || QuestJournal.JournalInstance(quest.QuestId) != journalInstance)
                    continue;

                // NoDelete quests are blocked in the client already (flag 0x400); refuse a crafted request too.
                if (quest.Template.IsNoDelete)
                    return true;

                quest.State = QuestState.Abandoned;
                Save(player, quest);
                QuestJournal.Delete(player, quest);
                _logger.Info(string.Format(CultureInfo.InvariantCulture, "Quest abandoned char={0} quest={1} ({2})",
                    player.Identity.Instance, quest.QuestId, quest.Template.Name));
                return true;
            }

            return false;
        }

        /// <summary>World entry: loads the player's quests and puts every active one in the client journal.</summary>
        public void Restore(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            foreach (PlayerQuest quest in GetLog(player).Quests.Values)
            {
                if (quest.IsActive)
                    QuestJournal.Send(player, quest, announce: false);
            }
        }

        /// <summary>Gives a Quests.json quest. A completed or closed one is started over; an active one is refused.</summary>
        public bool TryAssignTemplate(Player player, string hash, out string result)
        {
            ArgumentNullException.ThrowIfNull(player);
            if (!_catalog.TryGet(hash, out QuestTemplate template))
            {
                result = "Unknown quest: " + hash;
                return false;
            }

            QuestLog log = GetLog(player);
            if (log.Quests.TryGetValue(template.Hash, out PlayerQuest? held) && held.IsActive)
            {
                result = string.Format(CultureInfo.InvariantCulture, "{0} already has quest {1} ({2}).", player.Name, template.Hash, template.Name);
                return false;
            }

            Assign(player, log, template.Hash, QuestSource.Template, template, expiresAtUtc: null, acgJson: null);
            result = string.Format(CultureInfo.InvariantCulture, "Gave quest {0} ({1}) to {2}.", template.Hash, template.Name, player.Name);
            return true;
        }

        /// <summary>
        /// Creates a generated quest in Quests.json format without assigning it: a terminal offer, or one quest a
        /// whole team shares. It gets its own id, expires at <paramref name="expiresAtUtc"/>, and may carry ACG
        /// building generator data. Returns the id to assign with <see cref="TryAssignGenerated"/>.
        /// </summary>
        public string CreateGenerated(QuestTemplate template, DateTime expiresAtUtc, QuestOwnerType ownerType, int ownerId,
            string? acgBuildingGeneratorJson = null)
        {
            ArgumentNullException.ThrowIfNull(template);

            string id = "G" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            DateTime now = _now();
            _store.SaveGenerated(new GeneratedQuestRow
            {
                QuestId = id,
                OwnerType = (int)ownerType,
                OwnerId = ownerId,
                DefinitionJson = JsonSerializer.Serialize(template, QuestTemplate.JsonOptions),
                AcgBuildingGeneratorJson = acgBuildingGeneratorJson,
                CreatedAtUtcTicks = now.Ticks,
                ExpiresAtUtcTicks = expiresAtUtc.Ticks,
                UpdatedAtUtcTicks = now.Ticks
            });
            return id;
        }

        /// <summary>Assigns an existing generated quest (an accepted offer, or a team member joining it).</summary>
        public bool TryAssignGenerated(Player player, string questId, out string result)
        {
            ArgumentNullException.ThrowIfNull(player);

            GeneratedQuestRow? row = _store.LoadGenerated(questId);
            QuestTemplate? template = row == null ? null : Deserialize(row.DefinitionJson);
            if (row == null || template == null)
            {
                result = "Unknown generated quest: " + questId;
                return false;
            }

            DateTime expires = new DateTime(row.ExpiresAtUtcTicks, DateTimeKind.Utc);
            if (_now() >= expires)
            {
                result = "Generated quest " + questId + " has expired.";
                return false;
            }

            QuestLog log = GetLog(player);
            if (log.Quests.TryGetValue(questId, out PlayerQuest? held) && held.IsActive)
            {
                result = string.Format(CultureInfo.InvariantCulture, "{0} already has quest {1} ({2}).", player.Name, questId, template.Name);
                return false;
            }

            Assign(player, log, questId, QuestSource.Generated, template, expires, row.AcgBuildingGeneratorJson);
            result = string.Format(CultureInfo.InvariantCulture, "Gave quest {0} ({1}) to {2}.", questId, template.Name, player.Name);
            return true;
        }

        /// <summary>Creates a generated quest owned by <paramref name="player"/> and assigns it.</summary>
        public PlayerQuest AssignGenerated(Player player, QuestTemplate template, DateTime expiresAtUtc, string? acgBuildingGeneratorJson = null)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(template);

            string id = CreateGenerated(template, expiresAtUtc, QuestOwnerType.Character, player.Identity.Instance, acgBuildingGeneratorJson);
            return Assign(player, GetLog(player), id, QuestSource.Generated, template, expiresAtUtc, acgBuildingGeneratorJson);
        }

        /// <summary>Generated quest that owns an ACG building: the generator data is stored with the quest.</summary>
        public PlayerQuest AssignGenerated(Player player, QuestTemplate template, DateTime expiresAtUtc, AcgBuildingGeneratorData acgBuildingGenerator)
        {
            ArgumentNullException.ThrowIfNull(acgBuildingGenerator);
            return AssignGenerated(player, template, expiresAtUtc, JsonSerializer.Serialize(acgBuildingGenerator));
        }

        /// <summary>
        /// Kill credit for <paramref name="npc"/>: each player's active kill quests on that NPC advance by one.
        /// The NPC matches on the hash it was spawned from (the dump's META hash) or its resolved template.
        /// </summary>
        public void OnNpcKilled(NpcCharacter npc, IReadOnlyList<Player> players)
        {
            ArgumentNullException.ThrowIfNull(npc);
            ArgumentNullException.ThrowIfNull(players);

            for (int p = 0; p < players.Count; p++)
            {
                Player player = players[p];
                List<PlayerQuest>? advanced = null;
                foreach (PlayerQuest quest in GetLog(player).Quests.Values)
                {
                    if (quest.IsActive && quest.Template.IsKill && Matches(npc, quest.Template.Objective.Npc))
                        (advanced ??= new List<PlayerQuest>()).Add(quest);
                }

                for (int i = 0; advanced != null && i < advanced.Count; i++)
                    Advance(player, advanced[i], npc);
            }
        }

        static bool Matches(NpcCharacter npc, string? objectiveNpc)
        {
            if (string.IsNullOrEmpty(objectiveNpc))
                return false;

            return string.Equals(npc.SpawnHash, objectiveNpc, StringComparison.Ordinal)
                || string.Equals(npc.MobTemplate?.Hash, objectiveNpc, StringComparison.Ordinal);
        }

        void Advance(Player player, PlayerQuest quest, NpcCharacter npc)
        {
            quest.Progress = Math.Min(quest.RequiredCount, quest.Progress + 1);
            if (quest.Remaining > 0)
            {
                Save(player, quest);
                // Retail: category 110 id 204307477 with the remaining count and the NPC name as arguments.
                ClientFeedback.SendFormatted(player, QuestCatalog.KillsRemainingTextId, quest.Remaining, npc.Name ?? string.Empty);
                return;
            }

            Complete(player, quest);
        }

        void Complete(Player player, PlayerQuest quest)
        {
            quest.State = QuestState.Completed;
            Save(player, quest);
            QuestJournal.Delete(player, quest);
            ClientFeedback.Send(player, "Feedback_MissionAccomplished");
            GrantReward(player, quest.Template);

            _logger.Info(string.Format(CultureInfo.InvariantCulture, "Quest completed char={0} quest={1} ({2})",
                player.Identity.Instance, quest.QuestId, quest.Template.Name));

            string? next = quest.Template.NextStep;
            if (!string.IsNullOrEmpty(next) && _catalog.TryGet(next, out _))
                TryAssignTemplate(player, next, out _);
        }

        /// <summary>
        /// Credits and XP (already zeroed for NoCash/NoXp in the data), then one item record: the records are
        /// alternatives, so one is picked at random and all its items are given together.
        /// </summary>
        void GrantReward(Player player, QuestTemplate template)
        {
            QuestReward? reward = template.Reward;
            if (reward == null)
                return;

            if (reward.Credits > 0)
            {
                long cash = (long)player.Stats.GetOrZero(CharacterStat.Cash) + reward.Credits;
                player.Stats.Set(CharacterStat.Cash, (int)Math.Min(int.MaxValue, cash), StatDetail.Base, dirty: true);
                player.FlushDirtyStats();
            }

            if (reward.Xp > 0)
            {
                // The completing kill's XP is still pending; send it first so the client reports the two gains separately.
                player.FlushDirtyStats();
                player.AwardXp(reward.Xp, XpSource.Quest);
                player.FlushDirtyStats();
            }

            if (reward.Records is not { Count: > 0 })
                return;

            List<QuestRewardItem> record = reward.Records[Random.Shared.Next(reward.Records.Count)];
            for (int i = 0; i < record.Count; i++)
                GiveItem(player, record[i]);
        }

        void GiveItem(Player player, QuestRewardItem reward)
        {
            if (!player.Inventory.IsHydrated || reward.Id <= 0)
                return;

            int slot = player.Inventory.Inventory.FindFreeSlot();
            if (slot < 0)
            {
                _logger.Warn(string.Format(CultureInfo.InvariantCulture, "Quest reward {0} not given to char={1}: inventory full",
                    reward.Id, player.Identity.Instance));
                return;
            }

            Item item = _items.CreateWithNewInstance(reward.Id, reward.HighId ?? reward.Id, Math.Max(1, reward.Quality), ItemSource.Quest);
            if (!player.Inventory.Inventory.Add(slot, item))
                return;

            player.Inventory.MarkDirty(item, player.Inventory.Inventory, slot);
            _flush.NotifyDirty(player);
            player.Session?.Send(new AddTemplateMessage
            {
                Identity = player.Identity,
                HighId = item.HighId,
                LowId = item.LowId,
                Quality = item.Quality,
                Count = item.StackCount
            });
        }

        PlayerQuest Assign(Player player, QuestLog log, string questId, QuestSource source, QuestTemplate template,
            DateTime? expiresAtUtc, string? acgJson)
        {
            var quest = new PlayerQuest
            {
                QuestId = questId,
                Source = source,
                Template = template,
                State = QuestState.Active,
                Progress = 0,
                RequiredCount = template.RequiredCount,
                AssignedAtUtc = _now(),
                ExpiresAtUtc = source == QuestSource.Generated ? expiresAtUtc : null,
                AcgBuildingGeneratorJson = acgJson
            };

            log.Quests[questId] = quest;
            Save(player, quest);
            // The announce flag makes the client print "You got a new mission" itself.
            QuestJournal.Send(player, quest, announce: true);
            return quest;
        }

        void ExpireDue(Player player, QuestLog log)
        {
            DateTime now = _now();
            foreach (PlayerQuest quest in log.Quests.Values)
            {
                if (!quest.IsActive || quest.ExpiresAtUtc is not DateTime expires || now < expires)
                    continue;

                quest.State = QuestState.Expired;
                Save(player, quest);
                QuestJournal.Delete(player, quest);
                ClientFeedback.Send(player, "Feedback_QuestExpired");
            }
        }

        /// <summary>
        /// The player's rows, with template quests read from Quests.json and generated quests (definition,
        /// expiry, ACG data) from generatedquests.
        /// </summary>
        QuestLog Load(Player player)
        {
            int characterId = player.Identity.Instance;
            var generated = new Dictionary<string, GeneratedQuestRow>(StringComparer.Ordinal);
            foreach (GeneratedQuestRow row in _store.LoadGeneratedFor(characterId))
                generated[row.QuestId] = row;

            var log = new QuestLog();
            foreach (CharacterQuestRow row in _store.Load(characterId))
            {
                var source = (QuestSource)row.Source;
                QuestTemplate? template = null;
                GeneratedQuestRow? definition = null;
                if (source == QuestSource.Template)
                    template = _catalog.TryGet(row.QuestId, out QuestTemplate found) ? found : null;
                else if (generated.TryGetValue(row.QuestId, out definition))
                    template = Deserialize(definition.DefinitionJson);

                if (template == null)
                {
                    _logger.Warn(string.Format(CultureInfo.InvariantCulture, "Quest {0} for char={1} has no definition; skipped",
                        row.QuestId, characterId));
                    continue;
                }

                log.Quests[row.QuestId] = new PlayerQuest
                {
                    QuestId = row.QuestId,
                    Source = source,
                    Template = template,
                    State = (QuestState)row.State,
                    Progress = row.Progress,
                    RequiredCount = row.RequiredCount,
                    AssignedAtUtc = new DateTime(row.AssignedAtUtcTicks, DateTimeKind.Utc),
                    ExpiresAtUtc = definition != null ? new DateTime(definition.ExpiresAtUtcTicks, DateTimeKind.Utc) : null,
                    AcgBuildingGeneratorJson = definition?.AcgBuildingGeneratorJson
                };
            }

            return log;
        }

        static QuestTemplate? Deserialize(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<QuestTemplate>(json, QuestTemplate.JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>The player's state only; a generated quest's definition lives in generatedquests.</summary>
        void Save(Player player, PlayerQuest quest)
        {
            _store.Save(new CharacterQuestRow
            {
                CharacterId = player.Identity.Instance,
                QuestId = quest.QuestId,
                Source = (int)quest.Source,
                State = (int)quest.State,
                Progress = quest.Progress,
                RequiredCount = quest.RequiredCount,
                AssignedAtUtcTicks = quest.AssignedAtUtc.Ticks,
                UpdatedAtUtcTicks = _now().Ticks
            });
        }
    }
}
