namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Collections.Concurrent;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Enums;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// Shadowlands garden XP/SK + bind save (no credit fee).
    /// Stand on the garden save pad → one save, then a 10s cooldown before it can save again.
    /// SocialStatus is NOT used — that status floods the client with empty chat lines.
    /// </summary>
    public static class ShadowlandsGardenSaveRuntimeService
    {
        private const int GardenPlayfieldMin = 4676;

        private const int GardenPlayfieldMax = 4699;

        /// <summary>XZ metres from the trigger rune required to save — small centre spot only.</summary>
        private const float PadRadius = 2.5f;

        // Save TRIGGER spot: the rune Mike marked in the middle of the garden pit.
        // Client "Pos: 462.4, 411.8, 47.6" = engine (X=462.4, Z=411.8, height=47.6).
        // Gardens are instanced copies of one layout, so this is the same rune in every garden.
        private const float TriggerX = 462.4f;

        private const float TriggerZ = 411.8f;

        // BIND / respawn pad: the safe garden entry pad (previously confirmed good for death respawn).
        // Kept separate from the trigger so death respawns on solid ground, not inside the pit.
        private const float BindX = 462.3f;

        private const float BindY = 45.4f;

        private const float BindZ = 422.2f;

        /// <summary>Garden bind/respawn pad (used by the death-respawn resolver).</summary>
        public static void GetGardenSaveSpot(out float x, out float y, out float z)
        {
            x = BindX;
            y = BindY;
            z = BindZ;
        }

        /// <summary>Minimum time between garden saves — one save per 30 seconds.</summary>
        private static readonly TimeSpan SaveCooldown = TimeSpan.FromSeconds(30);

        /// <summary>
        /// characterId → currently standing on the trigger rune. Edge-detected: a save only fires on
        /// the transition OFF-pad → ON-pad. While standing still (move heartbeats keep arriving) this
        /// stays true, so the save and its animation never re-fire — that was the "flood" cause.
        /// </summary>
        private static readonly ConcurrentDictionary<int, bool> OnPadByCharacterId =
            new ConcurrentDictionary<int, bool>();

        /// <summary>characterId → UTC time of last successful pad save (cooldown gate).</summary>
        private static readonly ConcurrentDictionary<int, DateTime> LastSaveUtcByCharacterId =
            new ConcurrentDictionary<int, DateTime>();

        public static bool IsGardenPlayfield(int playfieldId)
        {
            return playfieldId >= GardenPlayfieldMin && playfieldId <= GardenPlayfieldMax;
        }

        /// <summary>
        /// Call from CharDCMove only. Saves once when the character stands on the garden pad.
        /// </summary>
        public static void TryApplyWhenOnSavePad(ICharacter character, string reason)
        {
            try
            {
                if (character == null || character.Playfield == null)
                {
                    return;
                }

                int characterId = character.Identity.Instance;
                int playfieldId = character.Playfield.Identity.Instance;

                // playfieldId<=0 is a transient during move processing — do NOT touch the on-pad flag
                // (clearing it here previously let the save re-fire while standing = the flood).
                if (playfieldId <= 0)
                {
                    return;
                }

                if (!IsGardenPlayfield(playfieldId))
                {
                    bool ignored;
                    OnPadByCharacterId.TryRemove(characterId, out ignored);
                    return;
                }

                // Distance to the trigger rune (X/Z only).
                float dx = character.RawCoordinates.X - TriggerX;
                float dz = character.RawCoordinates.Z - TriggerZ;
                bool onPad = (dx * dx) + (dz * dz) <= (PadRadius * PadRadius);

                bool wasOnPad;
                OnPadByCharacterId.TryGetValue(characterId, out wasOnPad);

                if (!onPad)
                {
                    // Stepped off: re-arm so the next step-on can save again.
                    if (wasOnPad)
                    {
                        OnPadByCharacterId[characterId] = false;
                    }

                    return;
                }

                // On the rune. Only act on the OFF→ON edge; standing still must never re-fire.
                if (wasOnPad)
                {
                    return;
                }

                // Mark landed immediately (before any packets) so move heartbeats can't double-apply.
                OnPadByCharacterId[characterId] = true;

                // 30s cooldown between saves (even step-off/step-on respects it) → one animation / 30s.
                DateTime lastSaveUtc;
                if (LastSaveUtcByCharacterId.TryGetValue(characterId, out lastSaveUtc)
                    && (DateTime.UtcNow - lastSaveUtc) < SaveCooldown)
                {
                    return;
                }

                LastSaveUtcByCharacterId[characterId] = DateTime.UtcNow;

                SaveRespawnPoint(character, playfieldId);

                uint savedSk;
                uint savedXp = CombatXpRuntimeService.ApplyInsuranceTerminalSave(character, out savedSk);

                int level = character.Stats[StatIds.level].Value;

                // Level 201+ (Shadowlevels): earn SK, not XP. At 220 (max) they earn neither, so no
                // number is shown. Shared with the insurance terminal save (savechar.cs).
                string storedText = CombatXpRuntimeService.BuildSaveRewardText(level, savedXp, savedSk);

                ChatTextMessageHandler.Default.Send(character, storedText);
                ChatTextMessageHandler.Default.Send(character, "Character saved");

                IZoneClient client = character.Controller != null ? character.Controller.Client : null;
                if (client != null && client.Server != null)
                {
                    client.Server.Info(
                        client,
                        "Shadowlands garden pad-save char={0} pf={1} xp={2} sk={3} reason={4}",
                        character.Identity,
                        playfieldId,
                        savedXp,
                        savedSk,
                        reason ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Debug(DebugInfoDetail.Error, "Shadowlands garden save FAILED: " + ex);
                try
                {
                    if (character != null
                        && character.Controller != null
                        && character.Controller.Client != null
                        && character.Controller.Client.Server != null)
                    {
                        character.Controller.Client.Server.Info(
                            character.Controller.Client,
                            "Shadowlands garden save FAILED reason={0} ex={1}",
                            reason ?? string.Empty,
                            ex.Message);
                    }
                }
                catch
                {
                }
            }
        }

        private static void SaveRespawnPoint(ICharacter character, int playfieldId)
        {
            int saveX = (int)Math.Round(BindX);
            int saveZ = (int)Math.Round(BindZ);

            character.Stats[StatIds.tempsaveplayfield].Set((uint)Math.Max(0, playfieldId));
            character.Stats[StatIds.tempsavex].Set((uint)Math.Max(0, saveX));
            character.Stats[StatIds.tempsavey].Set((uint)Math.Max(0, saveZ));
            character.Stats[StatIds.insurancepercentage].Set(100);
            character.Stats[StatIds.insurancetime].Set((uint)Math.Max(0, Environment.TickCount));
            character.Stats.Write();

            // Clear Changed flags so per-frame CharDCMove stat flushes do not re-push these bind
            // stats every frame — that re-flush interrupted/restarted the character animation on
            // the pad. DB write above already persisted them.
            character.Stats[StatIds.tempsaveplayfield].Changed = false;
            character.Stats[StatIds.tempsavex].Changed = false;
            character.Stats[StatIds.tempsavey].Changed = false;
            character.Stats[StatIds.insurancepercentage].Changed = false;
            character.Stats[StatIds.insurancetime].Changed = false;
        }
    }
}
