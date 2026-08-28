import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
MAIN = (ROOT / "tools-temp" / "AOSharpLiveCapture" / "Main.cs").read_text(
    encoding="utf-8"
)
BRIDGE = (
    ROOT / "tools-temp" / "AOSharpLiveCapture" / "NpcIdentityBridgeCapture.cs"
).read_text(encoding="utf-8")
PROJECT = (
    ROOT / "tools-temp" / "AOSharpLiveCapture" / "AOSharpLiveCapture.csproj"
).read_text(encoding="utf-8")
LAUNCHER = (ROOT / "tools-temp" / "start-aosharp-live-capture.cmd").read_text(
    encoding="utf-8"
)
REPLAY = (ROOT / "Tools" / "npc_identity_bridge_replay.py").read_text(
    encoding="utf-8"
)


class NpcIdentityBridgeCaptureContractTests(unittest.TestCase):
    def test_targeted_launcher_mode_is_explicit_consumed_and_mutually_exclusive(self):
        self.assertIn("--npc-identity-bridge", LAUNCHER)
        self.assertIn("npc-identity-bridge.request", LAUNCHER)
        self.assertIn(":cleanup_npc_identity_bridge_request", LAUNCHER)
        self.assertIn("npc-identity-bridge-live.jsonl", LAUNCHER)
        self.assertIn("CAPTURE_HAS_BRIDGE_ARTIFACT", LAUNCHER)
        self.assertIn("BRIDGE_REQUEST_CONSUMED", LAUNCHER)
        self.assertIn(
            "--loot-10 and --npc-identity-bridge are mutually exclusive", LAUNCHER
        )
        self.assertIn(
            "--pf127-geometry-only and --npc-identity-bridge are mutually exclusive",
            LAUNCHER,
        )

    def test_main_only_constructs_bridge_for_consumed_request(self):
        self.assertIn("NpcIdentityBridgeRequestFileName", MAIN)
        self.assertIn("this.npcIdentityBridgeRequested", MAIN)
        self.assertIn("new NpcIdentityBridgeCapture(", MAIN)
        self.assertIn("? new NpcIdentityBridgeCapture(", MAIN)
        self.assertIn("npc-identity-bridge-live.jsonl", BRIDGE)
        self.assertIn('Compile Include="NpcIdentityBridgeCapture.cs"', PROJECT)

    def test_zone_lifecycle_signals_are_wired_to_bridge(self):
        for callback in (
            "OnPlayfieldInit(",
            "OnTeleportStarted(",
            "OnTeleportEnded(",
            "OnTeleportFailed(",
        ):
            self.assertIn("capture." + callback, MAIN)
        self.assertIn("this.npcIdentityBridgeCapture?.Start(", MAIN)
        self.assertIn("this.npcIdentityBridgeCapture?.Complete(", MAIN)

    def test_epoch_bounds_and_identity_keys_are_structural(self):
        self.assertIn("priorEpoch.EndGlobalOrdinal.Value + 1", BRIDGE)
        self.assertIn("SelectStableEpochNoLock", BRIDGE)
        self.assertIn("globalOrdinal >= this.currentEpoch.StartGlobalOrdinal", BRIDGE)
        self.assertIn("this.transitionInProgress", BRIDGE)
        self.assertIn("EpochIdentityKey(", BRIDGE)
        self.assertIn("EvidenceAfterGlobalOrdinal", BRIDGE)
        self.assertIn("LastObservationGlobalOrdinal", BRIDGE)
        self.assertIn("long evidenceFloor = observationOrdinal;", BRIDGE)
        self.assertNotIn(": lineage.LastObservationGlobalOrdinal;", BRIDGE)
        self.assertIn('&& !epoch.EndGlobalOrdinal.HasValue\n', BRIDGE)
        self.assertGreaterEqual(BRIDGE.count("&& this.Epoch.EndGlobalOrdinal.HasValue"), 1)
        self.assertIn("zone_epoch_id", BRIDGE)
        self.assertIn("observation_global_ordinal", BRIDGE)

    def test_direct_world_identity_and_not_exposed_boundaries_are_explicit(self):
        self.assertIn("Playfield.Identity", BRIDGE)
        self.assertIn("Playfield.ModelIdentity", BRIDGE)
        self.assertIn("PlayfieldDistrictInfoType = 1000014", BRIDGE)
        self.assertIn("npc-specific-official-placement-identity-not-exposed", BRIDGE)
        self.assertIn('"template_id_direct"', BRIDGE)
        self.assertIn('"district_id_direct"', BRIDGE)
        self.assertIn("N3Dynel_t.GetZone", BRIDGE)
        self.assertIn("N3Zone_t.GetInstance", BRIDGE)

    def test_atomic_snapshot_preserves_spaces_orientation_stats_and_sentinel(self):
        for position_space in (
            '"world"',
            '"local"',
            '"district"',
            '"cell"',
            '"packet_scfu"',
        ):
            self.assertIn(position_space, BRIDGE)
        self.assertIn('"heading"', BRIDGE)
        self.assertIn('"orientation"', BRIDGE)
        self.assertIn('"packet_scfu_heading"', BRIDGE)
        self.assertIn('"packet_scfu_level"', BRIDGE)
        self.assertIn('"packet_scfu_breed_derived"', BRIDGE)
        self.assertIn('"packet_scfu_gender_derived"', BRIDGE)
        self.assertIn("character.GetStat(stat)", BRIDGE)
        self.assertIn("UnsetStatSentinel = 1234567890", BRIDGE)
        self.assertIn('record.Provenance = "sentinel/default"', BRIDGE)

    def test_packet_decode_eligibility_is_fail_closed_and_clears_old_evidence(self):
        self.assertGreaterEqual(
            BRIDGE.count("string.IsNullOrWhiteSpace(decodeError)"), 2
        )
        self.assertGreaterEqual(BRIDGE.count("message.DecodeFullyConsumed"), 2)
        self.assertIn("message.Npc != null", BRIDGE)
        self.assertGreaterEqual(
            BRIDGE.count("message.Identity.Type == (int)IdentityType.SimpleChar"), 2
        )
        self.assertIn("BridgeLinkEligible = epoch != null", BRIDGE)
        self.assertIn("BridgeLinkEligible = linkEligible", BRIDGE)
        self.assertIn('"bridge_link_eligible"', BRIDGE)
        self.assertGreaterEqual(
            BRIDGE.count("this.ClearCachedEvidenceNoLock(stableEpoch, null, null)"),
            2,
        )

    def test_scfu_link_requires_direct_stable_playfield_equality(self):
        self.assertIn(
            "epoch == null || message == null || !message.PlayfieldId.HasValue",
            BRIDGE,
        )
        self.assertIn(
            "message.PlayfieldId.Value != expectedRuntime",
            BRIDGE,
        )
        self.assertNotIn(
            "A pending epoch created by PlayfieldInit may receive initial",
            BRIDGE,
        )

    def test_lifecycle_boundaries_clear_cache_and_advance_same_pointer_lineage(self):
        self.assertGreaterEqual(
            BRIDGE.count("this.ObserveNpcLifecycleBoundary("), 2
        )
        self.assertIn('messageName == "Despawn"', BRIDGE)
        self.assertGreaterEqual(
            BRIDGE.count("this.BeginLifecycleBoundaryNoLock("), 2
        )
        self.assertIn("int nextLineage = prior == null ? 1 : prior.Ordinal + 1", BRIDGE)
        self.assertIn("long evidenceFloor = observationOrdinal", BRIDGE)
        self.assertGreaterEqual(
            BRIDGE.count("this.latestScfuByEpochIdentity.Remove(identityKey)"),
            2,
        )
        self.assertGreaterEqual(
            BRIDGE.count("this.latestStatByEpochIdentity.Remove(identityKey)"),
            2,
        )

    def test_bridge_callbacks_share_raw_packet_ordering_lock(self):
        helper_start = MAIN.index(
            "private void RunNpcIdentityBridgeOrdered"
        )
        helper_end = MAIN.index("private void OnCommandBoundary", helper_start)
        helper = MAIN[helper_start:helper_end]
        self.assertIn("lock (this.npcIdentityBridgeOrderingRoot)", helper)
        for callback in (
            "capture.OnPlayfieldInit(",
            "capture.OnTeleportStarted(",
            "capture.OnTeleportEnded(",
            "capture.OnTeleportFailed(",
        ):
            self.assertIn(callback, MAIN)
        self.assertIn("bridgeCapture.OnDynelSpawned(", MAIN)
        self.assertIn("bridgeCapture.OnCharInPlay(", MAIN)
        self.assertGreaterEqual(
            MAIN.count("lock (this.npcIdentityBridgeOrderingRoot)"), 4
        )
        raw_start = MAIN.index("private void CaptureNetworkPacketNoThrow")
        raw_end = MAIN.index("private void ReleaseRawPacketCallbackRegistration", raw_start)
        self.assertIn("lock (this.syncRoot)", MAIN[raw_start:raw_end])
        packet_start = MAIN.index("private void LogPacket(")
        packet_end = MAIN.index("private void LogPacketOrdered(", packet_start)
        self.assertIn(
            "lock (this.npcIdentityBridgeOrderingRoot)",
            MAIN[packet_start:packet_end],
        )

    def test_packet_heading_is_absent_when_scfu_flag_is_absent(self):
        self.assertGreaterEqual(BRIDGE.count("this.Message.Flags & 0x00000200"), 1)
        self.assertIn('AppendJsonNull(json, "heading", true)', BRIDGE)

    def test_raw_packet_linkage_uses_exact_ordering_key(self):
        self.assertIn("this.npcIdentityBridgeCapture?.ObserveRawPacket(", MAIN)
        self.assertIn(
            "this.npcIdentityBridgeCapture?.ObserveRawSimpleCharFullUpdate(", MAIN
        )
        self.assertIn("this.npcIdentityBridgeCapture?.ObserveRawStat(", MAIN)
        for field in ('"direction"', '"sequence"', '"global_ordinal"'):
            self.assertIn(field, BRIDGE)
        self.assertNotIn("timestamp fallback", REPLAY.lower())

    def test_bridge_component_has_no_client_mutation_or_packet_send_surface(self):
        forbidden = (
            "WriteProcessMemory",
            "Marshal.Write",
            "Network.Send",
            "Network.PacketSent +=",
            ".SetStat(",
            "ResourceDatabase.Set",
            "SendN3Message",
        )
        for token in forbidden:
            self.assertNotIn(token, BRIDGE)

    def test_full_bridge_state_vocabulary_and_acg_rejection_are_governed(self):
        combined = BRIDGE + REPLAY
        for state in (
            "direct-candidate",
            "partial",
            "not-exposed",
            "conflict",
            "invalid-epoch",
        ):
            self.assertIn(state, combined)
        self.assertIn("acg_hash_used_as_runtime_identity", REPLAY)
        self.assertIn("False", REPLAY)


if __name__ == "__main__":
    unittest.main()
