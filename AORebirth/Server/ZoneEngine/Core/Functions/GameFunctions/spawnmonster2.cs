namespace ZoneEngine.Core.Functions.GameFunctions
{
    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using ZoneEngine.Core;

    /// <summary>
    /// FunctionType.SpawnMonster2 (53063) — nano Summon Buckethead Technodealer (300439)
    /// args=["BKTH", 220, 600]. Capture 20260723-061619.
    /// </summary>
    internal class spawnmonster2 : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.SpawnMonster2;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            ICharacter owner = self as ICharacter;
            if (owner == null || arguments == null || arguments.Length < 1)
            {
                return false;
            }

            string hash = arguments[0].AsString();
            int level = arguments.Length > 1 ? arguments[1].AsInt32() : -1;
            int lifetimeSeconds = arguments.Length > 2 ? arguments[2].AsInt32() : 600;

            if (SummonedBucketheadTechnodealerRuntime.TrySpawn(owner, hash, level, lifetimeSeconds))
            {
                return true;
            }

            return false;
        }
    }
}
