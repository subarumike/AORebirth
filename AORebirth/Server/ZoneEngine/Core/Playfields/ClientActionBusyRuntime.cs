namespace ZoneEngine.Core.Playfields
{
    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    /// <summary>
    /// Clears the client "Please wait until previous action has finished" lock.
    /// UseActionFinished + ActionCategory only — never reset state/currentstate (that cancels combat).
    /// </summary>
    internal static class ClientActionBusyRuntime
    {
        internal static void Clear(ICharacter character)
        {
            if (character == null
                || character.Controller == null
                || character.Controller.Client == null)
            {
                return;
            }

            try
            {
                character.Stats[StatIds.actioncategory].Value = 0;

                character.Controller.Client.SendCompressed(
                    new CharacterActionMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        Action = CharacterActionType.UseActionFinished,
                        Unknown1 = 0,
                        Target = Identity.None,
                        Parameter1 = 0,
                        Parameter2 = 0,
                        Unknown2 = 0
                    });

                character.Controller.Client.SendCompressed(
                    new StatMessage
                    {
                        Identity = character.Identity,
                        Unknown = 0,
                        Stats =
                            new[]
                            {
                                new GameTuple<CharacterStat, uint>
                                {
                                    Value1 = CharacterStat.ActionCategory,
                                    Value2 = 0
                                }
                            }
                    });
            }
            catch
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "ClientActionBusyRuntime.Clear failed char="
                    + (character.Identity == null ? "?" : character.Identity.ToString()));
            }
        }
    }
}
