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

namespace ZoneEngine.Core.Functions.GameFunctions
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Statels;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Playfields;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using ServerPlayfield = AORebirth.Core.Playfields.Playfield;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    #endregion

    internal class teleportproxy : FunctionPrototype
    {
        private const FunctionType functionId = FunctionType.TeleportProxy;
        private const float ProxyEntryDoorClearance = 5.0f;

        public override FunctionType FunctionId
        {
            get
            {
                return functionId;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            
            ICharacter character = (ICharacter)self;

            int statelId =
                unchecked((int)(0xC0000000u | (uint)arguments[1].AsInt32() | ((uint)arguments[2].AsInt32() << 16)));
            character.Stats[StatIds.externaldoorinstance].BaseValue = (uint)caller.Identity.Instance;
            character.Stats[StatIds.externalplayfieldinstance].BaseValue = (uint)character.Playfield.Identity.Instance;

            if (arguments[1].AsInt32() > 0)
            {
                Coordinate officialDungeonDestination;
                if (TempleWorldInteractionRules.TryResolveProxyEntry(
                    character.Playfield.Identity.Instance,
                    caller.Identity,
                    arguments[0].AsInt32(),
                    arguments[1].AsInt32(),
                    arguments[2].AsInt32(),
                    arguments[3].AsInt32(),
                    out officialDungeonDestination))
                {
                    ServerPlayfield sourcePlayfield = character.Playfield as ServerPlayfield;
                    if (sourcePlayfield == null)
                    {
                        return false;
                    }

                    var preservedHeading = new Quaternion(
                        character.Rotation.xf,
                        character.Rotation.yf,
                        character.Rotation.zf,
                        character.Rotation.wf);
                    var envelopeDestination = new Vector3(
                        (float)character.Position.x,
                        (float)character.Position.y,
                        (float)character.Position.z);
                    sourcePlayfield.Teleport(
                        (Dynel)character,
                        officialDungeonDestination,
                        preservedHeading,
                        new Identity
                        {
                            Type = IdentityType.Playfield,
                            Instance = arguments[1].AsInt32()
                        },
                        transferCharacter =>
                            ZoneEngine.Core.MessageHandlers.TeleportMessageHandler.Default
                                .SendOfficialDungeonProxyTransfer(
                                    transferCharacter,
                                    envelopeDestination,
                                    preservedHeading,
                                    arguments[1].AsInt32(),
                                    caller.Identity));
                    return true;
                }

                Coordinate overrideDestination;
                Quaternion overrideHeading;
                if (SubwayTeleportProxyDestinationRules.TryResolveDestinationOverride(
                    arguments[1].AsInt32(),
                    statelId,
                    out overrideDestination,
                    out overrideHeading))
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Zoning,
                        string.Format(
                            "TeleportProxy caller={0} fromPf={1} from=({2:F2},{3:F2},{4:F2}) destDoor=SubwayEntranceOverride destPf={5} dest=({6:F2},{7:F2},{8:F2})",
                            caller.Identity.ToString(true),
                            character.Playfield.Identity.Instance,
                            (float)character.Position.x,
                            (float)character.Position.y,
                            (float)character.Position.z,
                            arguments[1].AsInt32(),
                            overrideDestination.x,
                            overrideDestination.y,
                            overrideDestination.z));
                    character.Playfield.Teleport(
                        (Dynel)character,
                        overrideDestination,
                        overrideHeading,
                        new Identity() { Type = (IdentityType)arguments[0].AsInt32(), Instance = arguments[1].AsInt32() });
                    return true;
                }

                StatelData sd = PlayfieldLoader.PFData[arguments[1].AsInt32()].GetDoor(statelId);
                if (sd == null)
                {
                    throw new Exception(
                        "Statel " + arguments[3].AsInt32().ToString("X") + " not found? Check the rdb dammit");
                }

                Vector3 v = new Vector3(sd.X, sd.Y, sd.Z);

                Quaternion q = new Quaternion(sd.HeadingX, sd.HeadingY, sd.HeadingZ, sd.HeadingW);

                Quaternion.Normalize(q);
                Vector3 n = (Vector3)q.RotateVector3(Vector3.AxisZ);

                v.x += n.x * ProxyEntryDoorClearance;
                v.z += n.z * ProxyEntryDoorClearance;
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    string.Format(
                        "TeleportProxy caller={0} fromPf={1} from=({2:F2},{3:F2},{4:F2}) destDoor={5} destPf={6} dest=({7:F2},{8:F2},{9:F2})",
                        caller.Identity.ToString(true),
                        character.Playfield.Identity.Instance,
                        (float)character.Position.x,
                        (float)character.Position.y,
                        (float)character.Position.z,
                        sd.Identity.ToString(true),
                        arguments[1].AsInt32(),
                        v.x,
                        v.y,
                        v.z));
                character.Playfield.Teleport(
                    (Dynel)character,
                    new Coordinate(v),
                    q,
                    new Identity() { Type = (IdentityType)arguments[0].AsInt32(), Instance = arguments[1].AsInt32() });
            }

            return true;
        }
    }
}
