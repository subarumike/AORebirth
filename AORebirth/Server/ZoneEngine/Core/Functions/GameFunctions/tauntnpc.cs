namespace ZoneEngine.Core.Functions.GameFunctions
{
    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using Utility;

    /// <summary>
    /// FunctionType.TauntNpc (53117).
    /// Mongo Slam nested nano 100194 uses large TauntNpc values (2000/3000/4000)
    /// as hate/taunt magnitude, not HP damage. Apply 1-point engage damage + force aggro
    /// steal so the caster pulls mobs off other players.
    /// </summary>
    internal class tauntnpc : FunctionPrototype
    {
        private const int EngageDamage = 1;

        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.TauntNpc;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            Character caster = self as Character;
            Character npc = target as Character;
            if (caster == null || npc == null || object.ReferenceEquals(caster, npc))
            {
                return false;
            }

            int tauntAmount = 0;
            if (arguments != null && arguments.Length >= 1)
            {
                tauntAmount = arguments[0].AsInt32();
            }

            // 1 HP engage tick so combat systems register the hit without nuking the mob.
            var hitArgs = new MessagePackObject[] { 27, -EngageDamage, -EngageDamage, 0 };
            new hit().Execute(caster, caster, npc, hitArgs);

            Playfield playfield = npc.Playfield as Playfield;
            if (playfield != null)
            {
                playfield.ForceNpcTauntAggro(caster, npc);
            }

            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                string.Format(
                    "TauntNpc caster={0} target={1} tauntAmount={2} engageDmg={3}",
                    caster.Identity,
                    npc.Identity,
                    tauntAmount,
                    EngageDamage));
            return true;
        }
    }
}
