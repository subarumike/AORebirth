namespace ZoneEngine.Core
{
    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Items;
    using AORebirth.Core.Requirements;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Functions;

    /// <summary>
    /// Capture 20260723-123341: every Use of a Clan/Omni token board sends
    /// FormatFeedback "Side tokens collected: N." before any upgrade packets.
    /// </summary>
    internal static class TokenBoardRuntime
    {
        private const int ClanBoardLowId = 296363;

        private const int ClanBoardHighId = 296370;

        private const int OmniBoardLowId = 296371;

        private const int OmniBoardHighId = 296378;

        internal static bool IsTokenBoard(Item item)
        {
            if (item == null)
            {
                return false;
            }

            return IsTokenBoardId(item.LowID) || IsTokenBoardId(item.HighID);
        }

        internal static bool TryHandleUse(ICharacter character, Identity itemPosition, Item item)
        {
            if (character == null || item == null || !IsTokenBoard(item))
            {
                return false;
            }

            // Capture: FormatFeedback first, every click — even when upgrade requirements fail.
            SendSideTokensCollected(character, item);

            if (item.Events == null)
            {
                return true;
            }

            foreach (Event itemEvent in item.Events)
            {
                if (itemEvent.EventType != EventType.OnUse || itemEvent.Functions == null)
                {
                    continue;
                }

                foreach (Function function in itemEvent.Functions)
                {
                    if (IsSideTokensCollectedSystemText(function))
                    {
                        continue;
                    }

                    if (!RequirementsPass(character, function))
                    {
                        continue;
                    }

                    var args = new System.Collections.Generic.List<MessagePackObject>();
                    if (function.Arguments != null && function.Arguments.Values != null)
                    {
                        args.AddRange(function.Arguments.Values);
                    }

                    args.Add(itemPosition.Instance);
                    FunctionCollection.Instance.CallFunction(
                        function.FunctionType,
                        character,
                        character,
                        character,
                        args.ToArray());
                }
            }

            return true;
        }

        internal static void SendSideTokensCollected(ICharacter character, Item item)
        {
            if (character == null || character.Controller == null || character.Controller.Client == null)
            {
                return;
            }

            int tokens = GetSideTokenCount(character);
            string template = TryGetSideTokensTemplate(item);
            string plain;
            if (!string.IsNullOrEmpty(template) && template.IndexOf("%d", StringComparison.Ordinal) >= 0)
            {
                plain = template.Replace("%d", tokens.ToString());
            }
            else
            {
                // Capture 20260723-123341 first upgrade feedback.
                plain = "Side tokens collected: " + tokens + ".";
            }

            // Capture wire: "~&!!!\":!!!)<s" + (char)(len+1) + text — plain text is empty yellow.
            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = ToYellowSystemFeedback(plain),
                    Unknown2 = 0
                },
                character.Identity.Instance);
        }

        /// <summary>
        /// Capture 20260723-123341 FormatFeedback body prefix for yellow system chat.
        /// </summary>
        internal static string ToYellowSystemFeedback(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return plainText;
            }

            if (plainText.StartsWith("~&!!!", StringComparison.Ordinal))
            {
                return plainText;
            }

            int encodedLength = plainText.Length + 1;
            if (encodedLength > 255)
            {
                encodedLength = 255;
            }

            return "~&!!!\":!!!)<s" + (char)encodedLength + plainText;
        }

        private static bool IsTokenBoardId(int id)
        {
            return (id >= ClanBoardLowId && id <= ClanBoardHighId)
                   || (id >= OmniBoardLowId && id <= OmniBoardHighId);
        }

        private static string TryGetSideTokensTemplate(Item item)
        {
            if (item == null || item.Events == null)
            {
                return null;
            }

            foreach (Event itemEvent in item.Events)
            {
                if (itemEvent.EventType != EventType.OnUse || itemEvent.Functions == null)
                {
                    continue;
                }

                foreach (Function function in itemEvent.Functions)
                {
                    if (!IsSideTokensCollectedSystemText(function))
                    {
                        continue;
                    }

                    if (function.Arguments == null || function.Arguments.Values == null
                        || function.Arguments.Values.Count < 1)
                    {
                        continue;
                    }

                    try
                    {
                        return function.Arguments.Values[0].AsString();
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            return null;
        }

        private static bool IsSideTokensCollectedSystemText(Function function)
        {
            if (function == null || function.FunctionType != (int)FunctionType.SystemText)
            {
                return false;
            }

            if (function.Arguments == null || function.Arguments.Values == null
                || function.Arguments.Values.Count < 1)
            {
                return false;
            }

            try
            {
                string text = function.Arguments.Values[0].AsString();
                return text != null
                       && text.IndexOf("Side tokens collected", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool RequirementsPass(ICharacter character, Function function)
        {
            if (function.Requirements == null)
            {
                return true;
            }

            foreach (Requirement requirement in function.Requirements)
            {
                if (!requirement.CheckRequirement(character))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Clan = alignment (62), Omni = metatype (75). Neutral/other → 0.
        /// </summary>
        private static int GetSideTokenCount(ICharacter character)
        {
            int side = character.Stats[StatIds.side].Value;
            if (side == (int)Side.Clan)
            {
                return Math.Max(0, character.Stats[StatIds.alignment].Value);
            }

            if (side == (int)Side.Omni)
            {
                return Math.Max(0, character.Stats[StatIds.metatype].Value);
            }

            return 0;
        }
    }
}
