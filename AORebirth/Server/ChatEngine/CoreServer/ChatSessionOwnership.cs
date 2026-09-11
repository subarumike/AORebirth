namespace ChatEngine.CoreServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>Serializes accepted chat owners and their disconnect side effects.</summary>
    internal sealed class ChatSessionOwnership<TClient> where TClient : class
    {
        private readonly object sync = new object();
        private readonly ConcurrentDictionary<uint, TClient> clients = new ConcurrentDictionary<uint, TClient>();
        private readonly ConditionalWeakTable<TClient, object> retired = new ConditionalWeakTable<TClient, object>();

        public IReadOnlyDictionary<uint, TClient> Clients { get { return this.clients; } }

        public bool Register(uint characterId, TClient client, Action markOnline)
        {
            if (client == null) throw new ArgumentNullException("client");
            lock (this.sync)
            {
                object closed;
                if (this.retired.TryGetValue(client, out closed)) return false;
                if (markOnline != null) markOnline();
                TClient previous;
                if (this.clients.TryGetValue(characterId, out previous) && !ReferenceEquals(previous, client))
                    this.retired.GetValue(previous, ignored => new object());
                this.clients[characterId] = client;
                return true;
            }
        }

        public bool Disconnect(uint characterId, TClient client, Action<uint> clearOnline, Action<uint> removeLocalState)
        {
            if (client == null) throw new ArgumentNullException("client");
            lock (this.sync)
            {
                this.retired.GetValue(client, ignored => new object());
                TClient current;
                if (!this.clients.TryGetValue(characterId, out current) || !ReferenceEquals(current, client))
                    return false;

                // Registration cannot publish a replacement or mark it online while this
                // cleanup runs. The caller additionally guards any live zone ownership.
                clearOnline(characterId);
                removeLocalState(characterId);
                TClient removed;
                this.clients.TryRemove(characterId, out removed);
                return true;
            }
        }
    }
}
