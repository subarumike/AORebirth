// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SimpleCharFullUpdateSerializer.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the SimpleCharFullUpdateSerializer type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    // TODO: Check the client side of this message for the possibly missing parts.
    public class SimpleCharFullUpdateSerializer : ISerializer
    {
        #region Fields

        private readonly Type type;

        #endregion

        #region Constructors and Destructors

        public SimpleCharFullUpdateSerializer()
        {
            this.type = typeof(SimpleCharFullUpdateMessage);
        }

        #endregion

        #region Public Properties

        public Type Type
        {
            get
            {
                return this.type;
            }
        }

        #endregion

        #region Public Methods and Operators

        public object Deserialize(
            StreamReader streamReader,
            SerializationContext serializationContext,
            PropertyMetaData propertyMetaData = null)
        {
            long bodyStart = streamReader.Position;
            byte[] rawBody = streamReader.ReadBytes((int)(streamReader.Length - bodyStart));
            streamReader.Position = bodyStart;

            var message = new SimpleCharFullUpdateMessage
            {
                RawBody = rawBody,
                N3MessageType = (N3MessageType)streamReader.ReadInt32(),
                Identity = streamReader.ReadIdentity(),
                Unknown = streamReader.ReadByte(),
                Version = streamReader.ReadByte(),
                Flags = (SimpleCharFullUpdateFlags)streamReader.ReadInt32()
            };

            SimpleCharFullUpdateFlags flags = message.Flags;
            if (flags.HasFlag(SimpleCharFullUpdateFlags.HasPlayfieldId))
            {
                message.PlayfieldId = streamReader.ReadInt32();
            }

            if (flags.HasFlag(SimpleCharFullUpdateFlags.HasFightingTarget))
            {
                message.FightingTarget = streamReader.ReadIdentity();
            }

            message.Coordinates = ReadVector3(streamReader);
            if (flags.HasFlag(SimpleCharFullUpdateFlags.HasHeading))
            {
                message.Heading = new Quaternion
                {
                    X = streamReader.ReadSingle(),
                    Y = streamReader.ReadSingle(),
                    Z = streamReader.ReadSingle(),
                    W = streamReader.ReadSingle()
                };
            }

            message.Appearance = new Appearance { Value = streamReader.ReadUInt32() };
            message.Name = streamReader.ReadString(streamReader.ReadByte());
            message.CharacterFlags = (CharacterFlags)streamReader.ReadInt32();
            message.AccountFlags = streamReader.ReadInt16();
            message.Expansions = streamReader.ReadInt16();

            if (flags.HasFlag(SimpleCharFullUpdateFlags.IsNpc))
            {
                var npc = new SimpleNpcInfo
                {
                    Family = flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallNpcFamily)
                                 ? streamReader.ReadByte()
                                 : streamReader.ReadInt16(),
                    LosHeight = flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallNpcLosHeight)
                                    ? streamReader.ReadByte()
                                    : streamReader.ReadInt16(),
                    UnknownData = flags.HasFlag(SimpleCharFullUpdateFlags.UnknownDataFlag)
                                      ? streamReader.ReadByte()
                                      : streamReader.ReadInt16(),
                    UnknownData2 = streamReader.ReadInt16()
                };
                if (npc.UnknownData2 > 0)
                {
                    npc.UnknownData3 = streamReader.ReadByte();
                }

                message.CharacterInfo = npc;
            }
            else
            {
                var pc = new SimplePcInfo
                {
                    CurrentNano = streamReader.ReadUInt32(),
                    Team = streamReader.ReadInt32(),
                    Swim = streamReader.ReadInt16(),
                    StrengthBase = streamReader.ReadInt16(),
                    AgilityBase = streamReader.ReadInt16(),
                    StaminaBase = streamReader.ReadInt16(),
                    IntelligenceBase = streamReader.ReadInt16(),
                    SenseBase = streamReader.ReadInt16(),
                    PsychicBase = streamReader.ReadInt16(),
                    FirstName = string.Empty,
                    LastName = string.Empty,
                    OrgName = string.Empty
                };
                if (message.CharacterFlags.HasFlag(CharacterFlags.HasVisibleName))
                {
                    pc.FirstName = streamReader.ReadString(streamReader.ReadInt16());
                    pc.LastName = streamReader.ReadString(streamReader.ReadInt16());
                }

                if (flags.HasFlag(SimpleCharFullUpdateFlags.HasOrgName))
                {
                    pc.OrgName = streamReader.ReadString(streamReader.ReadInt16());
                }

                message.CharacterInfo = pc;
            }

            message.Level = flags.HasFlag(SimpleCharFullUpdateFlags.HasExtendedLevel)
                                ? streamReader.ReadInt16()
                                : streamReader.ReadByte();
            message.Health = flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallHealth)
                                 ? streamReader.ReadUInt16()
                                 : streamReader.ReadInt32();
            if (flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallHealthDamage))
            {
                message.HealthDamage = streamReader.ReadByte();
            }
            else if (flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallHealth))
            {
                message.HealthDamage = streamReader.ReadUInt16();
            }
            else
            {
                message.HealthDamage = streamReader.ReadInt32();
            }

            message.MonsterData = streamReader.ReadUInt32();
            message.MonsterScale = streamReader.ReadInt16();
            message.VisualFlags = streamReader.ReadInt16();
            message.VisibleTitle = streamReader.ReadByte();

            int unknownLength = streamReader.ReadInt32();
            if (unknownLength < 0 || unknownLength > streamReader.Length - streamReader.Position)
            {
                throw new System.IO.InvalidDataException("Invalid SimpleCharFullUpdate Unknown1 length.");
            }

            message.Unknown1 = streamReader.ReadBytes(unknownLength);
            if (flags.HasFlag(SimpleCharFullUpdateFlags.HasHeadMesh))
            {
                message.HeadMesh = streamReader.ReadUInt32();
            }

            message.RunSpeedBase = flags.HasFlag(SimpleCharFullUpdateFlags.HasExtendedRunSpeed)
                                       ? streamReader.ReadInt16()
                                       : streamReader.ReadByte();
            if (flags.HasFlag(SimpleCharFullUpdateFlags.IsUnderAttack))
            {
                message.FightingTarget = streamReader.ReadIdentity();
            }

            byte[] remaining = streamReader.ReadBytes((int)(streamReader.Length - streamReader.Position));
            ScfuTail tail;
            if (TryDecodeTail(remaining, flags, message.Identity, out tail))
            {
                message.ExtendedTextureOverrideData = tail.ExtendedTextureOverrideData;
                message.ActiveNanos = tail.ActiveNanos;
                message.Waypoints = tail.Waypoints;
                message.Textures = tail.Textures;
                message.Meshes = tail.Meshes;
                message.Flags2 = tail.Flags2;
                message.Unknown2 = tail.Unknown2;
                message.Unknown4 = tail.Unknown4;
                message.TailFullyDecoded = true;
                message.UndecodedTail = new byte[0];
            }
            else
            {
                message.ExtendedTextureOverrideData = new byte[0];
                message.ActiveNanos = new ActiveNano[0];
                message.Waypoints = new Vector3[0];
                message.Textures = new Texture[0];
                message.Meshes = new Mesh[0];
                message.TailFullyDecoded = false;
                message.UndecodedTail = remaining;
            }

            return message;
        }

        public Expression DeserializerExpression(
            ParameterExpression streamReaderExpression,
            ParameterExpression serializationContextExpression,
            Expression assignmentTargetExpression,
            PropertyMetaData propertyMetaData)
        {
            var deserializerMethodInfo =
                ReflectionHelper
                    .GetMethodInfo
                    <SimpleCharFullUpdateSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>
                    (o => o.Deserialize);
            var serializerExp = Expression.New(this.GetType());
            var callExp = Expression.Call(
                serializerExp,
                deserializerMethodInfo,
                new Expression[]
                    {
                        streamReaderExpression, serializationContextExpression, 
                        Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
                    });

            var assignmentExp = Expression.Assign(
                assignmentTargetExpression, Expression.TypeAs(callExp, assignmentTargetExpression.Type));
            return assignmentExp;
        }

        public void Serialize(
            StreamWriter streamWriter,
            SerializationContext serializationContext,
            object value,
            PropertyMetaData propertyMetaData = null)
        {
            // Wire order is the reverse of AOSharp SimpleCharFullUpdateSerializer.Deserialize.
            var scfu = (SimpleCharFullUpdateMessage)value;
            var activeNanos = scfu.ActiveNanos ?? new ActiveNano[0];
            var textures = scfu.Textures ?? new Texture[0];
            var meshes = scfu.Meshes ?? new Mesh[0];
            var unknown1 = scfu.Unknown1 ?? new byte[0];
            var waypoints = scfu.Waypoints ?? new Vector3[0];
            string name = scfu.Name ?? string.Empty;

            streamWriter.WriteInt32((int)scfu.N3MessageType);
            streamWriter.WriteInt32((int)scfu.Identity.Type);
            streamWriter.WriteInt32(scfu.Identity.Instance);
            streamWriter.WriteByte(scfu.Unknown);

            streamWriter.WriteByte(scfu.Version);
            streamWriter.WriteInt32((int)scfu.Flags); // patched with final flags below

            var flags = SimpleCharFullUpdateFlags.None;

            if (scfu.PlayfieldId.HasValue)
            {
                flags |= SimpleCharFullUpdateFlags.HasPlayfieldId;
                streamWriter.WriteInt32(scfu.PlayfieldId.Value);
            }

            // Position is unconditional; FightingTarget is NOT early (AOSharp uses IsUnderAttack later).
            streamWriter.WriteSingle(scfu.Coordinates.X);
            streamWriter.WriteSingle(scfu.Coordinates.Y);
            streamWriter.WriteSingle(scfu.Coordinates.Z);

            if (scfu.Heading != null)
            {
                flags |= SimpleCharFullUpdateFlags.HasHeading;
                streamWriter.WriteSingle(scfu.Heading.X);
                streamWriter.WriteSingle(scfu.Heading.Y);
                streamWriter.WriteSingle(scfu.Heading.Z);
                streamWriter.WriteSingle(scfu.Heading.W);
            }

            streamWriter.WriteUInt32(scfu.Appearance.Value);
            WriteLengthPrefixedByteString(streamWriter, name);

            streamWriter.WriteInt32((int)scfu.CharacterFlags);
            streamWriter.WriteInt16(scfu.AccountFlags);
            streamWriter.WriteInt16(scfu.Expansions);

            var snpc = scfu.CharacterInfo as SimpleNpcInfo;
            var spc = scfu.CharacterInfo as SimplePcInfo;
            if (snpc != null)
            {
                flags |= SimpleCharFullUpdateFlags.IsNpc;

                // HasSmallNpcFamily set => byte; unset => Int16 (AOSharp deserialize).
                if (snpc.Family >= 0 && snpc.Family <= byte.MaxValue)
                {
                    flags |= SimpleCharFullUpdateFlags.HasSmallNpcFamily;
                    streamWriter.WriteByte((byte)snpc.Family);
                }
                else
                {
                    streamWriter.WriteInt16(snpc.Family);
                }

                if (snpc.LosHeight >= 0 && snpc.LosHeight <= byte.MaxValue)
                {
                    flags |= SimpleCharFullUpdateFlags.HasSmallNpcLosHeight;
                    streamWriter.WriteByte((byte)snpc.LosHeight);
                }
                else
                {
                    streamWriter.WriteInt16(snpc.LosHeight);
                }

                // AOSharp: when UnknownDataFlag, read Byte + Int16 (no trailing UnknownData3).
                bool emitUnknownData =
                    snpc.UnknownData != 0
                    || snpc.UnknownData2 != 0
                    || (scfu.AdditionalFlags & SimpleCharFullUpdateFlags.UnknownDataFlag) != 0;
                if (emitUnknownData
                    && (scfu.SuppressedFlags & SimpleCharFullUpdateFlags.UnknownDataFlag) == 0)
                {
                    flags |= SimpleCharFullUpdateFlags.UnknownDataFlag;
                    streamWriter.WriteByte((byte)snpc.UnknownData);
                    streamWriter.WriteInt16(snpc.UnknownData2);
                }

                // Live NPC SCFUs commonly carry these; preserve them on round-trip when
                // AdditionalFlags/SuppressedFlags are unset (deserialize does not populate them).
                flags |= SimpleCharFullUpdateFlags.UnknownFlag;
                flags |= SimpleCharFullUpdateFlags.UnknownFlag2;
            }
            else if (spc != null)
            {
                streamWriter.WriteUInt32(spc.CurrentNano);
                streamWriter.WriteInt32(spc.Team);
                streamWriter.WriteInt16(spc.Swim);
                streamWriter.WriteInt16(spc.StrengthBase);
                streamWriter.WriteInt16(spc.AgilityBase);
                streamWriter.WriteInt16(spc.StaminaBase);
                streamWriter.WriteInt16(spc.IntelligenceBase);
                streamWriter.WriteInt16(spc.SenseBase);
                streamWriter.WriteInt16(spc.PsychicBase);

                bool hasOrgName = string.IsNullOrWhiteSpace(spc.OrgName) == false || spc.OrgId != 0;
                if (hasOrgName)
                {
                    flags |= SimpleCharFullUpdateFlags.HasOrgName;
                    streamWriter.WriteInt32(spc.OrgId);
                    streamWriter.WriteByte(spc.OrgIdPadding);
                }

                if (scfu.CharacterFlags.HasFlag(CharacterFlags.HasVisibleName))
                {
                    WriteLengthPrefixedByteString(streamWriter, spc.FirstName ?? string.Empty);
                    WriteLengthPrefixedByteString(streamWriter, spc.LastName ?? string.Empty);
                }

                if (hasOrgName)
                {
                    WriteLengthPrefixedByteString(streamWriter, spc.OrgName ?? string.Empty);
                }
            }

            if (scfu.CharacterFlags.HasFlag(CharacterFlags.Tower))
            {
                streamWriter.WriteByte(scfu.ScfuTowerUnk);
            }

            if (scfu.Level > byte.MaxValue)
            {
                flags |= SimpleCharFullUpdateFlags.HasExtendedLevel;
                streamWriter.WriteInt16(scfu.Level);
            }
            else
            {
                streamWriter.WriteByte((byte)scfu.Level);
            }

            if (scfu.Health >= 0 && scfu.Health <= ushort.MaxValue)
            {
                flags |= SimpleCharFullUpdateFlags.HasSmallHealth;
                streamWriter.WriteUInt16((ushort)scfu.Health);
            }
            else
            {
                streamWriter.WriteInt32(scfu.Health);
            }

            if (scfu.HealthDamage >= 0 && scfu.HealthDamage <= byte.MaxValue)
            {
                flags |= SimpleCharFullUpdateFlags.HasSmallHealthDamage;
                streamWriter.WriteByte((byte)scfu.HealthDamage);
            }
            else if (flags.HasFlag(SimpleCharFullUpdateFlags.HasSmallHealth))
            {
                streamWriter.WriteUInt16((ushort)scfu.HealthDamage);
            }
            else
            {
                streamWriter.WriteInt32(scfu.HealthDamage);
            }

            streamWriter.WriteUInt32(scfu.MonsterData);
            streamWriter.WriteInt16(scfu.MonsterScale);
            streamWriter.WriteInt16(scfu.VisualFlags);
            streamWriter.WriteByte(scfu.VisibleTitle);

            streamWriter.WriteInt32(unknown1.Length);
            streamWriter.WriteBytes(unknown1);

            if (scfu.HeadMesh.HasValue)
            {
                flags |= SimpleCharFullUpdateFlags.HasHeadMesh;
                streamWriter.WriteInt32((int)scfu.HeadMesh.Value);
            }

            if (scfu.RunSpeedBase > byte.MaxValue)
            {
                flags |= SimpleCharFullUpdateFlags.HasExtendedRunSpeed;
                streamWriter.WriteInt16(scfu.RunSpeedBase);
            }
            else
            {
                streamWriter.WriteByte((byte)scfu.RunSpeedBase);
            }

            if (scfu.FightingTarget != null)
            {
                flags |= SimpleCharFullUpdateFlags.IsUnderAttack;
                Identity fightingTarget = scfu.FightingTarget.Value;
                streamWriter.WriteInt32((int)fightingTarget.Type);
                streamWriter.WriteInt32(fightingTarget.Instance);
            }

            if (scfu.ExtendedTextureOverrideData != null && scfu.ExtendedTextureOverrideData.Length > 0)
            {
                flags |= SimpleCharFullUpdateFlags.HasExtendedTextures;
                streamWriter.WriteBytes(scfu.ExtendedTextureOverrideData);
            }

            // Preview Additional/Suppressed so IsImmune / UnknownFlag3 padding matches final flags.
            var previewFlags = (flags | scfu.AdditionalFlags) & ~scfu.SuppressedFlags;
            if (previewFlags.HasFlag(SimpleCharFullUpdateFlags.IsImmune))
            {
                streamWriter.WriteByte(scfu.IsImmunePadding);
            }

            if (previewFlags.HasFlag(SimpleCharFullUpdateFlags.UnknownFlag3))
            {
                streamWriter.WriteByte(scfu.UnknownFlag3Padding);
            }

            streamWriter.WriteInt32((activeNanos.Length + 1) * 0x3F1);
            foreach (var activeNano in activeNanos)
            {
                streamWriter.WriteInt32((int)activeNano.NanoIdentity.Type);
                streamWriter.WriteInt32(activeNano.NanoIdentity.Instance);
                streamWriter.WriteInt32(activeNano.NanoInstance);
                streamWriter.WriteInt32(activeNano.Time1);
                streamWriter.WriteInt32(activeNano.Time2);
            }

            if (waypoints.Length > 0)
            {
                flags |= SimpleCharFullUpdateFlags.HasWaypoints;
                streamWriter.WriteInt32((int)scfu.Identity.Type);
                streamWriter.WriteInt32(scfu.Identity.Instance);
                streamWriter.WriteInt32(waypoints.Length);
                foreach (var waypoint in waypoints)
                {
                    streamWriter.WriteSingle(waypoint.X);
                    streamWriter.WriteSingle(waypoint.Y);
                    streamWriter.WriteSingle(waypoint.Z);
                }
            }

            streamWriter.WriteInt32((textures.Length + 1) * 0x3F1);
            foreach (var texture in textures)
            {
                streamWriter.WriteInt32(texture.Place);
                streamWriter.WriteInt32(texture.Id);
                streamWriter.WriteInt32(texture.Unknown);
            }

            streamWriter.WriteInt32((meshes.Length + 1) * 0x3F1);
            foreach (var mesh in meshes)
            {
                streamWriter.WriteByte(mesh.Position);
                streamWriter.WriteUInt32(mesh.Id);
                streamWriter.WriteInt32(mesh.OverrideTextureId);
                streamWriter.WriteByte(mesh.Layer);
            }

            streamWriter.WriteInt32(scfu.Flags2);

            // ScfuFlags2.HasOwner = 0x4
            if ((scfu.Flags2 & 0x4) != 0)
            {
                streamWriter.WriteInt32(scfu.OwnerInstance ?? 0);
            }

            streamWriter.WriteByte(scfu.Unknown2);

            // ScfuFlags2.Unknown3 = 0x40 — special-attack list (empty count when none supplied).
            if ((scfu.Flags2 & 0x40) != 0)
            {
                streamWriter.WriteByte(0);
            }

            // ScfuFlags2.Unknown1 = 0x2 — AOSharp has this commented; captures (Infector) require it.
            if ((scfu.Flags2 & 0x2) != 0)
            {
                streamWriter.WriteByte(scfu.Unknown4);
            }

            flags |= scfu.AdditionalFlags;
            flags &= ~scfu.SuppressedFlags;

            // Structural header flags must agree with the fields already written above.
            if (scfu.Level > byte.MaxValue)
            {
                flags |= SimpleCharFullUpdateFlags.HasExtendedLevel;
            }
            else
            {
                flags &= ~SimpleCharFullUpdateFlags.HasExtendedLevel;
            }

            if (scfu.Health >= 0 && scfu.Health <= ushort.MaxValue)
            {
                flags |= SimpleCharFullUpdateFlags.HasSmallHealth;
            }
            else
            {
                flags &= ~SimpleCharFullUpdateFlags.HasSmallHealth;
            }

            if (scfu.HealthDamage >= 0 && scfu.HealthDamage <= byte.MaxValue)
            {
                flags |= SimpleCharFullUpdateFlags.HasSmallHealthDamage;
            }
            else
            {
                flags &= ~SimpleCharFullUpdateFlags.HasSmallHealthDamage;
            }

            if (scfu.RunSpeedBase > byte.MaxValue)
            {
                flags |= SimpleCharFullUpdateFlags.HasExtendedRunSpeed;
            }
            else
            {
                flags &= ~SimpleCharFullUpdateFlags.HasExtendedRunSpeed;
            }

            if (scfu.FightingTarget != null)
            {
                flags |= SimpleCharFullUpdateFlags.IsUnderAttack;
            }
            else
            {
                flags &= ~SimpleCharFullUpdateFlags.IsUnderAttack;
            }

            if (waypoints.Length > 0)
            {
                flags |= SimpleCharFullUpdateFlags.HasWaypoints;
            }
            else
            {
                flags &= ~SimpleCharFullUpdateFlags.HasWaypoints;
            }

            if (snpc != null)
            {
                flags |= SimpleCharFullUpdateFlags.IsNpc;
                if (snpc.Family >= 0 && snpc.Family <= byte.MaxValue)
                {
                    flags |= SimpleCharFullUpdateFlags.HasSmallNpcFamily;
                }
                else
                {
                    flags &= ~SimpleCharFullUpdateFlags.HasSmallNpcFamily;
                }

                if (snpc.LosHeight >= 0 && snpc.LosHeight <= byte.MaxValue)
                {
                    flags |= SimpleCharFullUpdateFlags.HasSmallNpcLosHeight;
                }
                else
                {
                    flags &= ~SimpleCharFullUpdateFlags.HasSmallNpcLosHeight;
                }
            }

            var pos = streamWriter.Position;
            streamWriter.Position = 30;
            streamWriter.WriteInt32((int)flags);
            streamWriter.Position = pos;
        }

        private static void WriteLengthPrefixedByteString(StreamWriter streamWriter, string value)
        {
            int length = value.Length + 1;
            if (length > byte.MaxValue)
            {
                length = byte.MaxValue;
            }

            streamWriter.WriteByte((byte)length);
            streamWriter.WriteString(value, length);
        }

        public Expression SerializerExpression(
            ParameterExpression streamWriterExpression,
            ParameterExpression serializationContextExpression,
            Expression valueExpression,
            PropertyMetaData propertyMetaData)
        {
            var serializerMethodInfo =
                ReflectionHelper
                    .GetMethodInfo
                    <SimpleCharFullUpdateSerializer,
                        Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(o => o.Serialize);
            var serializerExp = Expression.New(this.GetType());
            var callExp = Expression.Call(
                serializerExp,
                serializerMethodInfo,
                new[]
                    {
                        streamWriterExpression, serializationContextExpression, valueExpression, 
                        Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
                    });
            return callExp;
        }

        private static Vector3 ReadVector3(StreamReader streamReader)
        {
            return new Vector3
            {
                X = streamReader.ReadSingle(),
                Y = streamReader.ReadSingle(),
                Z = streamReader.ReadSingle()
            };
        }

        private static bool TryDecodeTail(
            byte[] bytes,
            SimpleCharFullUpdateFlags flags,
            Identity identity,
            out ScfuTail result)
        {
            result = null;
            int firstOffset = flags.HasFlag(SimpleCharFullUpdateFlags.HasExtendedTextures) ? 1 : 0;
            int lastOffset = flags.HasFlag(SimpleCharFullUpdateFlags.HasExtendedTextures)
                                 ? Math.Max(1, bytes.Length - 17)
                                 : 0;

            for (int offset = firstOffset; offset <= lastOffset; offset++)
            {
                ScfuTail candidate;
                if (TryDecodeTailAt(bytes, offset, flags, identity, out candidate))
                {
                    candidate.ExtendedTextureOverrideData = new byte[offset];
                    Buffer.BlockCopy(bytes, 0, candidate.ExtendedTextureOverrideData, 0, offset);
                    result = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryDecodeTailAt(
            byte[] bytes,
            int offset,
            SimpleCharFullUpdateFlags flags,
            Identity identity,
            out ScfuTail result)
        {
            result = null;
            try
            {
                using (var memoryStream = new System.IO.MemoryStream(bytes, false))
                using (var reader = new StreamReader(memoryStream))
                {
                    reader.Position = offset;
                    int count;
                    if (!TryReadX3F1Count(reader, out count))
                    {
                        return false;
                    }

                    var activeNanos = new List<ActiveNano>(count);
                    for (int index = 0; index < count; index++)
                    {
                        activeNanos.Add(
                            new ActiveNano
                            {
                                NanoIdentity = reader.ReadIdentity(),
                                NanoInstance = reader.ReadInt32(),
                                Time1 = reader.ReadInt32(),
                                Time2 = reader.ReadInt32()
                            });
                    }

                    var waypoints = new List<Vector3>();
                    if (flags.HasFlag(SimpleCharFullUpdateFlags.HasWaypoints))
                    {
                        Identity waypointOwner = reader.ReadIdentity();
                        if (waypointOwner.Type != identity.Type || waypointOwner.Instance != identity.Instance)
                        {
                            return false;
                        }

                        int waypointCount = reader.ReadInt32();
                        if (waypointCount < 0 || waypointCount > 4096)
                        {
                            return false;
                        }

                        for (int index = 0; index < waypointCount; index++)
                        {
                            waypoints.Add(ReadVector3(reader));
                        }
                    }

                    if (!TryReadX3F1Count(reader, out count))
                    {
                        return false;
                    }

                    var textures = new List<Texture>(count);
                    for (int index = 0; index < count; index++)
                    {
                        textures.Add(
                            new Texture
                            {
                                Place = reader.ReadInt32(),
                                Id = reader.ReadInt32(),
                                Unknown = reader.ReadInt32()
                            });
                    }

                    if (!TryReadX3F1Count(reader, out count))
                    {
                        return false;
                    }

                    var meshes = new List<Mesh>(count);
                    for (int index = 0; index < count; index++)
                    {
                        meshes.Add(
                            new Mesh
                            {
                                Position = reader.ReadByte(),
                                Id = reader.ReadUInt32(),
                                OverrideTextureId = reader.ReadInt32(),
                                Layer = reader.ReadByte()
                            });
                    }

                    if (reader.Length - reader.Position < 5)
                    {
                        return false;
                    }

                    int flags2 = reader.ReadInt32();
                    int trailingByteCount = (flags2 & 0x2) != 0 ? 2 : 1;
                    if (reader.Length - reader.Position != trailingByteCount)
                    {
                        return false;
                    }

                    byte unknown2 = reader.ReadByte();
                    byte unknown4 = (flags2 & 0x2) != 0 ? reader.ReadByte() : (byte)0;

                    result = new ScfuTail
                    {
                        ActiveNanos = activeNanos.ToArray(),
                        Waypoints = waypoints.ToArray(),
                        Textures = textures.ToArray(),
                        Meshes = meshes.ToArray(),
                        Flags2 = flags2,
                        Unknown2 = unknown2,
                        Unknown4 = unknown4
                    };
                    return reader.Position == reader.Length;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryReadX3F1Count(StreamReader reader, out int count)
        {
            count = 0;
            if (reader.Length - reader.Position < 4)
            {
                return false;
            }

            int marker = reader.ReadInt32();
            if (marker < 0x3F1 || marker % 0x3F1 != 0)
            {
                return false;
            }

            count = (marker / 0x3F1) - 1;
            return count >= 0 && count <= 4096;
        }

        private sealed class ScfuTail
        {
            public byte[] ExtendedTextureOverrideData { get; set; }

            public ActiveNano[] ActiveNanos { get; set; }

            public Vector3[] Waypoints { get; set; }

            public Texture[] Textures { get; set; }

            public Mesh[] Meshes { get; set; }

            public int Flags2 { get; set; }

            public byte Unknown2 { get; set; }

            public byte Unknown4 { get; set; }
        }

        #endregion
    }
}
