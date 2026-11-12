namespace AORebirth.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;

    /// <summary>Authored item behavior used by shared-core item and inventory implementations.</summary>
    public sealed class ItemBehaviorBindings
    {
        private static readonly Lazy<ItemBehaviorBindings> RuntimeBindings = new Lazy<ItemBehaviorBindings>(
            () => Load(GameDataPaths.ResolveFile(GameDataPaths.ResolveRuntimeRoot(), GameDataPaths.ItemBehaviorFileName)));

        private readonly Dictionary<int, int> icons = new Dictionary<int, int>();
        private readonly Dictionary<int, int> vendorMeshes = new Dictionary<int, int>();
        private readonly HashSet<(int LowId, int HighId)> backpackContainers = new HashSet<(int, int)>();
        private readonly Dictionary<(int TemplateId, int MeshId, int CurrentTextureId), int> wornMeshOverrides =
            new Dictionary<(int, int, int), int>();
        private readonly HashSet<int> referencedTemplates = new HashSet<int>();

        private ItemBehaviorBindings(BindingsData data)
        {
            if (data == null || data.IconOverrides == null || data.IconOverrides.Length == 0
                || data.BackpackContainerTemplates == null || data.BackpackContainerTemplates.Length == 0
                || data.WornMeshOverrides == null || data.WornMeshOverrides.Length == 0
                || data.NpcFallbackWeaponTemplateId <= 0 || data.VendorFallbackTemplateId <= 0
                || data.VendorMeshOverrides == null || data.VendorMeshOverrides.Length == 0)
            {
                throw new InvalidDataException("ItemBehavior contains missing or invalid shared item bindings.");
            }

            NpcFallbackWeaponTemplateId = data.NpcFallbackWeaponTemplateId;
            VendorFallbackTemplateId = data.VendorFallbackTemplateId;
            referencedTemplates.Add(NpcFallbackWeaponTemplateId);
            referencedTemplates.Add(VendorFallbackTemplateId);

            foreach (VendorMeshOverrideData row in data.VendorMeshOverrides)
            {
                if (row == null || row.TemplateId <= 0 || row.MeshId <= 0 || vendorMeshes.ContainsKey(row.TemplateId))
                {
                    throw new InvalidDataException("ItemBehavior contains an invalid or duplicate vendor mesh override.");
                }

                vendorMeshes.Add(row.TemplateId, row.MeshId);
                referencedTemplates.Add(row.TemplateId);
            }

            foreach (IconOverrideData row in data.IconOverrides)
            {
                if (row == null || row.TemplateId <= 0 || row.IconId <= 0 || icons.ContainsKey(row.TemplateId))
                {
                    throw new InvalidDataException("ItemBehavior contains an invalid or duplicate icon override.");
                }

                icons.Add(row.TemplateId, row.IconId);
                referencedTemplates.Add(row.TemplateId);
            }

            foreach (TemplatePairData row in data.BackpackContainerTemplates)
            {
                if (row == null || row.LowId <= 0 || row.HighId <= 0 || !backpackContainers.Add((row.LowId, row.HighId)))
                {
                    throw new InvalidDataException("ItemBehavior contains an invalid or duplicate backpack template pair.");
                }

                referencedTemplates.Add(row.LowId);
                referencedTemplates.Add(row.HighId);
            }

            foreach (WornMeshOverrideData row in data.WornMeshOverrides)
            {
                if (row == null || row.TemplateIds == null || row.TemplateIds.Length == 0
                    || row.MeshId <= 0 || row.OnlyWhenOverrideTextureId < 0 || row.OverrideTextureId <= 0)
                {
                    throw new InvalidDataException("ItemBehavior contains an invalid worn mesh override.");
                }

                foreach (int templateId in row.TemplateIds)
                {
                    var key = (templateId, row.MeshId, row.OnlyWhenOverrideTextureId);
                    if (templateId <= 0 || wornMeshOverrides.ContainsKey(key))
                    {
                        throw new InvalidDataException("ItemBehavior contains an invalid or duplicate worn mesh template.");
                    }

                    wornMeshOverrides.Add(key, row.OverrideTextureId);
                    referencedTemplates.Add(templateId);
                }
            }
        }

        internal static ItemBehaviorBindings Current => RuntimeBindings.Value;

        public int NpcFallbackWeaponTemplateId { get; }

        public int VendorFallbackTemplateId { get; }

        public static ItemBehaviorBindings Load(string path)
        {
            using (FileStream input = File.OpenRead(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(BindingsData));
                return new ItemBehaviorBindings((BindingsData)serializer.ReadObject(input));
            }
        }

        public void ValidateTemplateReferences(Func<int, bool> templateExists)
        {
            if (templateExists == null)
            {
                throw new ArgumentNullException(nameof(templateExists));
            }

            foreach (int templateId in referencedTemplates)
            {
                if (!templateExists(templateId))
                {
                    throw new InvalidDataException("ItemBehavior references missing item template " + templateId + ".");
                }
            }
        }

        public bool IsBackpackContainerTemplate(int lowId, int highId)
        {
            return backpackContainers.Contains((lowId, highId));
        }

        public bool TryResolveIcon(int lowId, int highId, out int iconId)
        {
            return icons.TryGetValue(highId, out iconId) || icons.TryGetValue(lowId, out iconId);
        }

        public bool TryResolveVendorMesh(int templateId, out int meshId)
        {
            return vendorMeshes.TryGetValue(templateId, out meshId);
        }

        public bool TryResolveWornMeshOverride(int lowId, int highId, int meshId, int currentTextureId, out int textureId)
        {
            return wornMeshOverrides.TryGetValue((highId, meshId, currentTextureId), out textureId)
                || wornMeshOverrides.TryGetValue((lowId, meshId, currentTextureId), out textureId);
        }

        [DataContract]
        private sealed class BindingsData
        {
            [DataMember(IsRequired = true)]
            public IconOverrideData[] IconOverrides { get; set; }

            [DataMember(IsRequired = true)]
            public TemplatePairData[] BackpackContainerTemplates { get; set; }

            [DataMember(IsRequired = true)]
            public WornMeshOverrideData[] WornMeshOverrides { get; set; }

            [DataMember(IsRequired = true)]
            public int NpcFallbackWeaponTemplateId { get; set; }

            [DataMember(IsRequired = true)]
            public int VendorFallbackTemplateId { get; set; }

            [DataMember(IsRequired = true)]
            public VendorMeshOverrideData[] VendorMeshOverrides { get; set; }
        }

        [DataContract]
        private sealed class IconOverrideData
        {
            [DataMember(IsRequired = true)]
            public int TemplateId { get; set; }

            [DataMember(IsRequired = true)]
            public int IconId { get; set; }
        }

        [DataContract]
        private sealed class TemplatePairData
        {
            [DataMember(IsRequired = true)]
            public int LowId { get; set; }

            [DataMember(IsRequired = true)]
            public int HighId { get; set; }
        }

        [DataContract]
        private sealed class VendorMeshOverrideData
        {
            [DataMember(IsRequired = true)]
            public int TemplateId { get; set; }

            [DataMember(IsRequired = true)]
            public int MeshId { get; set; }
        }

        [DataContract]
        private sealed class WornMeshOverrideData
        {
            [DataMember(IsRequired = true)]
            public int[] TemplateIds { get; set; }

            [DataMember(IsRequired = true)]
            public int MeshId { get; set; }

            [DataMember(IsRequired = true)]
            public int OnlyWhenOverrideTextureId { get; set; }

            [DataMember(IsRequired = true)]
            public int OverrideTextureId { get; set; }
        }
    }
}
