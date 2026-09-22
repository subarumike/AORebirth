#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace AORebirth.Core.Components
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Runtime.CompilerServices;
    using System.Threading;

    using AORebirth.Core.EventHandlers.Events;

    using MemBus;
    using MemBus.Configurators;

    #endregion

    /// <summary>
    /// </summary>
    [Export(typeof(IBus))]
    public class MemBusAdapter : IBus
    {
        #region Fields

        /// <summary>
        /// </summary>
        private readonly MemBus.IBus memBus;

        /// <summary>
        /// </summary>
        private readonly SenderDispatchQueue nullSenderDispatchQueue = new SenderDispatchQueue();

        /// <summary>
        /// </summary>
        private readonly ConditionalWeakTable<object, SenderDispatchQueue> senderDispatchQueues =
            new ConditionalWeakTable<object, SenderDispatchQueue>();

#if AOREBIRTH_LINUX
        /// <summary>
        /// </summary>
        private readonly object dispatchSync = new object();

        /// <summary>
        /// </summary>
        private readonly ManualResetEventSlim dispatchIdle = new ManualResetEventSlim(true);

        /// <summary>
        /// </summary>
        private bool acceptingMessages = true;

        /// <summary>
        /// </summary>
        private int pendingMessages;
#endif

        /// <summary>
        /// </summary>
        private sealed class SenderDispatchQueue
        {
            /// <summary>
            /// </summary>
            internal readonly Queue<MessageReceivedEvent> Pending = new Queue<MessageReceivedEvent>();

            /// <summary>
            /// </summary>
            internal readonly object Sync = new object();

            /// <summary>
            /// </summary>
            internal bool Dispatching;
        }

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// </summary>
        /// <param name="iocAdapter">
        /// </param>
        [ImportingConstructor]
        public MemBusAdapter(IocAdapter iocAdapter)
        {
            this.memBus =
                BusSetup.StartWith<AsyncConfiguration>()
                    .Apply<IoCSupport>(s => s.SetAdapter(iocAdapter).SetHandlerInterface(typeof(IHandle<>)))
                    .Construct();
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <param name="message">
        /// </param>
        public void Publish(object message)
        {
            var receivedEvent = message as MessageReceivedEvent;
            if (receivedEvent == null)
            {
                this.memBus.Publish(message);
                return;
            }

#if AOREBIRTH_LINUX
            if (!this.TryRegisterDispatch())
            {
                throw new InvalidOperationException("LoginEngine message dispatch is stopping.");
            }
#endif

            SenderDispatchQueue dispatchQueue = receivedEvent.Sender == null
                                                     ? this.nullSenderDispatchQueue
                                                     : this.senderDispatchQueues.GetValue(
                                                         receivedEvent.Sender,
                                                         key => new SenderDispatchQueue());
            if (!receivedEvent.TrySetDispatchCompletion(
                () => this.CompleteOrderedDispatch(dispatchQueue, receivedEvent)))
            {
#if AOREBIRTH_LINUX
                this.CompleteTrackedDispatch();
#endif
                throw new InvalidOperationException("LoginEngine message dispatch was already registered.");
            }

            bool startDispatch;
            lock (dispatchQueue.Sync)
            {
                dispatchQueue.Pending.Enqueue(receivedEvent);
                startDispatch = !dispatchQueue.Dispatching;
                if (startDispatch)
                {
                    dispatchQueue.Dispatching = true;
                }
            }

            if (startDispatch)
            {
                this.PublishReceivedEvent(receivedEvent);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="dispatchQueue">
        /// </param>
        /// <param name="completedEvent">
        /// </param>
        private void CompleteOrderedDispatch(
            SenderDispatchQueue dispatchQueue,
            MessageReceivedEvent completedEvent)
        {
            MessageReceivedEvent nextEvent = null;
            lock (dispatchQueue.Sync)
            {
                if (dispatchQueue.Pending.Count > 0
                    && object.ReferenceEquals(dispatchQueue.Pending.Peek(), completedEvent))
                {
                    dispatchQueue.Pending.Dequeue();
                }

                if (dispatchQueue.Pending.Count > 0)
                {
                    nextEvent = dispatchQueue.Pending.Peek();
                }
                else
                {
                    dispatchQueue.Dispatching = false;
                }
            }

#if AOREBIRTH_LINUX
            this.CompleteTrackedDispatch();
#endif

            if (nextEvent != null)
            {
                this.PublishReceivedEvent(nextEvent);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="receivedEvent">
        /// </param>
        private void PublishReceivedEvent(MessageReceivedEvent receivedEvent)
        {
            try
            {
                this.memBus.Publish(receivedEvent);
            }
            catch
            {
                receivedEvent.CompleteDispatch();
                throw;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="action">
        /// </param>
        /// <typeparam name="T">
        /// </typeparam>
        /// <returns>
        /// </returns>
        public IDisposable Subscribe<T>(Action<T> action)
        {
            return this.memBus.Subscribe(action);
        }

#if AOREBIRTH_LINUX
        /// <summary>
        /// </summary>
        internal void StopAcceptingMessages()
        {
            lock (this.dispatchSync)
            {
                this.acceptingMessages = false;
                if (this.pendingMessages == 0)
                {
                    this.dispatchIdle.Set();
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="timeout">
        /// </param>
        /// <returns>
        /// </returns>
        internal bool WaitForIdle(TimeSpan timeout)
        {
            return this.dispatchIdle.Wait(timeout);
        }

        /// <summary>
        /// </summary>
        private void CompleteTrackedDispatch()
        {
            lock (this.dispatchSync)
            {
                if (this.pendingMessages <= 0)
                {
                    return;
                }

                this.pendingMessages--;
                if (this.pendingMessages == 0)
                {
                    this.dispatchIdle.Set();
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        private bool TryRegisterDispatch()
        {
            lock (this.dispatchSync)
            {
                if (!this.acceptingMessages)
                {
                    return false;
                }

                this.pendingMessages++;
                this.dispatchIdle.Reset();
                return true;
            }
        }
#endif

        #endregion
    }
}
