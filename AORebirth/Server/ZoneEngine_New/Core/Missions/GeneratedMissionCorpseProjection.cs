namespace ZoneEngine_New.Core.Missions;

using System;
using System.Buffers.Binary;
using System.Linq;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Missions;
using ZoneEngine.Core.Packets;

/// <summary>The accepted generated corpse consumer, never New's generic loot or Biofreak projection.</summary>
public static class GeneratedMissionCorpseProjection
{
    public static byte[] BuildPacket(GeneratedMissionNpcEvidence evidence, GeneratedMissionObject state, int livePlayfield, Identity receiver)
    {
        if (!state.IsDead || state.CurrentHealth != 0 || state.CorpseCredits < 0
            || state.RuntimeInstance != evidence.RuntimeInstance || state.RuntimeType != evidence.RuntimeType
            || state.CapturedInstance != evidence.CapturedInstance || state.CapturedType != evidence.CapturedType
            || state.RuntimeType != (int)IdentityType.CanbeAffected || evidence.IsFindPerson
            || livePlayfield < GeneratedMissionIdentitySpace.MinimumLivePlayfield2
            || livePlayfield > GeneratedMissionIdentitySpace.MaximumLivePlayfield2
            || receiver.Type != IdentityType.CanbeAffected || receiver.Instance <= 0)
            throw new InvalidOperationException("Corpse projection requires the exact durable generated NPC death.");
        var source = evidence.CopySpawnMessage();
        if (!source.TailFullyDecoded || source.UndecodedTail?.Length > 0)
            throw new InvalidOperationException("Corpse source appearance is not fully decoded.");
        int catMesh = GeneratedMissionCorpseWire.MissionCatMeshMappings()
            .Where(pair => pair.Key == evidence.MonsterData).Select(pair => pair.Value).SingleOrDefault();
        var appearance = MissionCorpseContent.Current;
        byte[] wire = GeneratedMissionCorpseWire.Build(evidence.Name, state.RuntimeInstance, state.RuntimeInstance,
            receiver.Instance, livePlayfield, state.X, state.Y, state.Z, livePlayfield,
            source.MonsterScale, appearance.Sex, appearance.Breed, appearance.Race, catMesh, evidence.MonsterData,
            state.CorpseClaimed ? 0 : state.CorpseCredits);
        // The shared typed CFU decoder cannot represent this accepted name/material tail.
        // Preserve the body exactly; replace only the ordinary current transport marker.
        BinaryPrimitives.WriteUInt16BigEndian(wire.AsSpan(0, 2), 0xDFDF);
        return wire;
    }
}
