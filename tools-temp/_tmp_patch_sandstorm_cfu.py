# -*- coding: utf-8 -*-
from pathlib import Path

path = Path(r"AORebirth/Server/ZoneEngine/Core/Packets/CorpseFullUpdate.cs")
text = path.read_text(encoding="utf-8")
old = """            // Capture 20260727-204902: fixed 425-byte mech corpse + texture ids; name already correct.
            byte[] buffer = (byte[])CapturedAreteSandstormMarauderTemplate.Clone();
            if (buffer.Length != CapturedAreteSandstormMarauderPacketLength)
            {
                throw new InvalidOperationException(
                    \"Captured Arete SANDSTORM Marauder corpse template length changed.\");
            }

            WritePacketLength(buffer, buffer.Length);
            WriteInt32(buffer, ServerIdOffset, serverId);
            WriteInt32(buffer, ReceiverInstanceOffset, receiver.Instance);
            WriteInt32(buffer, CorpseInstanceOffset, corpseIdentity.Instance);
            WriteSingle(buffer, PositionXOffset, deadNpc.RawCoordinates.X);
            WriteSingle(buffer, PositionYOffset, deadNpc.RawCoordinates.Y);
            WriteSingle(buffer, PositionZOffset, deadNpc.RawCoordinates.Z);
            WriteInt32(buffer, PlayfieldIdOffset, deadNpc.Playfield.Identity.Instance);
            WriteInt32(buffer, MonsterScaleOffset, deadNpc.Stats[StatIds.monsterscale].Value);
            WriteInt32(buffer, SexOffset, deadNpc.Stats[StatIds.sex].Value);
            WriteInt32(buffer, BreedOffset, deadNpc.Stats[StatIds.breed].Value);
            WriteInt32(buffer, RaceOffset, deadNpc.Stats[StatIds.race].Value);
            WriteInt32(buffer, DeadNpcInstanceOffset, deadNpc.Identity.Instance);
            WriteInt32(
                buffer,
                CorpseCatMeshOffset,
                corpseCatMesh > 0 ? corpseCatMesh : CapturedAreteSandstormMarauderDefaultCatMesh);
            WriteInt32(buffer, CorpseCashValueOffset, Math.Max(0, corpseCredits));
"""
new = """            // Capture 20260727-204902: fixed 425-byte mech corpse + texture ids.
            // Never write CorpseCatMeshFor()'s MonsterData fallback (265822) - MD-as-CATMesh
            // crashes the client. Keep capture CATMesh 265819. Keep capture scale/sex/breed/race.
            byte[] buffer = (byte[])CapturedAreteSandstormMarauderTemplate.Clone();
            if (buffer.Length != CapturedAreteSandstormMarauderPacketLength)
            {
                throw new InvalidOperationException(
                    \"Captured Arete SANDSTORM Marauder corpse template length changed.\");
            }

            WritePacketLength(buffer, buffer.Length);
            WriteInt32(buffer, ServerIdOffset, serverId);
            WriteInt32(buffer, ReceiverInstanceOffset, receiver.Instance);
            WriteInt32(buffer, CorpseInstanceOffset, corpseIdentity.Instance);
            WriteSingle(buffer, PositionXOffset, deadNpc.RawCoordinates.X);
            WriteSingle(buffer, PositionYOffset, deadNpc.RawCoordinates.Y);
            WriteSingle(buffer, PositionZOffset, deadNpc.RawCoordinates.Z);
            WriteInt32(buffer, PlayfieldIdOffset, deadNpc.Playfield.Identity.Instance);
            WriteInt32(buffer, DeadNpcInstanceOffset, deadNpc.Identity.Instance);
            WriteInt32(buffer, CorpseCatMeshOffset, CapturedAreteSandstormMarauderDefaultCatMesh);
            WriteInt32(buffer, CorpseCashValueOffset, Math.Max(0, corpseCredits));
"""
# fix escaped quotes in old/new for actual file content
old = old.replace('\\"', '"')
new = new.replace('\\"', '"')
if old not in text:
    raise SystemExit("old block not found")
path.write_text(text.replace(old, new, 1), encoding="utf-8")
print("patched ok")
