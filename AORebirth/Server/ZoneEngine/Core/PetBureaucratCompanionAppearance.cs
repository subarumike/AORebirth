#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core
{
    #region Usings ...

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Textures;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Capture-backed human appearance for Bureaucrat companion pets.
    /// Source: captures 20260713-130330 (Carlo Pinnetti, Carlita Desposito).
    /// Briefcase is weapon template 258219 in the right hand (WeaponItemFullUpdate),
    /// not a shoulder mesh. Mesh 29084 is the live back mesh at position 1 / layer 2.
    /// </summary>
    internal static class PetBureaucratCompanionAppearance
    {
        private const int CarloTorsoMeshId = 204896;
        private const int CarlitaTorsoMeshId = 204913;
        private const int CompanionBackMeshId = 29084;
        private const int CompanionBriefcaseItemId = 258219;
        private const int CompanionBriefcaseQuality = 100;

        public static void Apply(Character petCharacter, DBMobTemplate mobTemplate)
        {
            if (petCharacter == null || mobTemplate == null)
            {
                return;
            }

            petCharacter.Textures.Clear();
            AddTexture(petCharacter, 0, mobTemplate.TextureHands);
            AddTexture(petCharacter, 1, mobTemplate.TextureBody);
            AddTexture(petCharacter, 2, mobTemplate.TextureFeet);
            AddTexture(petCharacter, 3, mobTemplate.TextureArms);
            AddTexture(petCharacter, 4, mobTemplate.TextureLegs);

            if (mobTemplate.HeadMesh > 0)
            {
                petCharacter.MeshLayer.Clear();
                petCharacter.SocialMeshLayer.Clear();
                petCharacter.MeshLayer.AddMesh(0, mobTemplate.HeadMesh, 0, 4);
                petCharacter.SocialMeshLayer.AddMesh(0, mobTemplate.HeadMesh, 0, 4);
            }

            int torsoMeshId;
            if (!TryResolveTorsoMeshId(mobTemplate.Hash, out torsoMeshId))
            {
                return;
            }

            // Live SCFU meshes (20260713-130330):
            // pos0 torso, pos0 head (layer 4), pos1 mesh 29084 (layer 2).
            // Do NOT place 29084 at shoulder slots 3/4 — visualflags 31 double-pads
            // duplicate shoulder meshes into two floating briefcases.
            petCharacter.MeshLayer.AddMesh(2, torsoMeshId, 0, 0);
            petCharacter.SocialMeshLayer.AddMesh(2, torsoMeshId, 0, 0);
            petCharacter.MeshLayer.AddMesh(1, CompanionBackMeshId, 0, 2);
            petCharacter.SocialMeshLayer.AddMesh(1, CompanionBackMeshId, 0, 2);

            TryEquipBriefcase(petCharacter);
        }

        public static bool TryEquipBriefcase(Character petCharacter)
        {
            if (petCharacter == null || petCharacter.BaseInventory == null)
            {
                return false;
            }

            if (!ItemLoader.ItemList.ContainsKey(CompanionBriefcaseItemId))
            {
                return false;
            }

            IInventoryPage weaponPage;
            if (!petCharacter.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage))
            {
                return false;
            }

            int rightHandSlot = (int)WeaponSlots.Righthand;
            if (!weaponPage.ValidSlot(rightHandSlot) || weaponPage[rightHandSlot] != null)
            {
                return false;
            }

            var briefcase = new Item(
                CompanionBriefcaseQuality,
                CompanionBriefcaseItemId,
                CompanionBriefcaseItemId)
            {
                MultipleCount = 1
            };

            return weaponPage.Add(rightHandSlot, briefcase) == InventoryError.OK;
        }

        private static bool TryResolveTorsoMeshId(string mobHash, out int torsoMeshId)
        {
            if (string.Equals(mobHash, "A142", System.StringComparison.OrdinalIgnoreCase))
            {
                torsoMeshId = CarloTorsoMeshId;
                return true;
            }

            if (string.Equals(mobHash, "CRLT", System.StringComparison.OrdinalIgnoreCase))
            {
                torsoMeshId = CarlitaTorsoMeshId;
                return true;
            }

            torsoMeshId = 0;
            return false;
        }

        private static void AddTexture(Character petCharacter, int place, int textureId)
        {
            if (textureId > 0)
            {
                petCharacter.Textures.Add(new AOTextures(place, textureId));
            }
        }
    }
}
