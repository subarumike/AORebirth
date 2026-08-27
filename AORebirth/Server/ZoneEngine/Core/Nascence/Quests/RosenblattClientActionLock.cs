namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using AORebirth.Core.Entities;
    using AORebirth.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;

    #endregion

    /// <summary>
    /// Clears client busy state after KnuBot trade/dialogue so Attack is not stuck on
    /// "Please wait until previous action has finished".
    /// </summary>
    internal static class RosenblattClientActionLock
    {
        internal static void Clear(ICharacter character)
        {
            if (character == null || character.Controller == null || character.Controller.Client == null)
            {
                return;
            }

            try
            {
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
            }
            catch
            {
            }

            try
            {
                var playfield = character.Playfield as Playfield;
                if (playfield != null)
                {
                    playfield.CancelPlayerAttack(character);
                }
                else
                {
                    character.SetFightingTarget(Identity.None);
                }
            }
            catch
            {
            }
        }
    }
}
