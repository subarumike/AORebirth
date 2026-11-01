namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;
    using System.Linq;

    using AORebirth.Interfaces.Persistence.Characters;

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
        const int MaxNameLength = 40;
        const int MinNameLetters = 3;

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

            ChangeText(org, OrganizationTextField.Name, name);
        }

        // Retail rules (rules.txt): upper-case first letter, 4-40 characters, at least 3 letters.
        static bool IsValidName(string name)
            => name.Length is >= MinNameLength and <= MaxNameLength
               && char.IsUpper(name[0])
               && name.Count(char.IsLetter) >= MinNameLetters;

        void ChangeText(OrganizationRecord org, OrganizationTextField field, string value)
        {
            _organizations.SaveText(org.Id, field, value);
            OrganizationRecord updated = field switch
            {
                OrganizationTextField.Name => org with { Name = value },
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
