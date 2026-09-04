namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;

    public sealed class GmCommandContext
    {
        public GmCommandContext(IZoneSession session, Player player, string[] args)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Args = args ?? Array.Empty<string>();
        }

        public IZoneSession Session { get; }

        public Player Player { get; }

        public string[] Args { get; }
    }

    public interface IGmCommand
    {
        string Name { get; }

        int RequiredGmLevel { get; }

        string Usage { get; }

        void Execute(GmCommandContext context);
    }

    public static class GmCommandFeedback
    {
        public static void Send(IZoneSession session, Player player, string text)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(text);

            session.Send(
                new ChatTextMessage
                {
                    Identity = player.Identity,
                    Text = text,
                    Unknown1 = 0,
                    Unknown2 = 0,
                    Unknown3 = 0
                });
        }
    }

    public sealed class GmCommandDispatcher
    {
        private readonly Dictionary<string, IGmCommand> _commands;

        public GmCommandDispatcher(IEnumerable<IGmCommand> commands)
        {
            ArgumentNullException.ThrowIfNull(commands);
            _commands = new Dictionary<string, IGmCommand>(StringComparer.OrdinalIgnoreCase);
            foreach (IGmCommand command in commands)
            {
                ArgumentNullException.ThrowIfNull(command);
                _commands[command.Name] = command;
            }
        }

        public void TryExecute(IZoneSession session, Player player, string name, string[] args)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(name);
            args ??= Array.Empty<string>();

            if (!_commands.TryGetValue(name, out IGmCommand? command))
            {
                GmCommandFeedback.Send(session, player, "Unknown command: ." + name);
                return;
            }

            if (!player.Stats.TryGetValue(CharacterStat.GmLevel, out int gmLevel) || gmLevel < command.RequiredGmLevel)
            {
                GmCommandFeedback.Send(session, player, "Insufficient GM level.");
                return;
            }

            try
            {
                command.Execute(new GmCommandContext(session, player, args));
            }
            catch (Exception exception)
            {
                GmCommandFeedback.Send(
                    session,
                    player,
                    string.Format(CultureInfo.InvariantCulture, "Command failed: {0}", exception.Message));
            }
        }
    }
}
