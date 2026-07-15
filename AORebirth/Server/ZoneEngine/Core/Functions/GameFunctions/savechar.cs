#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.
//

#endregion

namespace ZoneEngine.Core.Functions.GameFunctions
{
    #region Usings ...

    using System;
    using System.Globalization;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// Insurance Terminal SaveChar (53032). Capture evidence: 20260714-164349 Borealis Terminal.
    /// Fee = Rubi-Ka level × 100. Binds death respawn + SavedXP watermark.
    /// </summary>
    internal class savechar : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.SaveChar;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            ICharacter character = self as ICharacter;
            if (character == null)
            {
                return false;
            }

            int level = Math.Max(1, character.Stats[StatIds.level].Value);
            int fee = level * 100;
            int cashBefore = CashStatRules.Clamp(character.Stats[StatIds.cash].BaseValue);
            if (cashBefore < fee)
            {
                SendFeedback(
                    character,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Insurance Terminal requires {0} credits (level × 100).",
                        fee));
                return false;
            }

            int cashAfter = CashStatRules.Clamp((long)cashBefore - fee);
            character.Stats[StatIds.cash].Set((uint)cashAfter);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)cashAfter);

            SendFeedback(
                character,
                string.Format(CultureInfo.InvariantCulture, "{0} credits were deducted from your account.", fee));

            SendSocialStatus(character, 4);

            SaveRespawnPoint(character);
            ApplyInsuranceXpWatermark(character);
            character.Stats.Write();

            SendFeedback(character, "Character stored. 0 Shadowknowledge saved.");
            return true;
        }

        private static void SaveRespawnPoint(ICharacter character)
        {
            if (character.Playfield == null)
            {
                return;
            }

            // ResolvePlayerRespawnLocation reads TempSaveX as X and TempSaveY as world Z.
            int playfieldId = character.Playfield.Identity.Instance;
            int saveX = (int)Math.Round(character.RawCoordinates.X);
            int saveZ = (int)Math.Round(character.RawCoordinates.Z);

            character.Stats[StatIds.tempsaveplayfield].Set((uint)Math.Max(0, playfieldId));
            character.Stats[StatIds.tempsavex].Set((uint)Math.Max(0, saveX));
            character.Stats[StatIds.tempsavey].Set((uint)Math.Max(0, saveZ));
            character.Stats[StatIds.insurancepercentage].Set(100);
            character.Stats[StatIds.insurancetime].Set((uint)Math.Max(0, Environment.TickCount));
        }

        private static void ApplyInsuranceXpWatermark(ICharacter character)
        {
            uint cumulativeXp = character.Stats[StatIds.xp].BaseValue;
            if (cumulativeXp == 1234567890u)
            {
                cumulativeXp = 0;
            }

            character.Stats[StatIds.savedxp].Set(cumulativeXp);
        }

        private static void SendSocialStatus(ICharacter character, int value)
        {
            character.Stats[StatIds.socialstatus].Set((uint)value);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.socialstatus, (uint)value);
        }

        private static void SendFeedback(ICharacter character, string text)
        {
            ChatTextMessageHandler.Default.Send(character, text);

            if (character.Controller == null || character.Controller.Client == null)
            {
                return;
            }

            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = text,
                    Unknown2 = 0
                },
                character.Identity.Instance);
        }
    }
}
