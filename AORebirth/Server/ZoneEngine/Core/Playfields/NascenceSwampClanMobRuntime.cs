namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Text;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Packets;

    #endregion

    /// <summary>
    /// Capture-backed PF 4312 Redeemed / Malah clan mob visuals and faction penalties (20260823-192914).
    /// </summary>
    internal static class NascenceSwampClanMobRuntime
    {
        private const int KillFactionPenalty = 50;

        private const int TextureDruid = 235151;
        private const int TextureWarrior = 213984;
        private const int TextureNanoman = 213996;
        private const int TextureGrey = 236639;
        private const int TextureMedusa2 = 209049;

        private const int MeshDruid = 234636;
        private const int MeshWarrior = 234635;
        private const int MeshVendorA = 209532;
        private const int MeshVendorB = 209541;

        internal const int AppearanceClan = 1225;
        internal const int AppearanceMalah = 1227;

        internal const short NpcFamilyClan = 201;
        internal const short NpcFamilyMalah = 191;

        private const int CharFlagsStandard = 268964353;
        private const int CharFlagsVendor = 271061505;
        private const int CharFlagsFalaLike = 277352961;

        private static readonly byte[] ScfuUnknown1Default =
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x01, 0x00,
                0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00
            };

        private static readonly byte[] ScfuUnknown1WithHighByte =
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00,
                0x00, 0x02, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00
            };

        private static readonly byte[] ScfuUnknown1Vendor =
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00,
                0x00, 0x03, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00
            };

        private static readonly byte[] ScfuUnknown1Fala =
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x01, 0x00,
                0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00
            };

        private static readonly byte[] ScfuUnknown1Malah =
            {
                0xBF, 0xB8, 0x7E, 0xEF, 0xBD, 0xEB, 0x97, 0xC5, 0x3E, 0xCC, 0x49, 0x03, 0x02, 0x02, 0x01, 0x01,
                0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00
            };

        private enum SwampClanProfession
        {
            None = 0,
            Druid = 1,
            Warrior = 2,
            Nanoman = 3,
            Vendor = 4,
            Malah = 5
        }

        internal static bool IsSwampClanMobName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (NascenceLifeSpawn.IsRedeemedVillageClanNpcName(name))
            {
                return true;
            }

            return StartsWith(name, "Len-")
                   || StartsWith(name, "Cur-")
                   || StartsWith(name, "Or-")
                   || StartsWith(name, "Malah-")
                   || StartsWith(name, "Sipius Aban ")
                   || StartsWith(name, "Diviner Aban ")
                   || StartsWith(name, "Devoted Aban ")
                   || StartsWith(name, "Seeker Aban ")
                   || StartsWith(name, "Watcher Aban ");
        }

        internal static bool TryGetExtendedTextureOverride(string name, out byte[] data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            switch (ResolveProfession(name))
            {
                case SwampClanProfession.Druid:
                    data = BuildDualMaterialExtTex("druid", "druid 2 side(cloak)", TextureDruid);
                    return true;
                case SwampClanProfession.Warrior:
                    data = BuildDualMaterialExtTex("varrior", "varrior 2 side(cloak)", TextureWarrior);
                    return true;
                case SwampClanProfession.Nanoman:
                    data = BuildDualMaterialExtTex("nanoman 2 side cloak", "nanoman", TextureNanoman);
                    return true;
                case SwampClanProfession.Vendor:
                    data = BuildSingleMaterialExtTex("grey", TextureGrey);
                    return true;
                case SwampClanProfession.Malah:
                    data = BuildSingleMaterialExtTex("medusa2", TextureMedusa2, terminalFlag: 1);
                    return true;
                default:
                    return false;
            }
        }

        internal static bool TryGetScfuUnknown1(string name, out byte[] data)
        {
            data = null;
            if (!IsSwampClanMobName(name))
            {
                return false;
            }

            if (IsAbanFalaName(name))
            {
                data = (byte[])ScfuUnknown1Fala.Clone();
                return true;
            }

            if (IsOrMadaVendorName(name))
            {
                data = (byte[])ScfuUnknown1Vendor.Clone();
                return true;
            }

            if (StartsWith(name, "Malah-"))
            {
                data = (byte[])ScfuUnknown1Malah.Clone();
                return true;
            }

            if (StartsWith(name, "Or-")
                || StartsWith(name, "Cur-")
                || StartsWith(name, "Devoted Aban "))
            {
                data = (byte[])ScfuUnknown1WithHighByte.Clone();
                return true;
            }

            data = (byte[])ScfuUnknown1Default.Clone();
            return true;
        }

        internal static void ApplySpawnStats(Character mob, string name)
        {
            if (mob == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            SwampClanProfession profession = ResolveProfession(name);
            if (profession == SwampClanProfession.None)
            {
                return;
            }

            ClearTemplateHeadMesh(mob);
            ApplyCaptureAppearanceStats(mob);

            if (profession == SwampClanProfession.Malah)
            {
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)NpcFamilyMalah);
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, (uint)Side.Monster);
                mob.Stats[StatIds.side].Value = (int)Side.Monster;
                mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.flags, (uint)CharFlagsStandard);
                return;
            }

            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.npcfamily, (uint)NpcFamilyClan);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.side, (uint)Side.Clan);
            mob.Stats[StatIds.side].Value = (int)Side.Clan;

            int characterFlags = ResolveCharacterFlags(name, profession);
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.flags, (uint)characterFlags);

            int meshId;
            if (TryGetCaptureMeshId(name, profession, out meshId))
            {
                mob.MeshLayer.Clear();
                mob.SocialMeshLayer.Clear();
                mob.MeshLayer.AddMesh(1, meshId, 0, 2);
                mob.SocialMeshLayer.AddMesh(1, meshId, 0, 2);
            }
        }

        internal static void ApplyScfuOverrides(SimpleCharFullUpdateMessage scfu, string name)
        {
            if (scfu == null || !IsSwampClanMobName(name))
            {
                return;
            }

            SwampClanProfession profession = ResolveProfession(name);
            if (profession == SwampClanProfession.None)
            {
                return;
            }

            bool isMalah = profession == SwampClanProfession.Malah;
            bool isRedeemedVillage = NascenceLifeSpawn.IsRedeemedVillageClanNpcName(name);
            bool isAbanFala = IsAbanFalaName(name);
            bool isCurBeat = string.Equals(name, "Cur-Beat", StringComparison.OrdinalIgnoreCase);

            scfu.AdditionalFlags = SimpleCharFullUpdateFlags.UnknownFlag6 | SimpleCharFullUpdateFlags.IsPet;
            if (!isCurBeat && !isAbanFala)
            {
                scfu.AdditionalFlags |= SimpleCharFullUpdateFlags.UnknownFlag7;
            }

            scfu.AdditionalFlags |= SimpleCharFullUpdateFlags.UnknownDataFlag;
            scfu.SuppressedFlags = SimpleCharFullUpdateFlags.UnknownFlag2;

            byte[] unknown1;
            if (TryGetScfuUnknown1(name, out unknown1))
            {
                scfu.Unknown1 = unknown1;
            }
            else if (isRedeemedVillage)
            {
                byte[] redeemedUnknown1;
                if (NascenceLifeSpawn.TryGetRedeemedVillageClanScfuUnknown1(out redeemedUnknown1))
                {
                    scfu.Unknown1 = redeemedUnknown1;
                }
            }

            scfu.CharacterInfo =
                new SimpleNpcInfo
                {
                    Family = (short)(isMalah ? NpcFamilyMalah : NpcFamilyClan),
                    LosHeight = 0
                };

            scfu.Appearance.Value = (uint)(isMalah ? AppearanceMalah : AppearanceClan);
            scfu.Appearance.Side = isMalah ? Side.Monster : Side.Clan;
            scfu.Appearance.Breed = Breed.Monster;
            scfu.Appearance.Gender = Gender.None;
            scfu.Appearance.Race = 1;
            scfu.RunSpeedBase = (short)ResolveRunSpeedBase(name, profession);
            scfu.Meshes = BuildScfuMeshes(name, profession);

            if (isAbanFala)
            {
                scfu.AdditionalFlags |= SimpleCharFullUpdateFlags.UnknownFlag3;
            }
            else if (string.Equals(name, "Watcher Aban Wei-Nuir", StringComparison.OrdinalIgnoreCase))
            {
                scfu.AdditionalFlags |= SimpleCharFullUpdateFlags.UnknownFlag3;
            }
        }

        internal static bool TryApplyKillFactionPenalty(ICharacter attacker, ICharacter victim)
        {
            if (attacker == null || victim == null || attacker.Controller == null || attacker.Controller.Client == null)
            {
                return false;
            }

            if (victim.Controller is NPCController == false)
            {
                return false;
            }

            StatIds factionStat;
            if (!TryResolveKillFactionStat(victim.Name, out factionStat))
            {
                return false;
            }

            try
            {
                int current = attacker.Stats[factionStat].Value;
                attacker.Stats[factionStat].Value = current - KillFactionPenalty;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryResolveKillFactionStat(string name, out StatIds factionStat)
        {
            factionStat = StatIds.clanredeemed;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (StartsWith(name, "Malah-"))
            {
                return false;
            }

            if (!IsSwampClanMobName(name))
            {
                return false;
            }

            switch (ResolveProfession(name))
            {
                case SwampClanProfession.Druid:
                    factionStat = StatIds.clangaia;
                    return true;
                case SwampClanProfession.Warrior:
                    factionStat = StatIds.clanvanguards;
                    return true;
                case SwampClanProfession.Nanoman:
                    factionStat = StatIds.clansentinels;
                    return true;
                case SwampClanProfession.Vendor:
                    factionStat = StatIds.clandevoted;
                    return true;
                default:
                    factionStat = StatIds.clanredeemed;
                    return true;
            }
        }

        private static SwampClanProfession ResolveProfession(string name)
        {
            if (StartsWith(name, "Malah-"))
            {
                return SwampClanProfession.Malah;
            }

            if (IsOrMadaVendorName(name))
            {
                return SwampClanProfession.Vendor;
            }

            if (StartsWith(name, "Cur-")
                || IsAbanFalaName(name))
            {
                return SwampClanProfession.Druid;
            }

            if (StartsWith(name, "Len-")
                || StartsWith(name, "Devoted Aban "))
            {
                return SwampClanProfession.Nanoman;
            }

            if (StartsWith(name, "Or-")
                || StartsWith(name, "Diviner Aban ")
                || StartsWith(name, "Sipius Aban ")
                || StartsWith(name, "Seeker Aban ")
                || StartsWith(name, "Watcher Aban "))
            {
                return SwampClanProfession.Warrior;
            }

            if (NascenceLifeSpawn.IsRedeemedVillageClanNpcName(name))
            {
                if (IsAbanFalaName(name))
                {
                    return SwampClanProfession.Druid;
                }

                if (string.Equals(name, "Devoted Aban Path-Duna", StringComparison.OrdinalIgnoreCase))
                {
                    return SwampClanProfession.Nanoman;
                }

                return SwampClanProfession.Warrior;
            }

            return SwampClanProfession.None;
        }

        private static bool IsAbanFalaName(string name)
        {
            return string.Equals(name, "Ecclesiast Aban Fala", StringComparison.OrdinalIgnoreCase);
        }

        private static int ResolveCharacterFlags(string name, SwampClanProfession profession)
        {
            if (IsAbanFalaName(name)
                || string.Equals(name, "Seeker Aban Kald-Nuir", StringComparison.OrdinalIgnoreCase))
            {
                return CharFlagsFalaLike;
            }

            if (profession == SwampClanProfession.Vendor)
            {
                return CharFlagsVendor;
            }

            return CharFlagsStandard;
        }

        private static int ResolveRunSpeedBase(string name, SwampClanProfession profession)
        {
            if (profession == SwampClanProfession.Vendor)
            {
                return 103;
            }

            if (StartsWith(name, "Len-"))
            {
                return 120;
            }

            if (StartsWith(name, "Or-Farat") || StartsWith(name, "Or-Jerad"))
            {
                return string.Equals(name, "Or-Farat", StringComparison.OrdinalIgnoreCase) ? 154 : 171;
            }

            return 137;
        }

        private static bool TryGetCaptureMeshId(string name, SwampClanProfession profession, out int meshId)
        {
            meshId = 0;
            switch (profession)
            {
                case SwampClanProfession.Druid:
                    meshId = MeshDruid;
                    return true;
                case SwampClanProfession.Warrior:
                    meshId = MeshWarrior;
                    return true;
                case SwampClanProfession.Vendor:
                    meshId = string.Equals(name, "Or-Mada of the Furious Fists", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(name, "Or-Mada of Flaming Barrels", StringComparison.OrdinalIgnoreCase)
                                 ? MeshVendorA
                                 : MeshVendorB;
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsOrMadaVendorName(string name)
        {
            return StartsWith(name, "Or-Mada");
        }

        private static bool StartsWith(string value, string prefix)
        {
            return value != null
                   && prefix != null
                   && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static void ClearTemplateHeadMesh(Character mob)
        {
            int existingHeadMesh = mob.Stats[StatIds.headmesh].Value;
            if (existingHeadMesh != 0)
            {
                mob.MeshLayer.RemoveMesh(0, existingHeadMesh, 0, 4);
                mob.SocialMeshLayer.RemoveMesh(0, existingHeadMesh, 0, 4);
            }

            mob.MeshLayer.RemoveMesh(0, 0, 0, 4);
            mob.SocialMeshLayer.RemoveMesh(0, 0, 0, 4);
            mob.Stats[StatIds.headmesh].BaseValue = 0;
            mob.Stats[StatIds.headmesh].Value = 0;
        }

        private static void ApplyCaptureAppearanceStats(Character mob)
        {
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.breed, (uint)Breed.Monster);
            mob.Stats[StatIds.breed].Value = (int)Breed.Monster;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.sex, (uint)Gender.None);
            mob.Stats[StatIds.sex].Value = (int)Gender.None;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.race, 1u);
            mob.Stats[StatIds.race].Value = 1;
            mob.Stats.SetBaseValueWithoutTriggering((int)StatIds.visualflags, 31u);
            mob.Stats[StatIds.visualflags].Value = 31;
        }

        private static Mesh[] BuildScfuMeshes(string name, SwampClanProfession profession)
        {
            int meshId;
            if (!TryGetCaptureMeshId(name, profession, out meshId))
            {
                return new Mesh[0];
            }

            return new[]
                       {
                           new Mesh
                           {
                               Position = 1,
                               Id = (uint)meshId,
                               OverrideTextureId = 0,
                               Layer = 2
                           }
                       };
        }

        private static byte[] BuildDualMaterialExtTex(string primaryMaterial, string secondaryMaterial, int textureId)
        {
            byte[] buffer = new byte[92];
            buffer[2] = 0x0B;
            buffer[3] = 0xD3;
            WriteAsciiField(buffer, 4, primaryMaterial, 32);
            WriteTextureId(buffer, 36, textureId);
            WriteAsciiField(buffer, 48, secondaryMaterial, 32);
            WriteTextureId(buffer, 80, textureId);
            return buffer;
        }

        private static byte[] BuildSingleMaterialExtTex(string material, int textureId, byte terminalFlag = 0)
        {
            byte[] buffer = new byte[48];
            buffer[2] = 0x07;
            buffer[3] = 0xE2;
            WriteAsciiField(buffer, 4, material, 32);
            WriteTextureId(buffer, 36, textureId);
            if (terminalFlag != 0)
            {
                buffer[47] = terminalFlag;
            }

            return buffer;
        }

        private static void WriteAsciiField(byte[] buffer, int offset, string text, int fieldLength)
        {
            if (buffer == null || string.IsNullOrEmpty(text) || fieldLength <= 0)
            {
                return;
            }

            byte[] ascii = Encoding.ASCII.GetBytes(text);
            int copy = Math.Min(ascii.Length, fieldLength - 1);
            Array.Copy(ascii, 0, buffer, offset, copy);
        }

        private static void WriteTextureId(byte[] buffer, int offset, int textureId)
        {
            buffer[offset] = 0;
            buffer[offset + 1] = (byte)((textureId >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((textureId >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(textureId & 0xFF);
        }
    }
}
