namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages.OrgServerMessages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Chat;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Organizations;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// /org info: answers OrgClient Info with OrgServer/OrgInfo for the targeted player's organization
    /// (capture 20260623-084448: a self target is the requesting character). The rank is shown only when the target
    /// belongs to the requester's own organization. /org ranks, tax and bank (read-only) and the not-in-an-organization refusals are also answered; other OrgClient commands are not handled yet.
    /// </summary>
    public sealed partial class OrgClientMessageHandler : IMessageHandler<OrgClientMessage>
    {
        readonly IGameData _gameData;
        readonly Lazy<PlayfieldManager> _playfieldManager;
        readonly IOrganizationRepository _organizations;
        readonly IZoneLogger _logger;
        readonly IChatEngineLink _chat;

        // Lazy: PlayfieldManager depends on the message router, which depends on the handlers.
        public OrgClientMessageHandler(IGameData gameData, Lazy<PlayfieldManager> playfieldManager,
            IOrganizationRepository organizations, IZoneLogger logger, IChatEngineLink chat)
        {
            _gameData = gameData;
            _playfieldManager = playfieldManager;
            _organizations = organizations;
            _logger = logger;
            _chat = chat;
        }

        public Type MessageBodyType => typeof(OrgClientMessage);

        public void Handle(MessageBody body, IZoneSession session) => Handle((OrgClientMessage)body, session);

        public void Handle(OrgClientMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay || session.Player is not { } player
                || !ReferenceEquals(player.Session, session) || player.IsPersistenceQuarantined)
                return;

            switch (message.Command)
            {
                case OrgClientCommand.Info:
                    HandleInfo(message, session, player);
                    return;
                case OrgClientCommand.Ranks:
                case OrgClientCommand.Tax:
                case OrgClientCommand.Bank:
                    HandleReadOnly(message, session, player);
                    return;
                case OrgClientCommand.Name:
                case OrgClientCommand.Description:
                case OrgClientCommand.Objective:
                case OrgClientCommand.History:
                case OrgClientCommand.GoverningForm:
                    HandleLeaderCommand(message, session, player);
                    return;
                case OrgClientCommand.Promote:
                    HandleRankChange(message, session, player, promote: true);
                    return;
                case OrgClientCommand.Demote:
                    HandleRankChange(message, session, player, promote: false);
                    return;
                case OrgClientCommand.Invite:
                case OrgClientCommand.Join:
                    HandleInviteJoin(message, session, player);
                    return;
                default:
                    // Commands not implemented yet stay silent for members; non-members get the captured refusal
                    // for the commands the captures proved answer "not in an organization".
                    if (player.Organization == null && AnswersNotInOrganization(message.Command))
                        SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.NotInOrganization));
                    return;
            }
        }

        static bool AnswersNotInOrganization(OrgClientCommand command) => command is
            OrgClientCommand.Debt or OrgClientCommand.VoteInfo or OrgClientCommand.Vote or OrgClientCommand.StartVote
            or OrgClientCommand.StopVote or OrgClientCommand.Unknown2 or OrgClientCommand.Kick
            or OrgClientCommand.BankAdd or OrgClientCommand.BankRemove or OrgClientCommand.Name
            or OrgClientCommand.Description or OrgClientCommand.Objective or OrgClientCommand.History
            or OrgClientCommand.GoverningForm or OrgClientCommand.Disband or OrgClientCommand.Leave
            or OrgClientCommand.Invite;

        static void SendRef(IZoneSession session, Player player, string text)
            => session.Send(OrgChatText.Message(player.Identity, text));

        // /org ranks, /org tax and /org bank without arguments: replies copied from the captures. Setting a tax and the
        // bank transfers are not implemented yet (they write to the database).
        void HandleReadOnly(OrgClientMessage message, IZoneSession session, Player player)
        {
            if (player.Organization is not { } org)
            {
                SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.NotInOrganization));
                return;
            }

            GoverningFormCatalog forms = _gameData.GoverningForms;
            switch (message.Command)
            {
                case OrgClientCommand.Ranks:
                    if (forms.Forms.TryGetValue(org.GovernmentForm, out GoverningForm? form))
                        SendRef(session, player, OrgChatText.RanksReply(form.Name, form.Ranks));
                    return;
                case OrgClientCommand.Tax when string.IsNullOrEmpty(message.CommandArgs):
                    SendRef(session, player, OrgChatText.WithInt(OrgChatRefs.TaxValue, org.Tax));
                    return;
                case OrgClientCommand.Bank:
                    // Viewing is refused to the lowest rank except in single-rank forms (anarchism); that refusal's ref was
                    // not captured, so such a request gets no reply rather than a guessed one.
                    int lowest = forms.Forms.TryGetValue(org.GovernmentForm, out GoverningForm? f) ? f.Ranks.Length - 1 : 0;
                    int rank = player.Stats.GetOrZero(CharacterStat.ClanLevel, StatDetail.Base);
                    if (lowest == 0 || rank < lowest)
                        SendRef(session, player, OrgChatText.WithInt(OrgChatRefs.BankBalance, org.Bank));
                    return;
            }
        }

        void HandleInfo(OrgClientMessage message, IZoneSession session, Player player)
        {
            Player target = player;
            if (message.Target.Instance != player.Identity.Instance
                && (message.Target.Instance == 0
                    || !_playfieldManager.Value.FindPlayer(message.Target.Instance, out target)))
                return;

            if (target.Organization is not { } org)
            {
                SendRef(session, player, OrgChatText.Wrap(ReferenceEquals(target, player)
                    ? OrgChatRefs.InfoSelfNoOrganization
                    : OrgChatRefs.InfoTargetNoOrganization));
                return;
            }
            bool sameOrg = player.Organization?.Id == org.Id;

            session.Send(new OrgInfoMessage
            {
                Identity = player.Identity,
                Organization = new Identity { Type = IdentityType.Organization, Instance = org.Id },
                OrganizationName = org.Name ?? string.Empty,
                Description = org.Description ?? string.Empty,
                Objective = org.Objective ?? string.Empty,
                History = org.History ?? string.Empty,
                GoverningForm = _gameData.GoverningForms.GetFormName(org.GovernmentForm),
                LeaderName = org.LeaderName ?? string.Empty,
                Rank = sameOrg
                    ? _gameData.GoverningForms.GetRankName(
                        org.GovernmentForm, target.Stats.GetOrZero(CharacterStat.ClanLevel, StatDetail.Base))
                    : string.Empty,
                Unknown3 = Array.Empty<object>()
            });
        }
    }
}
