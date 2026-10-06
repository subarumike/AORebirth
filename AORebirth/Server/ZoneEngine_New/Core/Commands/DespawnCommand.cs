namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// <c>.despawn</c>: removes the selected non-player dynel as if it had died, so whatever spawned it respawns it on its
    /// normal timer. NPCs raise <see cref="Character.Died"/> without a corpse, loot or XP; spawn-point statics go through
    /// the hash spawn system; corpses are simply removed. Statics no spawn point owns (world fixtures) are refused.
    /// </summary>
    public sealed class DespawnCommand : IGmCommand
    {
        public string Name => "despawn";

        public int RequiredGmLevel => 1;

        public string Usage => ".despawn (despawns the selected non-player target as if it died)";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            Player player = context.Player;
            if (player.Playfield is not Playfield playfield)
            {
                GmCommandFeedback.Send(context.Session, player, "Not on a playfield.");
                return;
            }

            Identity target = player.Target;
            if (target.Instance == 0 || target == player.Identity)
            {
                GmCommandFeedback.Send(context.Session, player, "Select a non-player target first.");
                return;
            }

            DynelRegistry registry = playfield.GetRequiredService<DynelRegistry>();
            if (!registry.TryGet(target, out Dynel? dynel)
                && !registry.TryGet(new Identity { Type = IdentityType.CanbeAffected, Instance = target.Instance }, out dynel))
            {
                GmCommandFeedback.Send(context.Session, player, "Unknown target.");
                return;
            }

            string described = Describe(dynel!);
            switch (dynel)
            {
                case Player:
                    GmCommandFeedback.Send(context.Session, player, "Players cannot be despawned.");
                    return;

                case NpcCharacter npc:
                    if (npc.IsDead)
                    {
                        GmCommandFeedback.Send(context.Session, player, described + " is already dead.");
                        return;
                    }

                    npc.DespawnAsDeath();
                    break;

                case Corpse corpse:
                    playfield.GetRequiredService<SpawnService>().DespawnCorpse(corpse);
                    break;

                case StaticDynel staticDynel:
                    if (!playfield.GetRequiredService<HashSpawnSystem>().TryDespawnStaticAsDeath(staticDynel))
                    {
                        GmCommandFeedback.Send(context.Session, player,
                            described + " is not owned by a spawn point and would not come back; not despawned.");
                        return;
                    }

                    break;

                default:
                    GmCommandFeedback.Send(context.Session, player, described + " cannot be despawned.");
                    return;
            }

            player.SetTarget(Identity.None);
            GmCommandFeedback.Send(context.Session, player, "Despawned " + described + ".");
        }

        static string Describe(Dynel dynel)
        {
            string id = dynel.Identity.Instance.ToString(CultureInfo.InvariantCulture);
            return dynel switch
            {
                NpcCharacter npc => (string.IsNullOrEmpty(npc.Name) ? "NPC" : npc.Name) + " (" + id + ")",
                Corpse corpse => corpse.Name + " (" + id + ")",
                StaticDynel s => (string.IsNullOrEmpty(s.Template.Name) ? "static" : s.Template.Name) + " (" + id + ")",
                _ => "dynel " + id
            };
        }
    }
}
