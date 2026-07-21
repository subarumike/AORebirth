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

    using ZoneEngine.Core;
    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// Insurance Terminal SaveChar (53032).
    /// Capture evidence:
    /// - 20260714-164349 Borealis Terminal (fee = level × 100)
    /// - 20260716-141512 Omni-Trade Terminal:C005028F PF655:
    ///   Cash → "{fee} credits were deducted..." → SocialStatus=12 →
    ///   "Character stored. {SavedXP} XP saved." → GenericCmd Use ACK
    /// Unsaved XP → SavedXP watermark; SK → LastSK.
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

            // Live Omni-Trade Insurance: SocialStatus=12.
            SendSocialStatus(character, 12);

            SaveRespawnPoint(character);

            uint savedSk;
            uint savedXp = CombatXpRuntimeService.ApplyInsuranceTerminalSave(character, out savedSk);

            // Level 201+ (Shadowlevels) earn SK not XP; 220 (max) earns neither. Shared with the
            // garden save pad so the giant cumulative XP total is never shown to high-level chars.
            SendFeedback(character, CombatXpRuntimeService.BuildSaveRewardText(level, savedXp, savedSk));

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
            character.Stats.Write();
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
