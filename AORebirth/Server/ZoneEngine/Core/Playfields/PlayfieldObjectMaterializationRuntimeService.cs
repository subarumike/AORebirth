namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Statels;
    using AORebirth.Database.Entities;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    internal sealed class PlayfieldObjectMaterializationRuntimeService
    {
        internal delegate bool TryResolveVendorStatelsDelegate(
            Identity playfieldIdentity,
            IEnumerable<StatelData> statels,
            out StatelData[] vendorStatels);

        internal void MaterializeStartupObjects(
            Identity playfieldIdentity,
            IEnumerable<StatelData> statels,
            Func<Identity, IEnumerable<DBMobSpawn>> loadMobSpawns,
            Func<DBMobSpawn, bool> shouldSuppressDbMobSpawn,
            Func<DBMobSpawn, IEnumerable<DBMobSpawnStat>> loadMobSpawnStats,
            Func<DBMobSpawn, DBMobSpawnStat[], ICharacter> instantiateDbMobSpawn,
            Action<ICharacter> activateNpc,
            Action<DBMobSpawn, ICharacter> attachMobSpawnScript,
            Action<Identity> registerContent,
            TryResolveVendorStatelsDelegate tryResolveVendorStatels,
            Action<StatelData[]> spawnVendors,
            Func<Identity, IEnumerable<PlayfieldStaticDynelDefinition>> resolveStaticDynels,
            Func<PlayfieldStaticDynelDefinition, IEntity> instantiateStaticDynel,
            Action<IEntity> registerDynel,
            Action refreshDynelRegistry)
        {
            Require(loadMobSpawns, "loadMobSpawns");
            Require(shouldSuppressDbMobSpawn, "shouldSuppressDbMobSpawn");
            Require(loadMobSpawnStats, "loadMobSpawnStats");
            Require(instantiateDbMobSpawn, "instantiateDbMobSpawn");
            Require(activateNpc, "activateNpc");
            Require(attachMobSpawnScript, "attachMobSpawnScript");
            Require(registerContent, "registerContent");
            Require(tryResolveVendorStatels, "tryResolveVendorStatels");
            Require(spawnVendors, "spawnVendors");
            Require(resolveStaticDynels, "resolveStaticDynels");
            Require(instantiateStaticDynel, "instantiateStaticDynel");
            Require(registerDynel, "registerDynel");
            Require(refreshDynelRegistry, "refreshDynelRegistry");

            this.MaterializeDbMobSpawns(
                playfieldIdentity,
                loadMobSpawns,
                shouldSuppressDbMobSpawn,
                loadMobSpawnStats,
                instantiateDbMobSpawn,
                activateNpc,
                attachMobSpawnScript);
            registerContent(playfieldIdentity);
            this.MaterializeVendors(playfieldIdentity, statels, tryResolveVendorStatels, spawnVendors);
            this.MaterializeStaticDynels(playfieldIdentity, resolveStaticDynels, instantiateStaticDynel, registerDynel);
            refreshDynelRegistry();
        }

        private void MaterializeDbMobSpawns(
            Identity playfieldIdentity,
            Func<Identity, IEnumerable<DBMobSpawn>> loadMobSpawns,
            Func<DBMobSpawn, bool> shouldSuppressDbMobSpawn,
            Func<DBMobSpawn, IEnumerable<DBMobSpawnStat>> loadMobSpawnStats,
            Func<DBMobSpawn, DBMobSpawnStat[], ICharacter> instantiateDbMobSpawn,
            Action<ICharacter> activateNpc,
            Action<DBMobSpawn, ICharacter> attachMobSpawnScript)
        {
            foreach (DBMobSpawn mob in loadMobSpawns(playfieldIdentity))
            {
                if (shouldSuppressDbMobSpawn(mob))
                {
                    continue;
                }

                ICharacter character = instantiateDbMobSpawn(mob, loadMobSpawnStats(mob).ToArray());
                activateNpc(character);
                attachMobSpawnScript(mob, character);
            }
        }

        private void MaterializeVendors(
            Identity playfieldIdentity,
            IEnumerable<StatelData> statels,
            TryResolveVendorStatelsDelegate tryResolveVendorStatels,
            Action<StatelData[]> spawnVendors)
        {
            StatelData[] vendorStatels;
            if (tryResolveVendorStatels(playfieldIdentity, statels, out vendorStatels))
            {
                spawnVendors(vendorStatels);
            }
        }

        private void MaterializeStaticDynels(
            Identity playfieldIdentity,
            Func<Identity, IEnumerable<PlayfieldStaticDynelDefinition>> resolveStaticDynels,
            Func<PlayfieldStaticDynelDefinition, IEntity> instantiateStaticDynel,
            Action<IEntity> registerDynel)
        {
            foreach (PlayfieldStaticDynelDefinition staticDynel in resolveStaticDynels(playfieldIdentity))
            {
                registerDynel(instantiateStaticDynel(staticDynel));
            }
        }

        private static void Require(Delegate callback, string name)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}
