namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;
    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// Use-on-loot for mission ChestFullUpdate containers (Barrel / Treasure / Small Crate / etc).
    /// Live visuals are IdentityType.Container packets — never SimpleChar.
    /// </summary>
    internal static class MissionLootPropService
    {
        private static readonly object Sync = new object();

        private static readonly HashSet<long> Props = new HashSet<long>();

        private static readonly HashSet<long> Looted = new HashSet<long>();

        private static readonly Dictionary<long, string> Names = new Dictionary<long, string>();

        private static long Key(Identity identity)
        {
            return ((long)(int)identity.Type << 32) | (uint)identity.Instance;
        }

        public static void Register(Identity identity)
        {
            Register(identity, null);
        }

        public static void Register(Identity identity, string displayName)
        {
            if ((int)identity.Type == 0 || identity.Instance == 0)
            {
                return;
            }

            long key = Key(identity);
            lock (Sync)
            {
                Props.Add(key);
                if (!string.IsNullOrEmpty(displayName))
                {
                    Names[key] = displayName;
                }
            }
        }

        public static bool IsLootProp(Identity identity)
        {
            lock (Sync)
            {
                return Props.Contains(Key(identity));
            }
        }

        public static bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (client == null || target == null || !IsLootProp(target))
            {
                return false;
            }

            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null || character.Playfield == null
                || !MissionInstanceService.IsMissionInstancePlayfield(character.Playfield.Identity.Instance))
            {
                return false;
            }

            long key = Key(target);
            lock (Sync)
            {
                if (Looted.Contains(key))
                {
                    GenericCmdMessageHandler.Default.Acknowledge(character, message);
                    return true;
                }

                Looted.Add(key);
            }

            string propName;
            lock (Sync)
            {
                if (!Names.TryGetValue(key, out propName) || string.IsNullOrEmpty(propName))
                {
                    propName = "container";
                }
            }

            int missionQl = 1;
            int stamped;
            if (MissionInstanceService.TryGetStampedMissionQuality(character.Playfield.Identity.Instance, out stamped)
                && stamped > 0)
            {
                missionQl = stamped;
            }

            var rng = new Random(unchecked(Environment.TickCount * 911) ^ target.Instance);
            int credits = 5 + rng.Next(10, 40) + (missionQl / 2);
            long before = character.Stats[StatIds.cash].Value;
            long after = before + credits;
            if (after > int.MaxValue)
            {
                after = int.MaxValue;
            }

            character.Stats[StatIds.cash].Set((uint)after);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)after);
            GenericCmdMessageHandler.Default.Acknowledge(character, message);
            character.Send(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    Unknown2 = 0,
                    FormattedMessage = TokenBoardRuntime.ToYellowSystemFeedback(
                        string.Format("You found {0} credits in the {1}.", credits, propName))
                });

            MissionRareLootCatalog.RareDrop rare;
            if (MissionRareLootCatalog.TryRoll(missionQl, rng, out rare) && rare != null)
            {
                int itemInstance;
                InventoryError error;
                if (MissionKeyGrantService.TryGrantNamedItem(
                    client,
                    character,
                    rare.LowId,
                    rare.HighId,
                    rare.Quality,
                    rare.Name,
                    out itemInstance,
                    out error))
                {
                    character.Send(
                        new FormatFeedbackMessage
                        {
                            Identity = character.Identity,
                            Unknown = 1,
                            Unknown1 = 0,
                            Unknown2 = 0,
                            FormattedMessage = TokenBoardRuntime.ToYellowSystemFeedback(
                                "You've found something rare: " + rare.Name + "!")
                        });
                }
            }

            Playfield playfield = character.Playfield as Playfield;
            if (playfield != null)
            {
                playfield.Despawn(target);
            }

            lock (Sync)
            {
                Props.Remove(key);
                Names.Remove(key);
            }

            MissionDiagnostics.Log(
                "LOOT-PROP char={0} target={1:X8} type={2} name={3} credits={4}",
                character.Identity.Instance,
                target.Instance,
                (int)target.Type,
                propName,
                credits);
            return true;
        }
    }
}
