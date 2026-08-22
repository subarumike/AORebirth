#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace ZoneEngine.Core.Packets
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Nanos;
    using AORebirth.Core.Network;
    using AORebirth.Core.Textures;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;
    using ZoneEngine.Core.Subway.Quests;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    #endregion

    /// <summary>
    /// </summary>
    public static class SimpleCharFullUpdate
    {
        private const int SubwayPlayfieldResource = 127;
        private static readonly byte[] CapturedSubwayFilthFleaExtendedTextureOverrideData =
            new byte[]
                {
                    0x00, 0x00, 0x07, 0xE2, 0x4D, 0x61, 0x74, 0x65,
                    0x72, 0x69, 0x61, 0x6C, 0x20, 0x23, 0x39, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x3B, 0x81,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01
                };

        private static readonly byte[] CapturedSubwayThiefUnknown1 =
            new byte[]
                {
                    0x3F, 0xBC, 0xC2, 0x27, 0x3D, 0x55, 0xBB, 0xA1,
                    0xBE, 0x89, 0xF0, 0x4E, 0x02, 0x02, 0x01, 0x01,
                    0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
                    0x00, 0x02, 0x00, 0x00
                };

        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <returns>
        /// </returns>
        public static SimpleCharFullUpdateMessage ConstructMessage(Character character)
        {
            // No need to set packet flags here, its all done in the SimpleCharFullUpdateSerializer.cs
            // - Algorithman

            // Character Variables
            bool socialonly;
            bool showsocial;

            int charPlayfield;
            Coordinate charCoord;
            Identity charId;
            Quaternion charHeading;

            uint sideValue;
            uint fatValue;
            uint breedValue;
            uint sexValue;
            uint raceValue;

            string charName;
            int charFlagsValue;
            int accFlagsValue;

            int expansionValue;
            int currentNano;
            int currentHealth;

            uint strengthBaseValue;
            uint staminaBaseValue;
            uint agilityBaseValue;
            uint senseBaseValue;
            uint intelligenceBaseValue;
            uint psychicBaseValue;

            string firstName;
            string lastName;
            int orgNameLength;
            string orgName;
            int levelValue;
            int healthValue;
            int losHeight;

            int petMasterInstance;

            int monsterData;
            int monsterScale;
            int visualFlags;

            int currentMovementMode;
            uint runSpeedBaseValue;

            int texturesCount;

            int headMeshValue;

            // NPC Values
            int NPCFamily;

            var socialTab = new Dictionary<int, int>();

            var textures = new List<AOTextures>();

            List<AOMeshs> meshs;

            var nanos = new List<AONano>();

            lock (character)
            {
                socialonly = (character.Stats[StatIds.visualflags].Value & 0x40) > 0;
                showsocial = (character.Stats[StatIds.visualflags].Value & 0x20) > 0;

                charPlayfield = character.Playfield.Identity.Instance;
                charCoord = character.Coordinates();
                charId = character.Identity;
                charHeading = character.Heading;

                sideValue = character.Stats[StatIds.side].BaseValue;
                fatValue = character.Stats[StatIds.fatness].BaseValue;
                breedValue = character.Stats[StatIds.breed].BaseValue;
                sexValue = character.Stats[StatIds.sex].BaseValue;
                raceValue = character.Stats[StatIds.race].BaseValue;

                charName = character.Name;
                charFlagsValue = character.Stats[StatIds.flags].Value;
                bool isGm = character.Stats[StatIds.gmlevel].Value > 0;
                if (isGm)
                {
                    // Green unknown without capture. Use confirmed blue bit + "[GM]" suffix.
                    charFlagsValue &= ~(int)CharacterFlags.NpcStyleFlag28;
                    charFlagsValue |= (int)CharacterFlags.HasBlueName;
                    if (charName != null
                        && charName.IndexOf("[GM]", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        charName = charName + " [GM]";
                    }
                }

                accFlagsValue = character.Stats[StatIds.accountflags].Value;

                expansionValue = character.Stats[StatIds.expansion].Value;
                currentNano = character.Stats[StatIds.currentnano].Value;

                strengthBaseValue = character.Stats[StatIds.strength].BaseValue;
                staminaBaseValue = character.Stats[StatIds.stamina].BaseValue;
                agilityBaseValue = character.Stats[StatIds.agility].BaseValue;
                senseBaseValue = character.Stats[StatIds.sense].BaseValue;
                intelligenceBaseValue = character.Stats[StatIds.intelligence].BaseValue;
                psychicBaseValue = character.Stats[StatIds.psychic].BaseValue;

                firstName = character.FirstName;
                lastName = character.LastName;
                orgNameLength = character.OrganizationName.Length;
                orgName = character.OrganizationName;
                levelValue = CombatXpRuntimeService.ResolveWireLevel(character);
                healthValue = character.Stats[StatIds.life].Value;

                monsterData = character.Stats[StatIds.monsterdata].Value;
                monsterScale = character.Stats[StatIds.monsterscale].Value;
                visualFlags = character.Stats[StatIds.visualflags].Value;

                currentMovementMode = character.Stats[StatIds.currentmovementmode].Value;
                runSpeedBaseValue = character.Stats[StatIds.runspeed].BaseValue;

                texturesCount = character.Textures.Count;

                headMeshValue = character.Stats[StatIds.headmesh].Value;

                foreach (int num in character.SocialTab.Keys)
                {
                    socialTab.Add(num, character.SocialTab[num]);
                }

                foreach (AOTextures at in character.Textures)
                {
                    textures.Add(new AOTextures(at.place, at.Texture));
                }

                meshs = MeshLayers.GetMeshs(character, showsocial, socialonly);

                foreach (KeyValuePair<int, IActiveNano> kv in character.ActiveNanos)
                {
                    var tempNano = new AONano();
                    tempNano.ID = kv.Value.ID;
                    tempNano.Instance = kv.Value.Instance;
                    tempNano.NanoStrain = kv.Key;
                    tempNano.Nanotype = kv.Value.Nanotype;
                    tempNano.TickCounter = kv.Value.TickCounter;
                    tempNano.TickInterval = kv.Value.TickInterval;
                    tempNano.Value3 = kv.Value.Value3;

                    nanos.Add(tempNano);
                }

                losHeight = character.Stats[StatIds.losheight].Value;
                NPCFamily = character.Stats[StatIds.npcfamily].Value;
                petMasterInstance = character.Stats[StatIds.petmaster].Value;
                currentHealth = character.Stats[StatIds.health].Value;
            }

            var scfu = new SimpleCharFullUpdateMessage();
            OrdinaryEnemyRuntimeDefinition ordinaryRuntime;
            bool hasOrdinaryRuntime =
                OrdinaryEnemyRuntimeRegistry.TryGet(character.Identity.Instance, out ordinaryRuntime);
            CapturedEncounterRuntimeDefinition encounterRuntime;
            bool hasEncounterRuntime =
                CapturedEncounterRuntimeRegistry.TryGet(character.Identity.Instance, out encounterRuntime);
            CapturedSubwayVendorRuntimeDefinition capturedVendorRuntime;
            bool hasCapturedVendorRuntime =
                CapturedSubwayVendorRuntimeRegistry.TryGet(
                    character.Identity.Instance,
                    out capturedVendorRuntime);
            WindcallerKarrecNpcRuntimeDefinition windcallerNpcRuntime;
            bool hasWindcallerNpcRuntime =
                WindcallerKarrecNpcRuntimeRegistry.TryGet(
                    character.Identity.Instance,
                    out windcallerNpcRuntime);

            // affected identity
            scfu.Identity = charId;

            scfu.Version = 57; // SCFU packet version (57/0x39)
            if (hasEncounterRuntime
                || hasCapturedVendorRuntime
                || hasWindcallerNpcRuntime
                || (hasOrdinaryRuntime
                 && ordinaryRuntime.Profile.Appearance.ScfuProfile
                 == OrdinaryEnemyScfuProfile.CapturedThief)
                || (charPlayfield == SubwayPlayfieldResource
                    && character.Waypoints != null
                    && character.Waypoints.Count > 1))
            {
                scfu.Version = 58;
            }
            else if (petMasterInstance != 0)
            {
                scfu.Version = 58;
            }

            scfu.PlayfieldId = charPlayfield; // playfield

            if (character.FightingTarget.Instance != 0)
            {
                scfu.FightingTarget = new Identity
                                      {
                                          Type = character.FightingTarget.Type,
                                          Instance = character.FightingTarget.Instance
                                      };
            }

            // Coordinates
            scfu.Coordinates = new Vector3 { X = charCoord.x, Y = charCoord.y, Z = charCoord.z };

            // Heading Data
            scfu.Heading = new SmokeLounge.AOtomation.Messaging.GameData.Quaternion
                           {
                               W = charHeading.wf,
                               X = charHeading.xf,
                               Y = charHeading.yf,
                               Z = charHeading.zf
                           };

            // Race
            scfu.Appearance = new Appearance
                              {
                                  Side = hasWindcallerNpcRuntime
                                             ? (Side)windcallerNpcRuntime.Content.Side
                                             : hasCapturedVendorRuntime
                                             ? (Side)capturedVendorRuntime.Content.Side
                                             : hasEncounterRuntime
                                             ? (Side)encounterRuntime.Side
                                             : (Side)sideValue,
                                  Fatness = hasWindcallerNpcRuntime
                                                ? (Fatness)windcallerNpcRuntime.Content.Fatness
                                                : hasCapturedVendorRuntime
                                                ? (Fatness)capturedVendorRuntime.Content.Fatness
                                                : hasEncounterRuntime
                                                ? (Fatness)encounterRuntime.Fatness
                                                : (Fatness)fatValue,
                                  Breed = hasWindcallerNpcRuntime
                                              ? (Breed)windcallerNpcRuntime.Content.Breed
                                              : hasCapturedVendorRuntime
                                              ? (Breed)capturedVendorRuntime.Content.Breed
                                              : hasEncounterRuntime
                                              ? (Breed)encounterRuntime.Breed
                                              : (Breed)breedValue,
                                  Gender = hasWindcallerNpcRuntime
                                               ? (Gender)windcallerNpcRuntime.Content.Sex
                                               : hasCapturedVendorRuntime
                                               ? (Gender)capturedVendorRuntime.Content.Sex
                                               : hasEncounterRuntime
                                               ? (Gender)encounterRuntime.Sex
                                               : (Gender)sexValue,
                                  Race = hasWindcallerNpcRuntime
                                             ? (uint)windcallerNpcRuntime.Content.Race
                                             : hasCapturedVendorRuntime
                                             ? (uint)capturedVendorRuntime.Content.Race
                                             : hasEncounterRuntime
                                             ? (uint)encounterRuntime.Race
                                             : raceValue
                              }; // appearance

            if (hasWindcallerNpcRuntime)
            {
                scfu.Appearance.Value = (uint)windcallerNpcRuntime.Content.AppearanceValue;
            }
            else if (hasCapturedVendorRuntime)
            {
                scfu.Appearance.Value = (uint)capturedVendorRuntime.Content.AppearanceValue;
            }
            else if (hasEncounterRuntime)
            {
                scfu.Appearance.Value = encounterRuntime.AppearanceValue;
            }
            else if (hasOrdinaryRuntime
                && ordinaryRuntime.Profile.Appearance.ScfuProfile
                == OrdinaryEnemyScfuProfile.CapturedThief)
            {
                scfu.Appearance.Value = ordinaryRuntime.Profile.Appearance.AppearanceValue;
            }

            // Name
            scfu.Name = charName;

            scfu.CharacterFlags = (CharacterFlags)charFlagsValue; // Flags
            scfu.AccountFlags = (short)accFlagsValue;
            scfu.Expansions = (short)expansionValue;

            // Capture-backed city NPCs (e.g. ICC Peacekeepers) can have NpcFamily=0 while still
            // using SimpleNpcInfo. Prefer NPCController over the family!=0 heuristic.
            bool isNpc = hasWindcallerNpcRuntime
                         || hasCapturedVendorRuntime
                         || character.Controller is NPCController
                         || ((NPCFamily != 1234567890) && (NPCFamily != 0));

            if (isNpc)
            {
                var snpc = new SimpleNpcInfo { Family = (short)NPCFamily, LosHeight = (short)losHeight };
                scfu.CharacterInfo = snpc;
            }
            else
            {
                // Are we a player?
                var spc = new SimplePcInfo();

                spc.CurrentNano = (uint)currentNano; // CurrentNano
                spc.Team = 0; // team?
                spc.Swim = 5; // swim?

                // The checks here are to prevent the client doing weird things if the character has really large or small base attributes
                spc.StrengthBase = (short)Math.Min(strengthBaseValue, short.MaxValue); // Strength
                spc.AgilityBase = (short)Math.Min(agilityBaseValue, short.MaxValue); // Agility
                spc.StaminaBase = (short)Math.Min(staminaBaseValue, short.MaxValue); // Stamina
                spc.IntelligenceBase = (short)Math.Min(intelligenceBaseValue, short.MaxValue); // Intelligence
                spc.SenseBase = (short)Math.Min(senseBaseValue, short.MaxValue); // Sense
                spc.PsychicBase = (short)Math.Min(psychicBaseValue, short.MaxValue); // Psychic

                if (scfu.CharacterFlags.HasFlag(CharacterFlags.HasVisibleName))
                {
                    // has visible names? (Flags)
                    spc.FirstName = firstName;
                    spc.LastName = lastName;
                }

                if (orgNameLength != 0)
                {
                    spc.OrgName = orgName;
                }

                scfu.CharacterInfo = spc;
            }

            // Level
            scfu.Level = (short)levelValue;

            // Health — values above ushort.MaxValue use int32 SCFU encoding and the client
            // shows the large boss HP bar. Keep server combat HP; clamp presentation only.
            int displayMaxHealth = healthValue;
            int displayCurrentHealth = currentHealth;
            if (healthValue > ushort.MaxValue)
            {
                displayMaxHealth = ushort.MaxValue;
                if (healthValue > 0)
                {
                    displayCurrentHealth = (int)((long)currentHealth * ushort.MaxValue / healthValue);
                    if (displayCurrentHealth < 0)
                    {
                        displayCurrentHealth = 0;
                    }
                    else if (displayCurrentHealth > displayMaxHealth)
                    {
                        displayCurrentHealth = displayMaxHealth;
                    }
                }
                else
                {
                    displayCurrentHealth = 0;
                }
            }

            scfu.Health = displayMaxHealth;
            scfu.HealthDamage = displayMaxHealth - displayCurrentHealth;

            // If player is in grid or fixer grid
            // make him/her/it a nice upside down pyramid
            if ((charPlayfield == 152) || (charPlayfield == 4107))
            {
                scfu.MonsterData = 99902;
            }
            else
            {
                scfu.MonsterData = (uint)monsterData; // Monsterdata
            }

            scfu.MonsterScale = (short)monsterScale; // Monsterscale
            scfu.VisualFlags = (short)visualFlags; // VisualFlags
            scfu.VisibleTitle = 0; // visible title?

            // 42 bytes long
            // For PlayerCharacters that is
            // NPC's have a shorter one?
            scfu.Unknown1 = new byte[]
                            {
                                0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00,
                                (byte)currentMovementMode, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00,
                                0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                0x00, 0x00, 0x00, 0x00
                            };

            // NPC Unknown1 (28-byte). Family=0 ICC NPCs still need this shape for idle anims.
            if (isNpc)
            {
                scfu.Unknown1 = new byte[]
                                {
                                    // Knubot values??            
                                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                    (byte)currentMovementMode, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00,
                                    0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00
                                };
            }

            if (hasOrdinaryRuntime
                && ordinaryRuntime.Profile.Appearance.ScfuProfile
                == OrdinaryEnemyScfuProfile.CapturedThief)
            {
                scfu.Unknown1 = CapturedSubwayThiefUnknown1;
                scfu.AdditionalFlags = SimpleCharFullUpdateFlags.UnknownFlag6 | SimpleCharFullUpdateFlags.IsPet;
                scfu.SuppressedFlags = SimpleCharFullUpdateFlags.UnknownFlag2;
            }
            else if (petMasterInstance != 0)
            {
                scfu.AdditionalFlags = SimpleCharFullUpdateFlags.UnknownFlag6
                    | SimpleCharFullUpdateFlags.IsPet
                    | SimpleCharFullUpdateFlags.UnknownDataFlag;
            }
            else if (character.Controller is NPCController
                     && ThrakOmniGardenSpawn.NeedsCapturedScfuIsPet(charName))
            {
                // Capture 20260821-225658: Hypnagogic Urga-Lum Thrak SCFU Flags include IsPet.
                scfu.AdditionalFlags = SimpleCharFullUpdateFlags.UnknownFlag6
                    | SimpleCharFullUpdateFlags.IsPet;
                scfu.SuppressedFlags = SimpleCharFullUpdateFlags.UnknownFlag2;
            }
            else if (!hasWindcallerNpcRuntime
                     && !hasCapturedVendorRuntime
                     && !hasEncounterRuntime
                     && character.Controller is NPCController
                     && AndromedaIccHqSpawn.IsAndromedaCityNpcPlayfield(charPlayfield))
            {
                // Live ICC HQ SCFU: UnknownFlag6 + IsPet; serializer's UnknownFlag2 is not on capture.
                scfu.AdditionalFlags = SimpleCharFullUpdateFlags.UnknownFlag6
                    | SimpleCharFullUpdateFlags.IsPet;
                scfu.SuppressedFlags = SimpleCharFullUpdateFlags.UnknownFlag2;
                if (AndromedaIccHqSpawn.NeedsNataliaScfuFlag7(charName))
                {
                    scfu.AdditionalFlags |= SimpleCharFullUpdateFlags.UnknownFlag7;
                }

                byte[] extendedTextures;
                if (AndromedaIccHqSpawn.TryGetExtendedTextureOverride(charName, out extendedTextures))
                {
                    scfu.ExtendedTextureOverrideData = extendedTextures;
                }
            }

            int emittedHeadMesh = hasWindcallerNpcRuntime
                                      ? windcallerNpcRuntime.Content.HeadMesh
                                      : hasCapturedVendorRuntime
                                      ? capturedVendorRuntime.Content.HeadMesh
                                      : hasEncounterRuntime
                                      ? encounterRuntime.HeadMesh
                                      : headMeshValue;
            if (emittedHeadMesh != 0)
            {
                scfu.HeadMesh = (uint?)emittedHeadMesh; // Headmesh
            }

            // Runspeed
            scfu.RunSpeedBase = hasWindcallerNpcRuntime
                                    ? (short)windcallerNpcRuntime.Content.RunSpeed
                                    : hasCapturedVendorRuntime
                                    ? (short)capturedVendorRuntime.Content.RunSpeed
                                    : hasEncounterRuntime
                                    ? (short)encounterRuntime.CapturedScfuRunSpeedBase
                                    : (short)runSpeedBaseValue;

            if (hasOrdinaryRuntime
                && ordinaryRuntime.Profile.Appearance.ScfuProfile
                == OrdinaryEnemyScfuProfile.CapturedFilthFlea)
            {
                scfu.ExtendedTextureOverrideData = CapturedSubwayFilthFleaExtendedTextureOverrideData;
            }
            else
            {
                byte[] alexExtendedTextures;
                if (AlexAreaMobRuntime.TryGetExtendedTextureOverride(charName, out alexExtendedTextures))
                {
                    // Capture 20260720-204431: Docker / Waste Collector / Garbage Flea HasExtendedTextures.
                    scfu.ExtendedTextureOverrideData = alexExtendedTextures;
                    byte[] alexUnknown1;
                    if (AlexAreaMobRuntime.TryGetCapturedScfuUnknown1(charName, out alexUnknown1))
                    {
                        scfu.Unknown1 = alexUnknown1;
                    }
                }
                else if (LoreleiOasisMobRuntime.TryGetExtendedTextureOverride(charName, out alexExtendedTextures))
                {
                    // Capture 20260721-loralei SCFU flags include UnknownFlag6|IsPet|UnknownFlag7 + ExtTex.
                    scfu.ExtendedTextureOverrideData = alexExtendedTextures;
                    scfu.AdditionalFlags = SimpleCharFullUpdateFlags.UnknownFlag6
                        | SimpleCharFullUpdateFlags.IsPet
                        | SimpleCharFullUpdateFlags.UnknownFlag7;
                    scfu.SuppressedFlags = SimpleCharFullUpdateFlags.UnknownFlag2;
                    byte[] oasisUnknown1;
                    if (LoreleiOasisMobRuntime.TryGetCapturedScfuUnknown1(charName, out oasisUnknown1))
                    {
                        scfu.Unknown1 = oasisUnknown1;
                    }
                }
                else if (NascenceLifeSpawn.TryGetExtendedTextureOverride(charName, out alexExtendedTextures))
                {
                    // Capture 20260723-221330 Barking Chimera / Yuttos Nascence Geosurvey Dog ExtTex.
                    scfu.ExtendedTextureOverrideData = alexExtendedTextures;
                }
                else if (petMasterInstance != 0
                    && ZoneEngine.Core.PetBureaucratGuardianAppearance.IsGuardianPet(character))
                {
                    // Owner receives the capture-exact guardian wire; other players receive this
                    // serializer-built visibility SCFU. Attach the guardian body textures so both match.
                    scfu.ExtendedTextureOverrideData =
                        ZoneEngine.Core.PetSummonScfuExtensions.CloneGuardianExtendedTextureOverrideData();
                    scfu.VisualFlags = 31;
                }
                else
                {
                    byte[] bureaucratColorTextures;
                    if (petMasterInstance != 0
                        && ZoneEngine.Core.PetSummonScfuExtensions
                            .TryGetBureaucratAttackPetExtendedTextureOverride(
                                charName,
                                out bureaucratColorTextures))
                    {
                        // Capture 20260806-crat-pets: Material #468 shell-matching color on visibility SCFU.
                        scfu.ExtendedTextureOverrideData = bureaucratColorTextures;
                        scfu.VisualFlags = 31;
                    }
                }
            }

            scfu.ActiveNanos = (from nano in nanos
                select
                    new ActiveNano
                    {
                        NanoIdentity =
                            new Identity
                            {
                                Type = IdentityType.NanoProgram,
                                Instance = nano.ID
                            },
                        NanoInstance = nano.Instance,
                        Time1 = nano.TickCounter,
                        Time2 = nano.TickInterval
                    }).ToArray();

            if (character.Waypoints != null && character.Waypoints.Count > 1)
            {
                scfu.Waypoints = (from waypoint in character.Waypoints
                    select
                        new Vector3
                            {
                                X = (float)waypoint.Position.x,
                                Y = (float)waypoint.Position.y,
                                Z = (float)waypoint.Position.z
                            }).ToArray();
            }

            // Texture/Cloth Data
            var scfuTextures = new List<Texture>();

            var aotemp = new AOTextures(0, 0);
            for (int c = 0; c < 5; c++)
            {
                aotemp.Texture = 0;
                aotemp.place = c;
                for (int c2 = 0; c2 < texturesCount; c2++)
                {
                    if (textures[c2].place != c)
                    {
                        continue;
                    }

                    aotemp.Texture = textures[c2].Texture;
                    break;
                }

                if (showsocial)
                {
                    if (socialonly)
                    {
                        aotemp.Texture = socialTab[c];
                    }
                    else
                    {
                        if (socialTab[c] != 0)
                        {
                            aotemp.Texture = socialTab[c];
                        }
                    }
                }

                scfuTextures.Add(new Texture { Place = aotemp.place, Id = aotemp.Texture, Unknown = 0 });
            }

            scfu.Textures = scfuTextures.ToArray();

            // End Textures

            // ############
            // # Meshs
            // ############
            scfu.Meshes = (from aoMesh in meshs
                select
                    new Mesh
                    {
                        Position = (byte)aoMesh.Position,
                        Id = (uint)aoMesh.Mesh,
                        OverrideTextureId = aoMesh.OverrideTexture,
                        Layer = (byte)aoMesh.Layer
                    }).ToArray();

            // End Meshs
            scfu.Flags2 = 0; // packetFlags2
            scfu.Unknown2 = 0;

            if (hasEncounterRuntime)
            {
                var capturedFlags = (SimpleCharFullUpdateFlags)encounterRuntime.CapturedScfuFlags;
                var capturedNpcInfo = scfu.CharacterInfo as SimpleNpcInfo;
                if (capturedNpcInfo != null)
                {
                    capturedNpcInfo.Family = (short)encounterRuntime.NpcFamily;
                    capturedNpcInfo.LosHeight = (short)encounterRuntime.NpcLosHeight;
                    capturedNpcInfo.UnknownData = (byte)encounterRuntime.CapturedScfuNpcUnknownData;
                }
                scfu.AdditionalFlags = capturedFlags;
                scfu.SuppressedFlags = ~capturedFlags;
                scfu.Flags2 = (byte)encounterRuntime.CapturedScfuFlags2;
                scfu.Unknown1 = encounterRuntime.CapturedScfuUnknown1.ToArray();
                scfu.Unknown2 = (byte)encounterRuntime.CapturedScfuUnknown2;
                scfu.Textures =
                    encounterRuntime.Textures.Select(
                        texture =>
                            new Texture
                            {
                                Place = texture.Place,
                                Id = texture.Id,
                                Unknown = texture.Unknown
                            }).ToArray();
                scfu.Meshes =
                    encounterRuntime.Meshes.Select(
                        mesh =>
                            new Mesh
                            {
                                Position = (byte)mesh.Position,
                                Id = mesh.Id,
                                OverrideTextureId = mesh.OverrideTextureId,
                                Layer = (byte)mesh.Layer
                            }).ToArray();
                scfu.Waypoints =
                    encounterRuntime.Waypoints.Select(
                        waypoint =>
                            new Vector3
                            {
                                X = waypoint.X,
                                Y = waypoint.Y,
                                Z = waypoint.Z
                            }).ToArray();
            }
            else if (hasWindcallerNpcRuntime)
            {
                WindcallerKarrecNpcDefinition definition = windcallerNpcRuntime.Content;
                // Do not force exact capture flags via SuppressedFlags=~flags.
                // That clears serializer-owned size bits (e.g. HasExtendedLevel for L200)
                // after Int16 level was already written, which desyncs/crashes the client.
                // Mirror subway thief/vendor style: only OR cosmetic bits; let the serializer
                // own health/level/runspeed/waypoint/npc-family sizing flags.
                scfu.CharacterInfo =
                    new SimpleNpcInfo
                    {
                        Family = (short)definition.NpcFamily,
                        LosHeight = (short)definition.NpcLosHeight
                    };
                scfu.CharacterFlags = (CharacterFlags)definition.CharacterFlags;
                scfu.AccountFlags = 0;
                scfu.Expansions = 0;
                scfu.AdditionalFlags = SimpleCharFullUpdateFlags.UnknownFlag6;
                scfu.SuppressedFlags = SimpleCharFullUpdateFlags.None;
                scfu.VisualFlags = (short)definition.VisualFlags;
                scfu.Flags2 = 0;
                scfu.Unknown1 = definition.CapturedScfuUnknown1.ToArray();
                scfu.Unknown2 = 0;
                scfu.VisibleTitle = (byte)definition.VisibleTitle;
                scfu.ActiveNanos =
                    definition.ActiveNanos.Select(
                        nano =>
                            new ActiveNano
                            {
                                NanoIdentity =
                                    new Identity
                                    {
                                        Type = (IdentityType)nano.NanoIdentityType,
                                        Instance = nano.NanoIdentityInstance
                                    },
                                NanoInstance = nano.NanoInstance,
                                Time1 = nano.Time1,
                                Time2 = nano.Time2
                            }).ToArray();
                scfu.Textures =
                    definition.Textures.Select(
                        texture =>
                            new Texture
                            {
                                Place = texture.Place,
                                Id = texture.Id,
                                Unknown = texture.Unknown
                            }).ToArray();
                scfu.Meshes =
                    definition.Meshes.Select(
                        mesh =>
                            new Mesh
                            {
                                Position = (byte)mesh.Position,
                                Id = mesh.Id,
                                OverrideTextureId = mesh.OverrideTextureId,
                                Layer = (byte)mesh.Layer
                            }).ToArray();
                var activePatrolCurrentPosition = new AORebirth.Core.Vector.Vector3();
                var activePatrolDestination = new AORebirth.Core.Vector.Vector3();
                NPCController npcController = character.Controller as NPCController;
                bool hasActivePatrolDestination =
                    npcController != null
                    && npcController.TryGetCapturedPatrolReplayProjection(
                        out activePatrolCurrentPosition,
                        out activePatrolDestination);

                WindcallerKarrecNpcWaypointDefinition resolvedCoordinates =
                    definition.ResolveScfuCoordinates(
                        hasActivePatrolDestination,
                        scfu.Coordinates.X,
                        scfu.Coordinates.Y,
                        scfu.Coordinates.Z,
                        activePatrolCurrentPosition.xf,
                        activePatrolCurrentPosition.yf,
                        activePatrolCurrentPosition.zf);
                scfu.Coordinates =
                    new Vector3
                    {
                        X = resolvedCoordinates.X,
                        Y = resolvedCoordinates.Y,
                        Z = resolvedCoordinates.Z
                    };

                scfu.Waypoints =
                    definition.ResolveScfuWaypoints(
                        hasActivePatrolDestination,
                        activePatrolCurrentPosition.xf,
                        activePatrolCurrentPosition.yf,
                        activePatrolCurrentPosition.zf,
                        activePatrolDestination.xf,
                        activePatrolDestination.yf,
                        activePatrolDestination.zf).Select(
                        waypoint =>
                            new Vector3
                            {
                                X = waypoint.X,
                                Y = waypoint.Y,
                                Z = waypoint.Z
                            }).ToArray();
            }
            else if (hasCapturedVendorRuntime)
            {
                CapturedSubwayVendorDefinition definition = capturedVendorRuntime.Content;
                var capturedFlags = (SimpleCharFullUpdateFlags)definition.CapturedScfuFlags;
                scfu.CharacterInfo =
                    new SimpleNpcInfo
                    {
                        Family = 0,
                        LosHeight = 0
                    };
                scfu.AdditionalFlags = capturedFlags;
                scfu.SuppressedFlags = ~capturedFlags;
                scfu.Flags2 = 0;
                scfu.Unknown1 = definition.CapturedScfuUnknown1.ToArray();
                scfu.Unknown2 = 0;
                scfu.VisibleTitle = 0;
                scfu.Textures =
                    definition.Textures.Select(
                        texture =>
                            new Texture
                            {
                                Place = texture.Place,
                                Id = texture.Id,
                                Unknown = texture.Unknown
                            }).ToArray();
                scfu.Meshes =
                    definition.Meshes.Select(
                        mesh =>
                            new Mesh
                            {
                                Position = (byte)mesh.Position,
                                Id = mesh.Id,
                                OverrideTextureId = mesh.OverrideTextureId,
                                Layer = (byte)mesh.Layer
                            }).ToArray();
                scfu.Waypoints =
                    definition.Waypoints.Select(
                        waypoint =>
                            new Vector3
                            {
                                X = waypoint.X,
                                Y = waypoint.Y,
                                Z = waypoint.Z
                            }).ToArray();
            }
            else if (hasOrdinaryRuntime && ordinaryRuntime.Spawn.HasCapturedScfuOverride)
            {
                OrdinaryEnemySpawnDefinition spawn = ordinaryRuntime.Spawn;
                OrdinaryEnemyAppearanceProfile appearance = ordinaryRuntime.Profile.Appearance;
                var capturedFlags = (SimpleCharFullUpdateFlags)spawn.CapturedScfuFlags;
                scfu.AdditionalFlags = capturedFlags;
                scfu.SuppressedFlags = ~capturedFlags;
                scfu.Flags2 = (byte)spawn.CapturedScfuFlags2;
                scfu.Unknown1 = spawn.CapturedScfuUnknown1.ToArray();
                scfu.Unknown2 = (byte)spawn.CapturedScfuUnknown2;
                scfu.VisibleTitle = (byte)appearance.VisibleTitle;
                scfu.Textures =
                    appearance.Textures.Select(
                        texture =>
                            new Texture
                            {
                                Place = texture.Place,
                                Id = texture.Id,
                                Unknown = texture.Unknown
                            }).ToArray();
                scfu.Meshes =
                    appearance.Meshes.Select(
                        mesh =>
                            new Mesh
                            {
                                Position = (byte)mesh.Position,
                                Id = mesh.Id,
                                OverrideTextureId = mesh.OverrideTextureId,
                                Layer = (byte)mesh.Layer
                            }).ToArray();
                scfu.Waypoints = new Vector3[0];
            }

            return scfu;
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <returns>
        /// </returns>
        public static SimpleCharFullUpdateMessage ConstructMessage(IZoneClient client)
        {
            return ConstructMessage((Character)client.Controller.Character);
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="receiver">
        /// </param>
        public static void SendToOne(ICharacter character, IZoneClient receiver)
        {
            SimpleCharFullUpdateMessage message = ConstructMessage((Character)character);
            receiver.Controller.Character.Send(message);
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        public static void SendToPlayfield(IZoneClient client)
        {
            SimpleCharFullUpdateMessage message = ConstructMessage(client);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                PlayfieldLifecycleTrace.StageJoiningCharacterSimpleCharFullUpdateBroadcast,
                PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate,
                client.Controller.Character.Identity);
            client.Controller.Character.Playfield.Announce(message);
        }

        #endregion
    }
}
