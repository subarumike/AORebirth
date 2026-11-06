namespace ChatEngine.Channels
{
    using System.Collections.Generic;

    using Cell.Core;

    using ChatEngine.CoreClient;

    /// <summary>A static channel from GameData ChatChannels.json; a client joins only when its conditions all pass.</summary>
    public sealed class ConfiguredChannel : ChannelBase
    {
        private readonly IReadOnlyList<ChannelCondition> conditions;

        public ConfiguredChannel(ChannelDefinition definition)
            : base(definition.Flags, definition.Type, definition.Id, definition.Name)
        {
            this.conditions = definition.Conditions;
        }

        public bool CanJoin(ChannelJoinContext context)
        {
            return ChannelCondition.AllMet(this.conditions, context);
        }

        /// <summary>Joins when the conditions pass, reading the character's values from <paramref name="context"/>.</summary>
        public bool AddClient(Client client, ChannelJoinContext context)
        {
            return this.CanJoin(context) && base.AddClient(client);
        }

        public override bool AddClient(IClient client)
        {
            Client chatClient = (Client)client;
            return this.AddClient(chatClient, new ChannelJoinContext(chatClient));
        }
    }
}
