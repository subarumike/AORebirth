namespace ZoneEngine.Core.Arete.Dialogue
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using ZoneEngine.Core.Arete;

    #endregion

    public sealed class DialogueContentRegistry
    {
        private readonly Dictionary<string, DialogueContentPack> packsById =
            new Dictionary<string, DialogueContentPack>(System.StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, DialogueNpcEntry> npcsByIdentity =
            new Dictionary<string, DialogueNpcEntry>(System.StringComparer.OrdinalIgnoreCase);

        public int PackCount
        {
            get
            {
                return this.packsById.Count;
            }
        }

        public int NpcCount
        {
            get
            {
                return this.npcsByIdentity.Count;
            }
        }

        public AreteValidationResult Load(IEnumerable<DialogueContentPack> packs)
        {
            AreteContentLoadResult<DialogueContentPack> loadResult = new DialogueContentPackLoader().Load(packs);
            return this.ApplyLoadResult(loadResult);
        }

        public AreteValidationResult LoadFromFiles(IEnumerable<string> filePaths)
        {
            AreteContentLoadResult<DialogueContentPack> loadResult = new DialogueContentPackLoader().LoadFiles(filePaths);
            return this.ApplyLoadResult(loadResult);
        }

        public AreteValidationResult LoadFromDirectory(string directoryPath)
        {
            AreteContentLoadResult<DialogueContentPack> loadResult =
                new DialogueContentPackLoader().LoadDirectory(directoryPath);

            return this.ApplyLoadResult(loadResult);
        }

        public AreteValidationResult LoadFromManifest(string manifestPath)
        {
            AreteContentLoadResult<DialogueContentPack> loadResult =
                new DialogueContentPackLoader().LoadManifest(manifestPath);

            return this.ApplyLoadResult(loadResult);
        }

        private AreteValidationResult ApplyLoadResult(AreteContentLoadResult<DialogueContentPack> loadResult)
        {
            if (!loadResult.IsValid)
            {
                return loadResult.Validation;
            }

            this.packsById.Clear();
            this.npcsByIdentity.Clear();

            foreach (DialogueContentPack pack in loadResult.Packs)
            {
                this.packsById.Add(pack.Identity.Id, pack);
                foreach (DialogueNpcEntry npc in pack.Npcs ?? Enumerable.Empty<DialogueNpcEntry>())
                {
                    this.npcsByIdentity.Add(npc.NpcIdentity, npc);
                }
            }

            return loadResult.Validation;
        }

        public bool TryGetNpc(string npcIdentity, out DialogueNpcEntry npc)
        {
            if (string.IsNullOrWhiteSpace(npcIdentity))
            {
                npc = null;
                return false;
            }

            return this.npcsByIdentity.TryGetValue(npcIdentity, out npc);
        }
    }
}
