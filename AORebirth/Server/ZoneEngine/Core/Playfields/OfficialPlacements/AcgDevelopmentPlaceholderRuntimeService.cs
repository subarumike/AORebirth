namespace ZoneEngine.Core.Playfields.OfficialPlacements
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Playfields.OfficialPlacements;
    using AORebirth.Core.Vector;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using CoreQuaternion = AORebirth.Core.Vector.Quaternion;

    using Utility;

    using ZoneEngine.Core.Controllers;

    internal static class AcgDevelopmentPlaceholderRuntimeRegistry
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<int, AcgDevelopmentPlaceholderPlanEntry> ByRuntimeIdentity =
            new Dictionary<int, AcgDevelopmentPlaceholderPlanEntry>();

        private static readonly Dictionary<int, HashSet<int>> RuntimeIdentitiesByPlayfield =
            new Dictionary<int, HashSet<int>>();

        internal static bool IsPlaceholder(int runtimeIdentity)
        {
            lock (Sync)
            {
                return ByRuntimeIdentity.ContainsKey(runtimeIdentity);
            }
        }

        internal static bool TryGet(
            int runtimeIdentity,
            out AcgDevelopmentPlaceholderPlanEntry metadata)
        {
            lock (Sync)
            {
                return ByRuntimeIdentity.TryGetValue(runtimeIdentity, out metadata);
            }
        }

        internal static void Register(
            int playfieldInstance,
            int runtimeIdentity,
            AcgDevelopmentPlaceholderPlanEntry metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException("metadata");
            }

            lock (Sync)
            {
                if (ByRuntimeIdentity.ContainsKey(runtimeIdentity))
                {
                    throw new InvalidOperationException(
                        "ACG placeholder runtime identity was registered twice: "
                        + runtimeIdentity.ToString(CultureInfo.InvariantCulture));
                }

                ByRuntimeIdentity.Add(runtimeIdentity, metadata);
                HashSet<int> identities;
                if (!RuntimeIdentitiesByPlayfield.TryGetValue(playfieldInstance, out identities))
                {
                    identities = new HashSet<int>();
                    RuntimeIdentitiesByPlayfield.Add(playfieldInstance, identities);
                }

                identities.Add(runtimeIdentity);
            }
        }

        internal static void Remove(int playfieldInstance, int runtimeIdentity)
        {
            lock (Sync)
            {
                ByRuntimeIdentity.Remove(runtimeIdentity);
                HashSet<int> identities;
                if (!RuntimeIdentitiesByPlayfield.TryGetValue(playfieldInstance, out identities))
                {
                    return;
                }

                identities.Remove(runtimeIdentity);
                if (identities.Count == 0)
                {
                    RuntimeIdentitiesByPlayfield.Remove(playfieldInstance);
                }
            }
        }

        internal static void ClearPlayfield(int playfieldInstance)
        {
            lock (Sync)
            {
                HashSet<int> identities;
                if (!RuntimeIdentitiesByPlayfield.TryGetValue(playfieldInstance, out identities))
                {
                    return;
                }

                foreach (int identity in identities)
                {
                    ByRuntimeIdentity.Remove(identity);
                }

                RuntimeIdentitiesByPlayfield.Remove(playfieldInstance);
            }
        }
    }

    internal sealed class AcgDevelopmentPlaceholderRuntimeService
    {
        private const string SafePlaceholderTemplateHash = "A004";

        private const int SimpleCharFullUpdateIsImmuneFlag = 0x00800000;

        private const int PlaceholderMeshPosition = 1;

        private const int PlaceholderMeshLayer = 4;

        private readonly AcgDevelopmentPlaceholderOptions options;

        private readonly AcgDevelopmentPlaceholderCatalog catalog;

        private AcgDevelopmentPlaceholderRuntimeService(
            AcgDevelopmentPlaceholderOptions options,
            AcgDevelopmentPlaceholderCatalog catalog)
        {
            this.options = options ?? throw new ArgumentNullException("options");
            if (!options.IsOff && catalog == null)
            {
                throw new ArgumentNullException("catalog");
            }

            this.catalog = catalog;
        }

        internal AcgDevelopmentPlaceholderMode Mode
        {
            get { return this.options.Mode; }
        }

        internal static AcgDevelopmentPlaceholderRuntimeService FromEnvironment(
            string zoneEngineBaseDirectory)
        {
            AcgDevelopmentPlaceholderOptions options =
                AcgDevelopmentPlaceholderOptions.FromEnvironment();
            if (options.IsOff)
            {
                return new AcgDevelopmentPlaceholderRuntimeService(options, null);
            }

            string corpusRoot = AcgDevelopmentPlaceholderCatalog.ResolveRuntimeCorpusRoot(
                zoneEngineBaseDirectory);
            return new AcgDevelopmentPlaceholderRuntimeService(
                options,
                new AcgDevelopmentPlaceholderCatalog(corpusRoot));
        }

        internal int Materialize(
            Playfield playfield,
            Identity playfieldIdentity,
            Action<ICharacter> activateNpc,
            Action<Identity> deactivateNpc)
        {
            if (playfield == null)
            {
                throw new ArgumentNullException("playfield");
            }

            if (activateNpc == null)
            {
                throw new ArgumentNullException("activateNpc");
            }

            if (deactivateNpc == null)
            {
                throw new ArgumentNullException("deactivateNpc");
            }

            if (this.options.IsOff)
            {
                return 0;
            }

            IList<AcgDevelopmentPlaceholderPlanEntry> plan = this.catalog.CreatePlan(
                this.options,
                playfieldIdentity.Instance);
            if (plan.Count == 0)
            {
                return 0;
            }

            if (MobTemplateDao.Instance.GetMobTemplateByHash(SafePlaceholderTemplateHash) == null)
            {
                throw new InvalidOperationException(
                    "ACG placeholder visual selection failed closed: proven template A004 is unavailable.");
            }

            if (this.options.Mode == AcgDevelopmentPlaceholderMode.CurrentPlayfieldAllPoints)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "ACG development placeholder warning: CurrentPlayfieldAllPoints includes official "
                    + "AdditionalPoints with unresolved runtime multiplicity semantics.");
            }

            var materialized = new List<Character>();
            try
            {
                foreach (AcgDevelopmentPlaceholderPlanEntry entry in plan)
                {
                    Character character = this.MaterializeOne(
                        playfield,
                        playfieldIdentity,
                        entry,
                        activateNpc,
                        deactivateNpc);
                    materialized.Add(character);
                }
            }
            catch
            {
                foreach (Character character in materialized)
                {
                    AcgDevelopmentPlaceholderRuntimeRegistry.Remove(
                        playfieldIdentity.Instance,
                        character.Identity.Instance);
                    deactivateNpc(character.Identity);
                    character.Dispose();
                }

                throw;
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ACG development placeholders materialized mode={0} pf={1} count={2}",
                    this.options.Mode,
                    playfieldIdentity.Instance,
                    materialized.Count));
            return materialized.Count;
        }

        private Character MaterializeOne(
            Playfield playfield,
            Identity playfieldIdentity,
            AcgDevelopmentPlaceholderPlanEntry entry,
            Action<ICharacter> activateNpc,
            Action<Identity> deactivateNpc)
        {
            if (entry.UseExactOfficialVisual)
            {
                AcgVisualResolution visual = this.catalog.GetVisual(entry.AcgHashNativeUInt32);
                if (!string.Equals(
                        visual.ServerTemplateHash,
                        SafePlaceholderTemplateHash,
                        StringComparison.Ordinal)
                    || visual.ServerTemplateId != 43296
                    || visual.MonsterDataInstance != 17655
                    || visual.ExactMeshInstance != 15222)
                {
                    throw new InvalidOperationException(
                        "ExactOfficial FDQO visual bridge failed closed before materialization.");
                }
            }

            if (entry.UseExactOfficialVisual)
            {
                if (entry.SelectedCatMeshId != AcgDevelopmentPlaceholderCatalog.ExactFdqoCatMeshId
                    || entry.SelectedItemId.HasValue
                    || entry.SelectedMeshId.HasValue)
                {
                    throw new InvalidOperationException(
                        "ACG development exact visual selection failed closed for "
                        + entry.OfficialSpawnRecordId);
                }
            }
            else
            {
                ItemTemplate placeholderItem;
                int itemMesh;
                if (entry.SelectedCatMeshId.HasValue
                    || entry.SelectedItemId != AcgDevelopmentPlaceholderCatalog.DefaultPlaceholderItemId
                    || entry.SelectedMeshId != AcgDevelopmentPlaceholderCatalog.DefaultPlaceholderMeshId
                    || !ItemLoader.ItemList.TryGetValue(
                        AcgDevelopmentPlaceholderCatalog.DefaultPlaceholderItemId,
                        out placeholderItem)
                    || !placeholderItem.Stats.TryGetValue(
                        AcgDevelopmentPlaceholderCatalog.DefaultPlaceholderMeshStatId,
                        out itemMesh)
                    || itemMesh != AcgDevelopmentPlaceholderCatalog.DefaultPlaceholderMeshId)
                {
                    throw new InvalidOperationException(
                        "ACG development item-mesh placeholder selection failed closed for "
                        + entry.OfficialSpawnRecordId);
                }
            }

            var controller = new NPCController
            {
                AiProfile = NpcAiProfile.Passive,
                State = CharacterState.Idle
            };
            Character character = NonPlayerCharacterHandler.SpawnMobFromTemplate(
                SafePlaceholderTemplateHash,
                playfieldIdentity,
                new Coordinate
                {
                    x = (float)entry.PositionX,
                    y = (float)entry.PositionY,
                    z = (float)entry.PositionZ
                },
                new CoreQuaternion(0.0f, 0.0f, 0.0f, 1.0f),
                controller,
                1);
            if (character == null)
            {
                throw new InvalidOperationException(
                    "ACG placeholder visual construction failed for "
                    + entry.OfficialSpawnRecordId);
            }

            character.Name = entry.VisibleName;
            character.FirstName = string.Empty;
            character.LastName = string.Empty;
            character.Playfield = playfield;
            character.Waypoints.Clear();
            controller.State = CharacterState.Idle;
            character.DoNotDoTimers = true;
            character.Stats.SetBaseValueWithoutTriggering(
                (int)StatIds.side,
                (uint)Side.Neutral);
            if (entry.UseExactOfficialVisual)
            {
                character.Stats.SetBaseValueWithoutTriggering(
                    (int)StatIds.catmesh,
                    (uint)entry.SelectedCatMeshId.Value);
                character.Stats.SetBaseValueWithoutTriggering(
                    (int)StatIds.displaycatmesh,
                    (uint)entry.SelectedCatMeshId.Value);
            }
            else
            {
                character.Stats.SetBaseValueWithoutTriggering(
                    (int)StatIds.monsterdata,
                    0);
                character.Stats.SetBaseValueWithoutTriggering(
                    (int)StatIds.catmesh,
                    0);
                character.Stats.SetBaseValueWithoutTriggering(
                    (int)StatIds.displaycatmesh,
                    0);
                character.Stats.SetBaseValueWithoutTriggering(
                    (int)StatIds.headmesh,
                    0);
                character.Stats.SetBaseValueWithoutTriggering(
                    (int)StatIds.weaponmeshright,
                    (uint)entry.SelectedMeshId.Value);
                character.Textures.Clear();
                character.MeshLayer.Clear();
                character.SocialMeshLayer.Clear();
                character.MeshLayer.AddMesh(
                    PlaceholderMeshPosition,
                    entry.SelectedMeshId.Value,
                    0,
                    PlaceholderMeshLayer);
                character.SocialMeshLayer.AddMesh(
                    PlaceholderMeshPosition,
                    entry.SelectedMeshId.Value,
                    0,
                    PlaceholderMeshLayer);
            }
            uint flags = (uint)character.Stats[StatIds.flags].Value;
            character.Stats.SetBaseValueWithoutTriggering(
                (int)StatIds.flags,
                flags | SimpleCharFullUpdateIsImmuneFlag);

            AcgDevelopmentPlaceholderRuntimeRegistry.Register(
                playfieldIdentity.Instance,
                character.Identity.Instance,
                entry);
            try
            {
                activateNpc(character);
                playfield.AnnounceSpawnedCharacterVisibility(character, Identity.None);
            }
            catch
            {
                AcgDevelopmentPlaceholderRuntimeRegistry.Remove(
                    playfieldIdentity.Instance,
                    character.Identity.Instance);
                deactivateNpc(character.Identity);
                character.Dispose();
                throw;
            }

            return character;
        }
    }
}
