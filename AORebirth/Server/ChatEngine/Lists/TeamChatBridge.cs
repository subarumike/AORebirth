namespace ChatEngine.Lists
{
    using System;

    using AORebirth.Communication.Messages;

    using Cell.Core;

    using ChatEngine.Channels;
    using ChatEngine.CoreClient;
    using ChatEngine.CoreServer;

    /// <summary>
    /// ZoneEngine → ChatEngine team/raid channel wiring (capture 20260902-073932).
    /// </summary>
    internal static class TeamChatBridge
    {
        public const string TeamJoinPrefix = "#aorebirth-team-join";

        public const string RaidConvertPrefix = "#aorebirth-raid-convert";

        public const string TeamLeavePrefix = "#aorebirth-team-leave";

        public static bool TryHandle(ChatServer server, ChatCommand chatCommand)
        {
            if (server == null || chatCommand == null || string.IsNullOrWhiteSpace(chatCommand.ChatCommandString))
            {
                return false;
            }

            string text = chatCommand.ChatCommandString.Trim();
            if (text.StartsWith(TeamJoinPrefix, StringComparison.Ordinal))
            {
                return HandleTeamJoin(server, chatCommand, text);
            }

            if (text.StartsWith(RaidConvertPrefix, StringComparison.Ordinal))
            {
                return HandleRaidConvert(server, chatCommand, text);
            }

            if (text.StartsWith(TeamLeavePrefix, StringComparison.Ordinal))
            {
                return HandleTeamLeave(server, chatCommand, text);
            }

            return false;
        }

        private static bool HandleTeamJoin(ChatServer server, ChatCommand chatCommand, string text)
        {
            uint teamId;
            if (!TryParseTeamId(text, out teamId))
            {
                return true;
            }

            Client client = ResolveClient(server, chatCommand.CharacterId);
            if (client == null)
            {
                return true;
            }

            TeamChannel channel = GetOrCreateTeamChannel(server, teamId);
            channel.AddClient(client);
            return true;
        }

        private static bool HandleRaidConvert(ChatServer server, ChatCommand chatCommand, string text)
        {
            uint teamId;
            if (!TryParseTeamId(text, out teamId))
            {
                return true;
            }

            RaidChannel raidChannel = GetOrCreateRaidChannel(server, teamId);
            TeamChannel teamChannel = FindChannel(server, ChannelType.Team, teamId) as TeamChannel;
            if (teamChannel != null)
            {
                foreach (IClient connected in teamChannel.SnapshotConnectedClients())
                {
                    Client client = connected as Client;
                    if (client == null)
                    {
                        continue;
                    }

                    teamChannel.RemoveClient(client);
                    client.Channels.Remove(teamChannel);
                    raidChannel.AddClient(client);
                }
            }

            // Capture: every convert notifies each teammate's chat. Do not rely on Team
            // channel membership alone — cross-PF members may miss Team join.
            Client commandClient = ResolveClient(server, chatCommand.CharacterId);
            if (commandClient != null)
            {
                RemoveClientFromChannel(commandClient, ChannelType.Team, teamId);
                raidChannel.AddClient(commandClient);
            }

            return true;
        }

        private static bool HandleTeamLeave(ChatServer server, ChatCommand chatCommand, string text)
        {
            uint teamId;
            if (!TryParseTeamId(text, out teamId))
            {
                return true;
            }

            Client client = ResolveClient(server, chatCommand.CharacterId);
            if (client == null)
            {
                return true;
            }

            RemoveClientFromChannel(client, ChannelType.Team, teamId);
            RemoveClientFromChannel(client, ChannelType.Raid, teamId);
            return true;
        }

        private static bool TryParseTeamId(string text, out uint teamId)
        {
            teamId = 0;
            string[] parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            int parsed;
            if (!int.TryParse(parts[1], out parsed) || parsed <= 0)
            {
                return false;
            }

            teamId = unchecked((uint)parsed);
            return true;
        }

        private static Client ResolveClient(ChatServer server, int characterId)
        {
            if (characterId <= 0)
            {
                return null;
            }

            Client client;
            if (server.ConnectedClients.TryGetValue(unchecked((uint)characterId), out client))
            {
                return client;
            }

            return null;
        }

        private static ChannelBase FindChannel(ChatServer server, ChannelType type, uint channelId)
        {
            foreach (ChannelBase channel in server.Channels)
            {
                if (channel.channelType == type && channel.ChannelId == channelId)
                {
                    return channel;
                }
            }

            return null;
        }

        private static TeamChannel GetOrCreateTeamChannel(ChatServer server, uint teamId)
        {
            TeamChannel existing = FindChannel(server, ChannelType.Team, teamId) as TeamChannel;
            if (existing != null)
            {
                return existing;
            }

            var channel = new TeamChannel(teamId);
            server.Channels.Add(channel);
            return channel;
        }

        private static RaidChannel GetOrCreateRaidChannel(ChatServer server, uint teamId)
        {
            RaidChannel existing = FindChannel(server, ChannelType.Raid, teamId) as RaidChannel;
            if (existing != null)
            {
                return existing;
            }

            var channel = new RaidChannel(teamId);
            server.Channels.Add(channel);
            return channel;
        }

        private static void RemoveClientFromChannel(Client client, ChannelType type, uint channelId)
        {
            ChannelBase found = null;
            foreach (ChannelBase channel in client.Channels)
            {
                if (channel.channelType == type && channel.ChannelId == channelId)
                {
                    found = channel;
                    break;
                }
            }

            if (found == null)
            {
                return;
            }

            found.RemoveClient(client);
            client.Channels.Remove(found);
        }
    }
}
