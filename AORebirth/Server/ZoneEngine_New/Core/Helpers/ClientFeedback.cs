namespace ZoneEngine_New.Core.Helpers
{
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Unformatted client feedback. The client looks the line up with
    /// GetText(category 110, ELF hash of the Feedback_* key). Retail Feedback body: Unknown1 0, category, message id
    /// (capture 2026-09-29: 00000000 0000006E 0BCBE074); the client picks the feedback channel itself.
    /// </summary>
    internal static class ClientFeedback
    {
        public const int CategoryId = 110;

        /// <summary>"You're unable to perform this action; please check the requirements of the item." Key name unknown.</summary>
        public const int CheckItemRequirements = 141178878;

        /// <summary>"Target does not have enough nano controlling units (NCU) left." (category 110).</summary>
        public const int TargetNotEnoughNcu = 220179189;

        /// <summary>
        /// "Nano program executed successfully." (category 110, 0x076C7CA9). Live sends it to the caster after
        /// FinishNanoCasting and before the nano's effects (summon capture 2026-10-07T21:09:22Z).
        /// </summary>
        public const int NanoExecutedSuccessfully = 124550313;

        /// <summary>"Target resisted." (category 110).</summary>
        public const int TargetResisted = 205237300;

        /// <summary>"A too high level player in your team prevents you from receiving any experience." (category 110).</summary>
        public const int TeammateTooHighForXp = 121950320;

        /// <summary>
        /// "This item requires a fighting-target to be applied on." (category 110, 0x0186D9F4). Live sends only this
        /// Feedback, no arguments, once per refused Stab before the fight started (capture 2026-10-06T14:02:52Z).
        /// </summary>
        public const int RequiresFightingTarget = 0x0186D9F4;

        /// <summary>"You are already running this action!" (category 110). Live perk spam capture 2026-10-02T03:54:58Z.</summary>
        public const int AlreadyRunningAction = 171187118;

        /// <summary>"The target is outside special attack range!" (category 110).</summary>
        public const int SpecialOutOfRange = 165509237;

        /// <summary>"You have no line of sight to the target!" (category 110).</summary>
        public const int SpecialNoLineOfSight = 175658468;

        /// <summary>"Special attack not possible. The target is aware of your presence." (category 110).</summary>
        public const int SpecialTargetAware = 90809749;

        /// <summary>"%d credits were deducted from your account." (category 110, FormatFeedback). Insurance save fee.</summary>
        public const int CreditsDeducted = 57671460;

        /// <summary>"Character stored. %d XP saved." (category 110, FormatFeedback). Insurance save.</summary>
        public const int CharacterStored = 159227364;

        /// <summary>"You failed to hide as you are currently fighting." (category 110). A stealth attempt mid-fight.</summary>
        public const int FailedToHideWhileFighting = 247632055;

        /// <summary>"You can't log out while in a fight." (category 110). A /camp refused mid-fight.</summary>
        public const int CantLogoutInFight = 207248100;

        /// <summary>"You have to kill %d more %s to fulfill one of your missions!" (category 110, FormatFeedback).</summary>
        public const int KillsRemaining = 204307477;

        /// <summary>"You've received an item as mission reward!" (category 110). Sent after a timed turn-in's token reward.</summary>
        public const int MissionRewardItemReceived = 108871108;

        /// <summary>
        /// Plain text passthrough: one string argument shown as is. Live sends item SystemText (53044, "You
        /// successfully perform an Impale attack.") as FormatFeedback 110/707 (capture 2026-10-02T03:54:58Z).
        /// </summary>
        public const int PlainText = 707;

        /// <summary>"Target %s your %s!" (category 110). A perk that fails to land: live capture 2026-10-02T13:02:33Z.</summary>
        public const int TargetVerbYourAction = 79653355;

        /// <summary>
        /// "Lockpicking successful." (category 110). The client shows nothing of its own when a pick opens a lock (the
        /// 0x64 action result only unlocks and opens it; Gamecode.dll has only Feedback_LockpickingFailed), so the
        /// server sends it.
        /// </summary>
        public const int LockpickingSuccessful = 265781900;

        /// <summary>
        /// "You can loot these remains." (category 110). Live sends it to a solo loot winner after the kill's death
        /// action and before the CorpseFullUpdate (capture 2026-10-05T01:45:30Z).
        /// </summary>
        public const int CanLootRemains = 0x0EE3EB33;

        /// <summary>
        /// "All team members can loot these remains." (text.mdb category 110, id 0x03940D94). We send it as zone
        /// Feedback, which shows the same text.
        /// NOTE: live may send this from the chat server instead. A live team kill showed the line with no zone
        /// Feedback or FormatFeedback packet (capture 2026-10-05T01:51:33Z), and the client never raises the id itself.
        /// A team-kill capture that includes chat-server traffic would confirm the real packet and chat tab.
        /// </summary>
        public const int AllTeamMembersCanLootRemains = 0x03940D94;

        /// <summary>
        /// "%u of your XP were allocated to your personal research." (category 110, 0x004DA1E0). Live sends it on a kill
        /// whose XP went to research, with FormatFeedback Unknown1 <see cref="ResearchAllocatedChannel"/>, before the
        /// PerkUpdate (capture 2026-10-13 22:48:10).
        /// </summary>
        public const int XpAllocatedToResearch = 5087712;

        /// <summary>FormatFeedback Unknown1 live uses for <see cref="XpAllocatedToResearch"/>.</summary>
        public const int ResearchAllocatedChannel = 0x4200001C;

        /// <summary>"You need a Reset Point in order to reset a skill!" (category 110).</summary>
        public const int NeedResetPoint = 142566068;

        /// <summary>"You can't reset Map Navigation!" (category 110).</summary>
        public const int CantResetMapNavigation = 54233262;

        /// <summary>
        /// "All items must be unequipped before performing a skill reset!" (category 110, 0x04A7B8B4). Live sends it as
        /// Feedback to a reset with items equipped (capture 2026-10-14T00:07:22Z).
        /// </summary>
        public const int UnequipBeforeSkillReset = 78100660;

        /// <summary>"You can't reset this skill! You need to have spent some IP on the skill first." (category 110).</summary>
        public const int NoIpSpentOnSkill = 72805276;

        /// <summary>
        /// "%d IP have been returned to you for the reset of %s!" (category 110, 0x0D7C0C34). Live: FormatFeedback with the
        /// refund and the skill name, after the RP and IP/skill stats (capture 2026-10-14T00:07:33Z).
        /// </summary>
        public const int SkillResetIpReturned = 226232628;

        /// <summary>
        /// "Your Full IP Reset has finished.\r\n\r\nYou now have %d IP." (category 110, 0x0D06358B): the line for a
        /// completed Reset All (CharacterAction ResetSkill with skill 0).
        /// </summary>
        public const int FullIpResetFinished = 218510731;

        /// <summary>text.mdb category of the client's GUI strings (skill window, dialogs).</summary>
        public const int GuiTextCategory = 502;

        /// <summary>"You are only allowed to reset abilities and skills." (category 502, 0x02B705F3).</summary>
        public const int OnlyAbilitiesAndSkillsResettable = 45549043;

        /// <summary>text.mdb category of the client's skill and stat names (id = stat id, e.g. 155 "Evade-ClsC").</summary>
        public const int StatNameCategory = 2003;

        /// <summary>"evaded" (category 110), the verb live puts in <see cref="TargetVerbYourAction"/>.</summary>
        public const int Evaded = 114064052;

        /// <summary>"No ammo was found for %s!" (category 110), the weapon's name as the argument (retail 2026-10-23).</summary>
        public const int NoAmmoFoundFor = 62734319;

        /// <summary>"%s is already fully loaded." (category 110), the weapon's name as the argument (retail 2026-10-23).</summary>
        public const int AlreadyFullyLoaded = 54186628;

        /// <summary>"You already have this unique item: %s" (category 110, FormatFeedback with the item name; retail 2026-10-25).</summary>
        public const int AlreadyHaveUniqueItem = 128711717;

        /// <summary>"Your trade partner already has this unique item: %s" (category 110, FormatFeedback with the item name).</summary>
        public const int TradePartnerHasUniqueItem = 205739797;

        /// <summary>"No combination found!" (category 110). An inventory item dropped on another with no recipe (retail 2026-10-24).</summary>
        public const int NoCombinationFound = 87316052;

        /// <summary>"Combine failed!" (category 110). Follows <see cref="CombineNeedsSkill"/> (retail 2026-10-24).</summary>
        public const int CombineFailed = 220669556;

        /// <summary>
        /// "It is theoretically possible to combine \"%s\" with \"%s\". But you need at least %u in %s." (category 110,
        /// FormatFeedback): source name, target name, skill needed, the skill's client name (retail 2026-10-24).
        /// </summary>
        public const int CombineNeedsSkill = 263676236;

        /// <summary>
        /// "You combined \"%s\" with \"%s\" and the result is a quality level %u \"%s\"." (category 110, FormatFeedback):
        /// source name, target name, result QL, result name (retail 2026-10-24).
        /// </summary>
        public const int CombinedResult = 102719811;

        /// <summary>
        /// "\"%s\" is of a too low quality level. With \"%s\" at quality of %u , the \"%s\" must be at least at quality %u."
        /// (category 110, FormatFeedback): source name, target name, target QL, source name, the lowest source QL that works.
        /// </summary>
        public const int CombineSourceQlTooLow = 171196092;

        /// <summary>"\"%s\" is of a too low quality level. It must be at least at quality %u." (category 110): item name, QL.</summary>
        public const int CombineItemQlTooLow = 253484164;

        /// <summary>"It is not possible to assemble those two items. Maybe the order was wrong?" (category 110).</summary>
        public const int CombineNotPossible = 46513671;

        /// <summary>"%d xp was gained as a side bonus!" (category 110): the bonus (retail 2026-10-27).</summary>
        public const int SideXpBonus = 226754835;

        /// <summary>
        /// "You recouped %d of your previously lost experience - remaining to be recouped: %d." (category 110): what a kill
        /// gave back from the death pool and what is left in it (retail 2026-10-27).
        /// </summary>
        public const int DeathXpRecouped = 629344;

        /// <summary>
        /// "This XP was added to the pool of unsaved experience points that you get back through gaining new experience
        /// points. Your current pool of unsaved experience is %d." (category 110): the pool after it grew.
        /// </summary>
        public const int DeathXpAddedToPool = 7144452;

        /// <summary>
        /// "%s looted %s from %s." (category 110, 60420421): looter name, item low id, high id, QL, item name, corpse name.
        /// Live sends it from the chat server (retail 2026-10-28, "Kustephe looted Battered Low-Tech Armor Shorts from
        /// Remains of 34-I Helper.").
        /// </summary>
        public const int LootedItemFrom = 60420421;

        /// <summary>
        /// Longest 's' argument: the client reads its length as one UTF-8 coded character holding length + 1, and the
        /// message string goes out as ASCII, so length + 1 must stay a single byte (0x7F).
        /// </summary>
        public const int MaxTextArgumentLength = 126;

        /// <summary>A text argument the client looks up itself: 'R' + base-85 category + base-85 message id.</summary>
        public readonly record struct TextReference(int Category, int MessageId);

        public static void Send(Character character, string key)
            => Send(character, unchecked((int)ElfHash(key)));

        public static void Send(Character character, int messageId)
        {
            if (character is not Player player || player.Session == null)
                return;

            player.Session.Send(Create(player.Identity, messageId));
        }

        /// <summary>
        /// Category 110 feedback text the client formats itself. Retail quest kill progress (capture 2026-09-27):
        /// Unknown1 0, then "~&" + base-85 category + base-85 message id, then each argument as 'i' + base-85 int,
        /// 'f' + base-85 IEEE754 bits for a float (retail mission token chance),
        /// 'R' + base-85 category + base-85 id for a <see cref="TextReference"/>, or 's' + (length + 1) + text.
        /// </summary>
        public static void SendFormatted(Character character, int messageId, params object[] args)
            => SendFormattedWithUnknown1(character, 0, messageId, args);

        /// <summary><see cref="SendFormatted"/> with the FormatFeedback Unknown1 live uses for that message.</summary>
        public static void SendFormattedWithUnknown1(Character character, int unknown1, int messageId, params object[] args)
        {
            if (character is not Player player || player.Session == null)
                return;

            player.Session.Send(new FormatFeedbackMessage
            {
                Identity = player.Identity,
                Unknown = 1,
                Unknown1 = unknown1,
                Unknown2 = 0,
                FormattedMessage = FormatText(messageId, args)
            });
        }

        /// <summary>
        /// The "~&" formatted text of category 110 message <paramref name="messageId"/> with <paramref name="args"/>, as
        /// <see cref="SendFormatted"/> sends it. The chat server carries the same text (a loot line).
        /// </summary>
        public static string FormatText(int messageId, params object[] args)
        {
            var text = new StringBuilder("~&").Append(Base85(CategoryId)).Append(Base85(messageId));
            foreach (object arg in args)
            {
                if (arg is int number)
                    text.Append('i').Append(Base85(number));
                else if (arg is float percentage)
                    text.Append('f').Append(Base85(System.BitConverter.SingleToInt32Bits(percentage)));
                else if (arg is TextReference reference)
                    text.Append('R').Append(Base85(reference.Category)).Append(Base85(reference.MessageId));
                else
                {
                    string value = arg?.ToString() ?? string.Empty;
                    if (value.Length > MaxTextArgumentLength)
                        value = value[..MaxTextArgumentLength];
                    text.Append('s').Append((char)(value.Length + 1)).Append(value);
                }
            }

            // Retail closes the formatted text with '~' (no-ammo capture 2026-10-23, "...s\x19Bolter Cannon 42mm - 40C~").
            text.Append('~');
            return text.ToString();
        }

        /// <summary>Five base-85 digits offset by '!', most significant first (110 -> "!!!\":").</summary>
        static string Base85(int value)
        {
            uint remaining = unchecked((uint)value);
            var digits = new char[5];
            for (int i = 4; i >= 0; i--)
            {
                digits[i] = (char)('!' + remaining % 85);
                remaining /= 85;
            }

            return new string(digits);
        }

        public static FeedbackMessage Create(Identity identity, string key)
            => Create(identity, unchecked((int)ElfHash(key)));

        static FeedbackMessage Create(Identity identity, int messageId)
            => new()
            {
                Identity = identity,
                Unknown = 1,
                Unknown1 = 0,
                CategoryId = CategoryId,
                MessageId = messageId
            };

        public static uint ElfHash(string text)
        {
            uint hash = 0;
            foreach (char c in text)
            {
                hash = (hash << 4) + c;
                uint high = hash & 0xF0000000;
                if (high != 0)
                    hash ^= high >> 24;
                hash &= ~high;
            }

            return hash;
        }
    }
}
