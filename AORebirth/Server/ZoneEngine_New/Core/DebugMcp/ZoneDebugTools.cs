namespace ZoneEngine_New.Core.DebugMcp
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;

    using AORebirth.Interfaces.Persistence.Missions;

    using ModelContextProtocol.Server;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Metrics;
    using ZoneEngine_New.Core.Missions;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;

    [McpServerToolType]
    public sealed class ZoneDebugTools
    {
        static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        readonly PlayfieldManager _playfields;
        readonly IPlayfieldMetricsRegistry _metrics;
        readonly GeneratedMissionService _missions;
        readonly DebugMcpEndpoint _endpoint;

        public ZoneDebugTools(
            PlayfieldManager playfields,
            IPlayfieldMetricsRegistry metrics,
            GeneratedMissionService missions,
            DebugMcpEndpoint endpoint)
        {
            ArgumentNullException.ThrowIfNull(playfields);
            ArgumentNullException.ThrowIfNull(metrics);
            ArgumentNullException.ThrowIfNull(missions);
            ArgumentNullException.ThrowIfNull(endpoint);
            _playfields = playfields;
            _metrics = metrics;
            _missions = missions;
            _endpoint = endpoint;
        }

        [McpServerTool(Name = "server_status")]
        [System.ComponentModel.Description("Live ZoneEngine status: player count, loaded playfield count, and the debug listener. Read-only. No credentials.")]
        public string ServerStatus()
        {
            return Json(new
            {
                ready = true,
                players = _playfields.SnapshotPlayers().Count,
                playfields = _playfields.SnapshotPlayfields().Count,
                listen = _endpoint.ListenIP,
                port = _endpoint.Port,
                path = DebugMcpOptions.Route
            });
        }

        [McpServerTool(Name = "list_playfields")]
        [System.ComponentModel.Description("Loaded playfields. Each row is id, ordinary or mission, player count, and dynel count. Capped.")]
        public string ListPlayfields()
        {
            IReadOnlyList<Playfield> playfields = _playfields.SnapshotPlayfields();
            var rows = new List<object>(Math.Min(playfields.Count, ZoneDebugSnapshots.MaxPlayfields));
            for (int i = 0; i < playfields.Count && rows.Count < ZoneDebugSnapshots.MaxPlayfields; i++)
            {
                Playfield playfield = playfields[i];
                int players = 0;
                int dynels = 0;
                DynelRegistry registry = playfield.GetRequiredService<DynelRegistry>();
                foreach (Player _ in registry.PlayerEntities())
                    players++;
                foreach (Dynel _ in registry.Dynels())
                    dynels++;
                rows.Add(new
                {
                    id = playfield.Identity.Instance,
                    kind = playfield is MissionPlayfield ? "mission" : "ordinary",
                    players,
                    dynels
                });
            }

            return Json(new
            {
                count = rows.Count,
                truncated = playfields.Count > rows.Count,
                playfields = rows
            });
        }

        [McpServerTool(Name = "list_players")]
        [System.ComponentModel.Description("Online players: character id, name, playfield, position, connection phase, level, health, and nano. Capped.")]
        public string ListPlayers()
        {
            IReadOnlyList<Player> players = _playfields.SnapshotPlayers();
            var rows = new List<PlayerView>(Math.Min(players.Count, ZoneDebugSnapshots.MaxPlayers));
            for (int i = 0; i < players.Count && rows.Count < ZoneDebugSnapshots.MaxPlayers; i++)
                rows.Add(ZoneDebugSnapshots.ProjectPlayer(players[i]));
            return Json(new
            {
                count = rows.Count,
                truncated = players.Count > rows.Count,
                players = rows
            });
        }

        [McpServerTool(Name = "get_player")]
        [System.ComponentModel.Description("One online player by characterId or name. Includes position, heading, target, fight target, profession, breed, vitals, active nanos, and accepted mission bindings.")]
        public string GetPlayer(int characterId = 0, string name = "")
        {
            if (!TryFindPlayer(characterId, name, out Player? player, out string? error))
                return Error(error!);
            PlayerView view = ZoneDebugSnapshots.ProjectPlayer(player!);
            object missions = ReadMissions(player!, includeOffers: false);
            var nanos = new List<object>();
            foreach (Buff buff in player!.Buffs)
            {
                if (nanos.Count >= 16)
                    break;
                nanos.Add(new { id = buff.Id, name = buff.Name });
            }

            return Json(new { player = view, activeNanos = nanos, missions });
        }

        [McpServerTool(Name = "get_stats")]
        [System.ComponentModel.Description("Named CharacterStat values for one online player. stats is a comma-separated list, at most 32 names. Unknown names are reported.")]
        public string GetStats(string stats, int characterId = 0, string name = "")
        {
            if (!TryFindPlayer(characterId, name, out Player? player, out string? error))
                return Error(error!);
            if (string.IsNullOrWhiteSpace(stats))
                return Error("Pass stats as a comma-separated list of CharacterStat names.");

            string[] parts = stats.Split(',');
            if (parts.Length > ZoneDebugSnapshots.MaxStats)
                return Error("At most " + ZoneDebugSnapshots.MaxStats.ToString(CultureInfo.InvariantCulture) + " stats.");

            var values = new List<object>();
            var missing = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string statName = parts[i].Trim();
                if (statName.Length == 0)
                    continue;
                if (!Enum.TryParse(statName, ignoreCase: true, out CharacterStat stat))
                {
                    missing.Add(statName);
                    continue;
                }

                bool present = player!.Stats.TryGetValue(stat, out int full);
                values.Add(new
                {
                    stat = stat.ToString(),
                    value = present ? full : (int?)null,
                    present
                });
            }

            return Json(new
            {
                characterId = player!.Identity.Instance,
                name = ZoneDebugSnapshots.DisplayName(player),
                stats = values,
                missing
            });
        }

        [McpServerTool(Name = "list_dynels")]
        [System.ComponentModel.Description("Dynels on one playfield. Optional identity type filter and radius around nearCharacterId. Returns identity, type, name, and position. Capped at 50.")]
        public string ListDynels(int playfieldId, string type = "", int nearCharacterId = 0, double radius = 0)
        {
            if (!TryPlayfield(playfieldId, out Playfield? playfield, out string? error))
                return Error(error!);

            IdentityType? typeFilter = null;
            if (!string.IsNullOrWhiteSpace(type))
            {
                if (!Enum.TryParse(type.Trim(), ignoreCase: true, out IdentityType parsed))
                    return Error("Unknown identity type.");
                typeFilter = parsed;
            }

            Player? near = null;
            if (nearCharacterId > 0 || radius > 0)
            {
                if (nearCharacterId <= 0 || radius <= 0)
                    return Error("Pass both nearCharacterId and radius.");
                if (!_playfields.FindPlayer(nearCharacterId, out Player? found) || found.Playfield == null
                    || found.Playfield.Identity.Instance != playfieldId)
                    return Error("Player is not on that playfield.");
                near = found;
            }

            var rows = new List<DynelView>();
            int matched = 0;
            foreach (Dynel dynel in playfield!.GetRequiredService<DynelRegistry>().Dynels())
            {
                if (typeFilter.HasValue && dynel.Identity.Type != typeFilter.Value)
                    continue;
                if (near != null && dynel.Distance3D(near) > radius)
                    continue;
                matched++;
                if (rows.Count < ZoneDebugSnapshots.MaxDynels)
                {
                    rows.Add(ZoneDebugSnapshots.ProjectDynel(dynel));
                }
            }

            return Json(new
            {
                playfieldId,
                count = rows.Count,
                truncated = matched > rows.Count,
                dynels = rows
            });
        }

        [McpServerTool(Name = "get_dynel")]
        [System.ComponentModel.Description("One dynel on a playfield by identity type and instance.")]
        public string GetDynel(int playfieldId, string identityType, int instance)
        {
            if (!TryPlayfield(playfieldId, out Playfield? playfield, out string? error))
                return Error(error!);
            if (string.IsNullOrWhiteSpace(identityType)
                || !Enum.TryParse(identityType.Trim(), ignoreCase: true, out IdentityType type))
                return Error("Unknown identity type.");

            var identity = new Identity { Type = type, Instance = instance };
            if (!playfield!.GetRequiredService<DynelRegistry>().TryGet(identity, out Dynel? dynel) || dynel == null)
                return Error("Dynel not found.");
            return Json(new { playfieldId, dynel = ZoneDebugSnapshots.ProjectDynel(dynel) });
        }

        [McpServerTool(Name = "get_inventory")]
        [System.ComponentModel.Description("One online player's inventory pages: page, slot, low id, high id, quality, stack, and name. Capped at 100 slots.")]
        public string GetInventory(int characterId = 0, string name = "")
        {
            if (!TryFindPlayer(characterId, name, out Player? player, out string? error))
                return Error(error!);
            PlayerInventory inventory = player!.Inventory;
            if (!inventory.IsHydrated)
            {
                return Json(new
                {
                    characterId = player.Identity.Instance,
                    name = ZoneDebugSnapshots.DisplayName(player),
                    hydrated = false,
                    slots = Array.Empty<object>()
                });
            }

            var slots = new List<object>();
            bool truncated = false;
            AddPage(slots, ref truncated, "inventory", inventory.Inventory);
            AddPage(slots, ref truncated, "weapons", inventory.Equipment);
            AddPage(slots, ref truncated, "armor", inventory.Armor);
            AddPage(slots, ref truncated, "implants", inventory.Implant);
            AddPage(slots, ref truncated, "social", inventory.Social);
            return Json(new
            {
                characterId = player.Identity.Instance,
                name = ZoneDebugSnapshots.DisplayName(player),
                hydrated = true,
                truncated,
                count = slots.Count,
                slots
            });
        }

        [McpServerTool(Name = "list_missions")]
        [System.ComponentModel.Description("One online player's generated mission offers and accepted bindings. Read-only. Capped.")]
        public string ListMissions(int characterId = 0, string name = "")
        {
            if (!TryFindPlayer(characterId, name, out Player? player, out string? error))
                return Error(error!);
            return Json(ReadMissions(player!, includeOffers: true));
        }

        [McpServerTool(Name = "playfield_metrics")]
        [System.ComponentModel.Description("Tick and world-sim timing for one playfield. windowSeconds must be 1, 3, 5, 10, or 30.")]
        public string PlayfieldMetrics(int playfieldId, int windowSeconds = 1)
        {
            if (playfieldId <= 0)
                return Error("playfieldId is required.");
            if (!PlayfieldMetricsWindows.IsAllowed(windowSeconds))
                return Error("windowSeconds must be 1, 3, 5, 10, or 30.");
            if (!_metrics.TryGetSnapshot(playfieldId, windowSeconds, out PlayfieldMetricsSnapshot? snapshot) || snapshot == null)
                return Error("No metrics for playfield " + playfieldId.ToString(CultureInfo.InvariantCulture) + ".");
            return Json(new
            {
                snapshot.PlayfieldId,
                snapshot.WindowSeconds,
                snapshot.BuildElapsedMs,
                snapshot.TickAverageMs,
                snapshot.TickSampleCount,
                snapshot.WorldSimAverageMs,
                snapshot.WorldSimSampleCount
            });
        }

        [McpServerTool(Name = "recent_log")]
        [System.ComponentModel.Description("Recent ZoneEngine log lines from the in-memory ring. count is capped at 64.")]
        public string RecentLog(int count = 32)
        {
            if (count <= 0)
            {
                count = 32;
            }

            if (count > DebugMcpLogBuffer.MaxLines)
            {
                count = DebugMcpLogBuffer.MaxLines;
            }
            IReadOnlyList<string> lines = DebugMcpLogBuffer.Shared.Recent(count);
            return Json(new { count = lines.Count, lines });
        }

        bool TryFindPlayer(int characterId, string name, out Player? player, out string? error)
        {
            player = null;
            error = null;
            IReadOnlyList<Player> players = _playfields.SnapshotPlayers();
            if (characterId > 0)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].Identity.Instance == characterId)
                    {
                        player = players[i];
                        return true;
                    }
                }

                error = "Player not online.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Pass characterId or name.";
                return false;
            }

            string wanted = name.Trim();
            for (int i = 0; i < players.Count; i++)
            {
                Player candidate = players[i];
                if (string.Equals(ZoneDebugSnapshots.DisplayName(candidate), wanted, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.FirstName, wanted, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.Name, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    player = candidate;
                    return true;
                }
            }

            error = "Player not online.";
            return false;
        }

        bool TryPlayfield(int playfieldId, out Playfield? playfield, out string? error)
        {
            playfield = null;
            error = null;
            if (playfieldId <= 0)
            {
                error = "playfieldId is required.";
                return false;
            }

            if (!_playfields.TryGet(playfieldId, out playfield) || playfield == null)
            {
                error = "Playfield is not loaded.";
                return false;
            }

            return true;
        }

        object ReadMissions(Player player, bool includeOffers)
        {
            try
            {
                IReadOnlyList<GeneratedMissionBinding> accepted = _missions.ReadAccepted(player);
                var bindings = new List<object>();
                bool truncated = accepted.Count > ZoneDebugSnapshots.MaxMissions;
                int bindingCount = Math.Min(accepted.Count, ZoneDebugSnapshots.MaxMissions);
                for (int i = 0; i < bindingCount; i++)
                    bindings.Add(ProjectBinding(accepted[i]));

                if (!includeOffers)
                {
                    return new
                    {
                        characterId = player.Identity.Instance,
                        accepted = bindings,
                        truncated
                    };
                }

                IReadOnlyList<GeneratedMissionOffer> offers = _missions.ReadOffers(player);
                var offerRows = new List<object>();
                if (offers.Count > ZoneDebugSnapshots.MaxMissions)
                {
                    truncated = true;
                }
                int offerCount = Math.Min(offers.Count, ZoneDebugSnapshots.MaxMissions);
                for (int i = 0; i < offerCount; i++)
                    offerRows.Add(ProjectOffer(offers[i]));
                return new
                {
                    characterId = player.Identity.Instance,
                    name = ZoneDebugSnapshots.DisplayName(player),
                    offers = offerRows,
                    accepted = bindings,
                    truncated
                };
            }
            catch (Exception exception)
            {
                return new
                {
                    characterId = player.Identity.Instance,
                    error = "Mission read failed: " + exception.GetType().Name
                };
            }
        }

        static object ProjectOffer(GeneratedMissionOffer offer)
        {
            return new
            {
                offer.Title,
                description = Trim(offer.Description, 180),
                state = offer.State.ToString(),
                offer.Quality,
                offer.MissionType,
                offer.OfferType,
                offer.OfferInstance,
                offer.DestinationPlayfield,
                expiresUtc = Utc(offer.ExpiresAtUtcTicks)
            };
        }

        static object ProjectBinding(GeneratedMissionBinding binding)
        {
            return new
            {
                title = binding.Offer == null ? null : binding.Offer.Title,
                state = binding.State.ToString(),
                binding.Progress,
                binding.RequiredCount,
                binding.LivePlayfield,
                binding.QuestType,
                binding.QuestInstance,
                expiresUtc = Utc(binding.ExpiresAtUtcTicks)
            };
        }

        static void AddPage(List<object> slots, ref bool truncated, string page, Container container)
        {
            foreach (KeyValuePair<int, Item> pair in container.Content.ToArray())
            {
                if (slots.Count >= ZoneDebugSnapshots.MaxInventorySlots)
                {
                    truncated = true;
                    return;
                }

                Item item = pair.Value;
                slots.Add(new
                {
                    page,
                    slot = pair.Key,
                    lowId = item.LowId,
                    highId = item.HighId,
                    quality = item.Quality,
                    stack = item.StackCount,
                    name = item.Name
                });
            }
        }

        static string? Utc(long ticks)
        {
            if (ticks <= 0)
                return null;
            try
            {
                return new DateTime(ticks, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        static string? Trim(string? value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
                return value;
            return value.Substring(0, max);
        }

        static string Json(object value)
            => JsonSerializer.Serialize(value, JsonOptions);

        static string Error(string message)
            => Json(new { error = message });
    }
}
