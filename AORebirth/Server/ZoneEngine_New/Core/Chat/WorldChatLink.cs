namespace ZoneEngine_New.Core.Chat
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;

    using AORebirth.World.Contracts.V1;

    using Grpc.Core;

    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Teams;
    using ZoneEngine_New.Core.World;

    public interface IChatEngineLink : IDisposable
    {
        void Start();

        /// <summary>A command string for ChatEngine about one character (team channels, LFT location).</summary>
        bool SendChatCommand(int characterId, string command);

        /// <summary>Vicinity chat for ChatEngine to deliver to the recipients it names.</summary>
        bool SendVicinity(int senderId, string? senderName, byte messageType, string text, IReadOnlyList<int> recipients);

        /// <summary>
        /// A vicinity line from no one (chat-server type 0x23) for ChatEngine to deliver to <paramref name="recipients"/>.
        /// False when it could not be handed to World.
        /// </summary>
        bool SendAnonymousVicinity(string text, IReadOnlyList<int> recipients, byte[]? blob = null);

        /// <summary>Delivers a server-generated notice to the existing Org Msg channel.</summary>
        bool SendOrganizationMessage(int organizationId, string text);
    }

    /// <summary>
    /// Zone and ChatEngine talk through World: this instance publishes to ChatEngine, and ChatEngine's commands for
    /// a character arrive here when this instance owns it.
    /// </summary>
    public sealed class WorldChatLink : IChatEngineLink
    {
        static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(3);

        readonly WorldLink _world;
        readonly IZoneLogger _logger;
        readonly Lazy<TeamService> _teams;
        readonly Lazy<Playfield.PlayfieldManager> _playfields;
        readonly Lazy<Commands.GmCommandDispatcher> _gmCommands;
        bool _started;
        bool _disposed;
        long _lastFailureLogMs;

        public WorldChatLink(
            WorldLink world,
            IZoneLogger logger,
            Lazy<TeamService> teams,
            Lazy<Playfield.PlayfieldManager> playfields,
            Lazy<Commands.GmCommandDispatcher> gmCommands)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _teams = teams ?? throw new ArgumentNullException(nameof(teams));
            _playfields = playfields ?? throw new ArgumentNullException(nameof(playfields));
            _gmCommands = gmCommands ?? throw new ArgumentNullException(nameof(gmCommands));
        }

        public void Start()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started) return;
            _world.WorldMessageReceived += OnWorldMessage;
            _started = true;
        }

        public bool SendChatCommand(int characterId, string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return false;
            return Publish(new ChatEvent { ToCharacter = new CharacterChatCommand { CharacterId = characterId, Command = command } });
        }

        public bool SendVicinity(int senderId, string? senderName, byte messageType, string text, IReadOnlyList<int> recipients)
        {
            var vicinity = new VicinityMessage
            {
                Sender = senderId,
                SenderName = senderName ?? string.Empty,
                ChannelType = messageType,
                Text = text ?? string.Empty
            };
            vicinity.Recipients.AddRange(recipients);
            return Publish(new ChatEvent { Vicinity = vicinity });
        }

        public bool SendOrganizationMessage(int organizationId, string text)
        {
            if (organizationId <= 0 || string.IsNullOrEmpty(text)) return false;
            return Publish(new ChatEvent
            {
                Organization = new OrganizationMessage
                {
                    OrganizationId = organizationId,
                    Text = text
                }
            });
        }

        public bool SendAnonymousVicinity(string text, IReadOnlyList<int> recipients, byte[]? blob = null)
        {
            var vicinity = new VicinityMessage { Anonymous = true, Text = text ?? string.Empty };
            if (blob is { Length: > 0 })
                vicinity.Payload = Google.Protobuf.ByteString.CopyFrom(blob);
            vicinity.Recipients.AddRange(recipients);
            return Publish(new ChatEvent { Vicinity = vicinity });
        }

        /// <summary>On the registry stream, so lines arrive in the order they were said.</summary>
        bool Publish(ChatEvent message)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_started) return false;
            if (_world.PublishChat(message)) return true;
            long now = Environment.TickCount64;
            if (now - _lastFailureLogMs > 10_000)
            {
                _lastFailureLogMs = now;
                _logger.Warn("Chat not sent: World is unreachable");
            }

            return false;
        }

        void OnWorldMessage(WorldToService message)
        {
            if (message.BodyCase != WorldToService.BodyOneofCase.Chat
                || message.Chat.BodyCase != ChatEvent.BodyOneofCase.ToCharacter) return;
            CharacterChatCommand command = message.Chat.ToCharacter;
            try
            {
                // A ".command" typed in a chat-server channel or tell: run it as if said in vicinity chat.
                if (command.Command.StartsWith('.'))
                    RunGmCommand(command.CharacterId, command.Command);
                else
                    _teams.Value.TryHandleInboundChatCommand(command.CharacterId, command.Command);
            }
            catch (Exception exception)
            {
                _logger.Warn(string.Format(CultureInfo.InvariantCulture, "Inbound chat command failed: {0}", exception.Message));
            }
        }

        /// <summary>
        /// Runs a relayed GM command on the player's own tick, with the same session and GM level checks as one typed in
        /// vicinity chat. Dropped when this instance no longer runs the character or it is not in play.
        /// </summary>
        void RunGmCommand(int characterId, string text)
        {
            if (!_playfields.Value.FindPlayer(characterId, out Entities.Player player)
                || player.Playfield is not Playfield.Playfield at)
                return;

            at.DispatchPlayerProjection(player, () =>
            {
                if (player.Session is not Network.IZoneSession session || session.State != Network.SessionState.InPlay
                    || !ReferenceEquals(player.Playfield, at) || player.IsPersistenceQuarantined)
                    return;
                _gmCommands.Value.TryExecuteText(session, player, text);
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_started) _world.WorldMessageReceived -= OnWorldMessage;
        }
    }
}
