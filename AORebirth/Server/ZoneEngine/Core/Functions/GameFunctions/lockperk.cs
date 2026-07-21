namespace ZoneEngine.Core.Functions.GameFunctions
{
    using System;
    using System.Threading;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    /// <summary>
    /// FunctionType.LockPerk (53187).
    /// Perk action args from items.dat: [mode, packetId, durationSeconds].
    /// Capture 20260715-194155: PerkUnavailable then PerkAvailable after cooldown.
    /// </summary>
    internal class lockperk : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.LockPerk;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            Character character = self as Character;
            if (character == null || character.Controller == null || character.Controller.Client == null)
            {
                return false;
            }

            int packetId;
            int durationSeconds;
            if (!TryReadArguments(arguments, out packetId, out durationSeconds))
            {
                return false;
            }

            // Enforce cooldown server-side; client UI alone is not authoritative.
            character.LockPerkPacket(packetId, durationSeconds);

            character.Controller.Client.SendCompressed(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.PerkUnavailable,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = packetId,
                    Parameter2 = 1,
                    Unknown2 = 0
                });

            int delayMs = Math.Max(1, durationSeconds) * 1000;
            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    Thread.Sleep(delayMs);
                    if (character.Controller == null || character.Controller.Client == null)
                    {
                        return;
                    }

                    character.Controller.Client.SendCompressed(
                        new CharacterActionMessage
                        {
                            Identity = character.Identity,
                            Unknown = 0,
                            Action = CharacterActionType.PerkAvailable,
                            Unknown1 = 0,
                            Target = Identity.None,
                            Parameter1 = 0,
                            Parameter2 = packetId,
                            Unknown2 = 0
                        });
                });

            LogUtil.Debug(
                DebugInfoDetail.GameFunctions,
                string.Format("LockPerk char={0} packetId={1} duration={2}s", character.Identity, packetId, durationSeconds));
            return true;
        }

        internal static bool TryReadArguments(MessagePackObject[] arguments, out int packetId, out int durationSeconds)
        {
            packetId = 0;
            durationSeconds = 0;
            if (arguments == null || arguments.Length < 2)
            {
                return false;
            }

            // Observed: [3, packetId, durationSeconds]
            if (arguments.Length >= 3)
            {
                packetId = arguments[1].AsInt32();
                durationSeconds = arguments[2].AsInt32();
                return packetId > 0 && durationSeconds > 0;
            }

            packetId = arguments[0].AsInt32();
            durationSeconds = arguments[1].AsInt32();
            return packetId > 0 && durationSeconds > 0;
        }
    }
}
