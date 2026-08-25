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
        // Toggle OLD statel/DB materialization diagnostics independently from NEW hydration diagnostics.
        private const bool EnableOldStaticConsoleDiagnostics = false;

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
            ConsoleLog($"[OLD static materialization] PF {playfieldIdentity.Instance} START");
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
            ConsoleLog($"[OLD static materialization] PF {playfieldIdentity.Instance} COMPLETE");
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
            int loaded = 0;
            foreach (DBMobSpawn mob in loadMobSpawns(playfieldIdentity))
            {
                if (shouldSuppressDbMobSpawn(mob))
                {
                    continue;
                }

                ICharacter character = instantiateDbMobSpawn(mob, loadMobSpawnStats(mob).ToArray());
                activateNpc(character);
                attachMobSpawnScript(mob, character);
                loaded++;
                ConsoleLog($"[OLD static materialization][MOB] PF {playfieldIdentity.Instance} sourceMob={mob.Id} activated");
            }
            ConsoleLog($"[OLD static materialization][MOB] PF {playfieldIdentity.Instance} count={loaded}");
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
                ConsoleLog($"[OLD static materialization][SHOP] PF {playfieldIdentity.Instance} statels={vendorStatels.Length}");
                spawnVendors(vendorStatels);
            }
        }

        private void MaterializeStaticDynels(
            Identity playfieldIdentity,
            Func<Identity, IEnumerable<PlayfieldStaticDynelDefinition>> resolveStaticDynels,
            Func<PlayfieldStaticDynelDefinition, IEntity> instantiateStaticDynel,
            Action<IEntity> registerDynel)
        {
            int loaded = 0;
            foreach (PlayfieldStaticDynelDefinition staticDynel in resolveStaticDynels(playfieldIdentity))
            {
                registerDynel(instantiateStaticDynel(staticDynel));
                loaded++;
            }

            Utility.LogUtil.Debug(
                Utility.DebugInfoDetail.Database,
                "MaterializeStaticDynels pf=" + playfieldIdentity.Instance + " loaded=" + loaded);
            ConsoleLog($"[OLD static materialization][STATIC] PF {playfieldIdentity.Instance} count={loaded}");
        }

        private static void ConsoleLog(string message)
        {
            if (EnableOldStaticConsoleDiagnostics)
            {
                Console.WriteLine(message);
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
