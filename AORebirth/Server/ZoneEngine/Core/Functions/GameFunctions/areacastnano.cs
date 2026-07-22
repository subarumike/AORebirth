namespace ZoneEngine.Core.Functions.GameFunctions
{
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Nanos;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;

    /// <summary>
    /// FunctionType.AreaCastNano (53087).
    /// Capture 20260719-Rex-Markus-stone / nanos.dat Mongo Slam family:
    /// AreaCastNano args [nestedNanoId, radiusMeters] (e.g. 100194, 20).
    /// Hits NPCs in radius; players only when PvP-flagged.
    /// </summary>
    internal class areacastnano : FunctionPrototype
    {
        private const float DefaultRadiusMeters = 20f;

        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.AreaCastNano;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            Character character = self as Character;
            if (character == null || arguments == null || arguments.Length < 1)
            {
                return false;
            }

            int nestedNanoId = arguments[0].AsInt32();
            float radius = DefaultRadiusMeters;
            if (arguments.Length >= 2)
            {
                int radiusArg = arguments[1].AsInt32();
                if (radiusArg > 0)
                {
                    radius = radiusArg;
                }
            }
            else
            {
                NanoFormula nested;
                if (NanoLoader.NanoList.TryGetValue(nestedNanoId, out nested))
                {
                    int attrRadius = nested.getItemAttribute(287);
                    if (attrRadius > 0 && attrRadius != 1234567890)
                    {
                        radius = attrRadius;
                    }
                }
            }

            Playfield playfield = character.Playfield as Playfield;
            if (playfield == null)
            {
                return castnano.ApplyInstantNano(character, character, nestedNanoId);
            }

            IList<ICharacter> inRange = playfield.FindCharacterInRange(character, radius);
            int hits = 0;
            foreach (ICharacter nearby in inRange)
            {
                Character other = nearby as Character;
                if (other == null || object.ReferenceEquals(other, character))
                {
                    continue;
                }

                if (!IsValidAreaCastTarget(character, other))
                {
                    continue;
                }

                if (castnano.ApplyInstantNano(character, other, nestedNanoId))
                {
                    hits++;
                }
            }

            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                string.Format(
                    "AreaCastNano caster={0} nested={1} radius={2} hits={3}",
                    character.Identity,
                    nestedNanoId,
                    radius,
                    hits));
            return true;
        }

        private static bool IsValidAreaCastTarget(Character caster, Character other)
        {
            bool isNpc = other.Stats[StatIds.npcfamily].BaseValue != 0
                           || other.Stats[StatIds.monsterdata].BaseValue != 0
                           || other.Controller is Controllers.NPCController;
            if (isNpc)
            {
                // Social / quest NPCs (Rex, Marcus, vendors) must not receive Mongo-style
                // AreaCastNano → TauntNpc. Combat Passive/Aggressive only.
                NPCController npcController = other.Controller as NPCController;
                if (npcController != null
                    && !NpcAiProfiles.CanRetaliate(npcController.AiProfile))
                {
                    return false;
                }

                return true;
            }

            // Players / player pets: only when PvP-flagged (or gas zones already allow PvP).
            if (!PlayerVersusPlayerCombatRules.IsProtectedPlayerVersusPlayerTarget(other))
            {
                return false;
            }

            return PlayerVersusPlayerCombatRules.CanEngagePlayerVersusPlayerCombat(caster, other);
        }
    }
}
