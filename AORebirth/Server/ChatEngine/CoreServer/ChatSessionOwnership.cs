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
        private readonly Dictionary<uint, IDisposable> ownershipLeases = new Dictionary<uint, IDisposable>();
        private readonly ConditionalWeakTable<TClient, object> retired = new ConditionalWeakTable<TClient, object>();

        public IReadOnlyDictionary<uint, TClient> Clients { get { return this.clients; } }

        public bool Register(uint characterId, TClient client, Func<IDisposable> acquireOwnership)
        {
            if (client == null) throw new ArgumentNullException("client");
            lock (this.sync)
            {
                object closed;
                if (this.retired.TryGetValue(client, out closed)) return false;
                foreach (var registered in this.clients)
                    if (registered.Key != characterId && ReferenceEquals(registered.Value, client))
                        throw new InvalidOperationException("A chat session already owns another character.");
                // Acquire before retiring the current owner. A failed online write or
                // lease acquisition must leave the accepted owner and its lease intact.
                IDisposable acquired = acquireOwnership == null ? null : acquireOwnership();
                if (acquireOwnership != null && acquired == null)
                    throw new InvalidOperationException("Chat ownership acquisition returned no lease.");
                TClient previous;
                IDisposable previousLease;
                this.ownershipLeases.TryGetValue(characterId, out previousLease);
                if (this.clients.TryGetValue(characterId, out previous) && !ReferenceEquals(previous, client))
                    this.retired.GetValue(previous, ignored => new object());
                this.clients[characterId] = client;
                if (acquired == null) this.ownershipLeases.Remove(characterId);
                else this.ownershipLeases[characterId] = acquired;
                if (previousLease != null) previousLease.Dispose();
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
                {
                    // A failed repeated selection may have changed the client's character
                    // field. Release only the registration actually owned by this object.
                    bool found = false;
                    foreach (var registered in this.clients)
                        if (ReferenceEquals(registered.Value, client))
                        {
                            characterId = registered.Key;
                            found = true;
                            break;
                        }
                    if (!found) return false;
                }

                // Registration cannot publish a replacement or mark it online while this
                // cleanup runs. The caller additionally guards any live zone ownership.
                IDisposable ownership;
                this.ownershipLeases.TryGetValue(characterId, out ownership);
                this.ownershipLeases.Remove(characterId);
                try
                {
                    // Release this chat owner's reference before guarded cleanup. A
                    // replacement/zone lease still prevents its Online state being cleared.
                    if (ownership != null) ownership.Dispose();
                    clearOnline(characterId);
                    return true;
                }
                finally
                {
                    try { removeLocalState(characterId); }
                    finally
                    {
                        TClient removed;
                        this.clients.TryRemove(characterId, out removed);
                    }
                }
            }
        }
    }
}
