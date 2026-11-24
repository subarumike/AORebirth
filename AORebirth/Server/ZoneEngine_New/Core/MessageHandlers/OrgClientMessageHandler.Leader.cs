namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.CompilerServices;

    using AORebirth.Interfaces.Persistence.Characters;
    using AORebirth.World.Contracts.V1;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Organizations;

    /// <summary>
    /// Leader-only commands (rank 0): /org name, description, objective, history and governingform. Rules and replies come
    /// from rules.txt and the captures (docs/generated/org_commands_scenario_capture_20261031.md).
    /// </summary>
    public sealed partial class OrgClientMessageHandler
    {
        const int MaxTextLength = 255;
        const int MinNameLength = 4;
        // organizations.Name is varchar(40) (migration 20261121_organizations_name_unique.sql), the retail limit.
        const int MaxNameLength = 40;
        // Punctuation seen in the live Rubi-Ka 2019 organization names (people.anarchy-online.com), besides letters, digits and spaces.
        const string NamePunctuation = ".-'()&";
        const int MinNameLetters = 3;
        const string OrganizationNameRefreshCommand = "org.refresh-name";
        readonly object _organizationNameRefreshSync = new();
        readonly ConditionalWeakTable<Player, object> _pendingOrganizationNames = new();

        void HandleLeaderCommand(OrgClientMessage message, IZoneSession session, Player player)
        {
            if (player.Organization is not { } org)
            {
                SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.NotInOrganization));
                return;
            }

            // Rank 0 is the leader (in single-rank forms every member is rank 0, as on retail).
            if (player.Stats.GetOrZero(CharacterStat.ClanLevel, StatDetail.Base) != 0)
            {
                SendRef(session, player, OrgChatText.Wrap(NotLeaderRef(message.Command)));
                return;
            }

            string args = (message.CommandArgs ?? string.Empty).Trim();
            try
            {
                switch (message.Command)
                {
                    case OrgClientCommand.Name:
                        ChangeName(player, org, args);
                        break;
                    case OrgClientCommand.Description:
                        ChangeText(org, OrganizationTextField.Description, Truncate(args));
                        break;
                    case OrgClientCommand.Objective:
                        ChangeText(org, OrganizationTextField.Objective, Truncate(args));
                        break;
                    case OrgClientCommand.History:
                        ChangeText(org, OrganizationTextField.History, Truncate(args));
                        break;
                    case OrgClientCommand.GoverningForm:
                        ChangeGoverningForm(session, player, org, args);
                        break;
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "/org " + message.Command + " failed for organization " + org.Id);
            }
        }

        static string NotLeaderRef(OrgClientCommand command) => command switch
        {
            OrgClientCommand.Name => OrgChatRefs.NotLeaderName,
            OrgClientCommand.Description => OrgChatRefs.NotLeaderDescription,
            OrgClientCommand.Objective => OrgChatRefs.NotLeaderObjective,
            OrgClientCommand.History => OrgChatRefs.NotLeaderHistory,
            _ => OrgChatRefs.NotLeaderGoverningForm
        };

        // The columns are varchar(255); retail truncation behaviour was not captured.
        static string Truncate(string text) => text.Length > MaxTextLength ? text[..MaxTextLength] : text;

        void ChangeName(Player player, OrganizationRecord org, string name)
        {
            // The refusal feedback for an invalid rename was not captured, so it is only logged for now.
            if (!IsValidName(name))
            {
                _logger.Info("/org name refused (invalid name) org=" + org.Id);
                return;
            }

            if (_organizations.IsNameTaken(name, org.Id))
            {
                _logger.Info("/org name refused (name already used) org=" + org.Id);
                return;
            }

            lock (_organizationNameRefreshSync)
            {
                _organizations.SaveText(org.Id, OrganizationTextField.Name, name);
                foreach (Player member in OnlineMembers(org.Id))
                    PublishOrganizationName(member, org.Id, name);

                string organizationId = org.Id.ToString(CultureInfo.InvariantCulture);
                _chat.SendChatCommand(player.Identity.Instance, "#aorebirth-org-refresh " + organizationId);
                _ = _worldCommands?.BroadcastAsync(OrganizationNameRefreshCommand, organizationId);
            }
        }

        void OnOrganizationNameRefresh(InstanceCommand command)
        {
            if (command.Arguments.Count != 1
                || !int.TryParse(command.Arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out int orgId)
                || orgId <= 0)
                return;

            lock (_organizationNameRefreshSync)
            {
                Player[] members = OnlineMembers(orgId).ToArray();
                if (members.Length == 0 || _organizations.GetById(orgId) is not { } organization)
                    return;

                foreach (Player member in members)
                    PublishOrganizationName(member, orgId, organization.Name);
            }
        }

        void PublishOrganizationName(Player member, int orgId, string name)
        {
            object publication = new();
            _pendingOrganizationNames.AddOrUpdate(member, publication);
            ApplyOnTick(member, () =>
            {
                lock (_organizationNameRefreshSync)
                {
                    // The active playfield can change while an earlier name update is queued.
                    if (!_pendingOrganizationNames.TryGetValue(member, out var pending)
                        || !ReferenceEquals(publication, pending))
                        return;
                    _pendingOrganizationNames.Remove(member);
                    if (member.Organization is not { } current || current.Id != orgId)
                        return;

                    OrganizationRecord renamed = current with { Name = name };
                    member.Organization = renamed;
                    member.Session?.Send(BuildOrgInfoPacket(member.Identity, renamed));
                    member.Session?.Send(OrgChatText.InfoUpdated(member.Identity));
                }
            });
        }

        // Retail rules (rules.txt), checked in the captured order: 4 characters minimum, at least 3 letters, only ASCII
        // letters, digits, spaces and the punctuation above, upper-case first letter.
        static OrgNameCheck CheckName(string name)
        {
            if (name.Length is < MinNameLength or > MaxNameLength)
                return OrgNameCheck.InvalidLength;
            if (name.Count(char.IsAsciiLetter) < MinNameLetters)
                return OrgNameCheck.TooFewLetters;
            if (!name.All(c => char.IsAsciiLetterOrDigit(c) || c == ' ' || NamePunctuation.Contains(c)))
                return OrgNameCheck.InvalidCharacter;
            return char.IsAsciiLetterUpper(name[0]) ? OrgNameCheck.Valid : OrgNameCheck.LowercaseInitial;
        }

        static bool IsValidName(string name) => CheckName(name) == OrgNameCheck.Valid;

        void ChangeText(OrganizationRecord org, OrganizationTextField field, string value)
        {
            _organizations.SaveText(org.Id, field, value);
            OrganizationRecord updated = field switch
            {
                OrganizationTextField.Description => org with { Description = value },
                OrganizationTextField.Objective => org with { Objective = value },
                _ => org with { History = value }
            };

            // Captured: every online member (leader included) receives the plain "Organization info updated." text.
            foreach (Player member in OnlineMembers(org.Id))
            {
                member.Organization = updated;
                member.Session?.Send(OrgChatText.InfoUpdated(member.Identity));
            }
        }

        void ChangeGoverningForm(IZoneSession session, Player player, OrganizationRecord org, string name)
        {
            GoverningFormCatalog forms = _gameData.GoverningForms;
            if (name.Length == 0)
            {
                // Retail lists the ids 1..5 first and the default form (0) last.
                string[] names = forms.Forms.OrderBy(pair => pair.Key == 0).ThenBy(pair => pair.Key)
                    .Select(pair => pair.Value.Name).ToArray();
                session.Send(OrgChatText.Message(player.Identity, OrgChatText.GoverningFormList(names)));
                return;
            }

            var chosen = forms.Forms.FirstOrDefault(pair => string.Equals(pair.Value.Name, name, StringComparison.OrdinalIgnoreCase));
            if (chosen.Value == null)
            {
                _logger.Info("/org governingform refused (unknown form) org=" + org.Id);
                return;
            }

            // The same form is accepted too: it rebuilds the hierarchy (every rank but the leader's is reset).
            int lowestRank = Math.Max(0, chosen.Value.Ranks.Length - 1);
            _organizations.SaveGoverningForm(org.Id, chosen.Key, player.Identity.Instance, lowestRank);
            OrganizationRecord updated = org with { GovernmentForm = chosen.Key };

            foreach (Player member in OnlineMembers(org.Id))
            {
                member.Organization = updated;
                if (!ReferenceEquals(member, player))
                    ResetRank(member, lowestRank);
            }

            _chat.SendOrganizationMessage(org.Id, OrgChatText.GoverningFormNotice(player.Name ?? string.Empty, chosen.Value.Name));
            SendRef(session, player, OrgChatText.Wrap(OrgChatRefs.GoverningFormChanged));
        }

        // Retail does not send a Stat for the reset, so the in-memory value is changed silently on the member's own tick.
        static void ResetRank(Player member, int rank)
        {
            void Apply() => member.Stats.Set(CharacterStat.ClanLevel, rank, StatDetail.Base);
            if (member.Playfield is { } playfield)
                playfield.DispatchPlayerProjection(member, Apply);
            else
                Apply();
        }

        System.Collections.Generic.IEnumerable<Player> OnlineMembers(int organizationId)
            => _playfieldManager.Value.SnapshotPlayers().Where(p => p.Organization?.Id == organizationId);
    }
}
