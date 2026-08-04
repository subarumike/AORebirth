namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Functions;

    #endregion

    /// <summary>
    /// Arete Landing ICC Cell Structure Scanner SaveChar.
    /// playfields.dat Terminal:C00D1999 tpl=300813 at (3464,9,811) next to Patrick.
    /// Live capture Terminal:574187D0 — 20260801-091856 / 20260801-Patrick Sun.
    /// </summary>
    public static class AreteCellStructureScannerSave
    {
        /// <summary>Live AO StaticDynel identity.</summary>
        public const int LiveTerminalInstance = unchecked((int)0x574187D0);

        /// <summary>playfields.dat Arete Terminal next to Patrick.</summary>
        public const int PlayfieldStatelInstance = unchecked((int)0xC00D1999);

        public const int TemplateId = 300813;

        // Capture 20260801-091856 auto-save tip after Character stored.
        private const string CapturedAutoSaveTipFeedback =
            "~&!!!\":!!!)<s??You will be automatically saved every time you reach a new level or always below level 20 (so you might save money by not using it!)";

        private static readonly MessagePackObject[] NoArguments = new MessagePackObject[0];

        public static bool IsTarget(Identity target, int templateId = 0)
        {
            if (target.Type != IdentityType.Terminal)
            {
                return false;
            }

            if (target.Instance == LiveTerminalInstance || target.Instance == PlayfieldStatelInstance)
            {
                return true;
            }

            return templateId == TemplateId;
        }

        public static bool TrySave(ICharacter character)
        {
            if (character == null)
            {
                return false;
            }

            character.DoNotDoTimers = false;

            try
            {
                // Proven SaveChar path (fee + Character stored). Capture Arete overlay after:
                // SocialStatus=0 (not Omni-Trade 12) + auto-save tip wire.
                bool saved = FunctionCollection.Instance.CallFunction(
                    (int)FunctionType.SaveChar,
                    character,
                    character,
                    character,
                    NoArguments);

                if (!saved)
                {
                    return false;
                }

                try
                {
                    character.Stats[StatIds.socialstatus].Set(0);
                    StatMessageHandler.Default.SendSingle(character, (int)StatIds.socialstatus, 0);
                }
                catch (Exception ex)
                {
                    LogUtil.Debug(
                        DebugInfoDetail.Engine,
                        "AreteCellStructureScannerSave SocialStatus overlay failed: " + ex.Message);
                }

                SendWireFeedback(character, CapturedAutoSaveTipFeedback);
                return true;
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "AreteCellStructureScannerSave.TrySave failed: " + ex);
                return false;
            }
        }

        private static void SendWireFeedback(ICharacter character, string formattedMessage)
        {
            if (character == null
                || character.Controller == null
                || character.Controller.Client == null
                || string.IsNullOrEmpty(formattedMessage))
            {
                return;
            }

            try
            {
                character.Controller.Client.SendCompressed(
                    new FormatFeedbackMessage
                    {
                        Identity = character.Identity,
                        Unknown = 1,
                        Unknown1 = 0,
                        FormattedMessage = formattedMessage,
                        Unknown2 = 0
                    },
                    character.Identity.Instance);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "AreteCellStructureScannerSave tip feedback failed: " + ex.Message);
            }
        }
    }
}
