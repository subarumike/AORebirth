namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;

    using AORebirth.AccountBroker;
    using AORebirth.BotService;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class BotServicePersistenceTests
    {
        [TestMethod]
        [TestCategory("BotService")]
        public void PersistentCredentialUsesFoundationFormatAndVerifier()
        {
            PersistentBotCredentialIssuer issuer = new PersistentBotCredentialIssuer();
            BotCredentialRecord record;
            BotCredentialIssue issue = issuer.Issue(Guid.NewGuid(), 1, out record);

            Assert.IsTrue(issue.Credential.StartsWith("bot_v1_", StringComparison.Ordinal));
            Assert.IsTrue(issuer.Verify(record, issue.Credential));
            Assert.IsFalse(issuer.Verify(record, issue.Credential.Substring(0, issue.Credential.Length - 1) + "0"));
            Assert.AreEqual(16, record.Salt.Length);
            Assert.AreEqual(32, record.Verifier.Length);
        }

        [TestMethod]
        [TestCategory("BotService")]
        public void AccountManagementUsesAuthenticatedOwnerAndRevealsSecretOnce()
        {
            InMemoryPersistentBotRepository repository = new InMemoryPersistentBotRepository();
            BotAccountManagementService service = CreateManagement(repository, new AllowOrganizationAuthority());
            BotManagementContext owner = Context(101);

            BotManagementResult created = service.Create(
                owner,
                new BotManagementCreateRequest
                {
                    DisplayName = "OrgRelay",
                    OrganizationId = 77,
                    Scopes = BotScope.TellReceive | BotScope.OrganizationRead | BotScope.ChannelJoin,
                    RateLimitProfile = "org-default"
                });

            Assert.AreEqual(101L, created.Principal.OwningAccountId);
            Assert.IsNotNull(created.OneTimeCredential);
            Assert.IsNull(service.Get(owner, created.Principal.BotId).ToString().Contains("bot_v1_") ? "leaked" : null);
            Assert.AreEqual(1, service.List(owner).Length);
        }

        [TestMethod]
        [TestCategory("BotService")]
        public void AccountManagementRejectsCrossOwnerAccessAndUnauthorizedOrganization()
        {
            InMemoryPersistentBotRepository repository = new InMemoryPersistentBotRepository();
            BotAccountManagementService service = CreateManagement(repository, new DenyOrganizationAuthority());
            AssertThrows<InvalidOperationException>(() => service.Create(
                Context(101),
                new BotManagementCreateRequest
                {
                    DisplayName = "DeniedOrg",
                    OrganizationId = 77,
                    Scopes = BotScope.OrganizationRead
                }));

            service = CreateManagement(repository, new AllowOrganizationAuthority());
            BotManagementResult created = service.Create(
                Context(101),
                new BotManagementCreateRequest { DisplayName = "PrivateBot", Scopes = BotScope.TellReceive });
            AssertThrows<InvalidOperationException>(() => service.Get(Context(202), created.Principal.BotId));
        }

        [TestMethod]
        [TestCategory("BotService")]
        public void RotationRevokesOldCredentialAtomically()
        {
            InMemoryPersistentBotRepository repository = new InMemoryPersistentBotRepository();
            BotAccountManagementService service = CreateManagement(repository, new AllowOrganizationAuthority());
            BotManagementContext owner = Context(101);
            BotManagementResult created = service.Create(
                owner,
                new BotManagementCreateRequest { DisplayName = "RotateBot", Scopes = BotScope.TellReceive });
            BotManagementResult rotated = service.RotateCredential(owner, created.Principal.BotId);

            Assert.IsTrue(repository.FindCredential(created.PublicCredentialId).Revoked);
            Assert.IsFalse(repository.FindCredential(rotated.PublicCredentialId).Revoked);
            Assert.AreEqual(2, repository.FindPrincipal(created.Principal.BotId).CurrentCredentialVersion);
            Assert.AreNotEqual(created.OneTimeCredential, rotated.OneTimeCredential);
        }

        [TestMethod]
        [TestCategory("BotService")]
        public void ScopeReplaceAndRevokeInvalidateHostedState()
        {
            InMemoryPersistentBotRepository repository = new InMemoryPersistentBotRepository();
            BotAccountManagementService service = CreateManagement(repository, new AllowOrganizationAuthority());
            BotManagementContext owner = Context(101);
            BotManagementResult created = service.Create(
                owner,
                new BotManagementCreateRequest { DisplayName = "StateBot", Scopes = BotScope.TellReceive });

            service.UpdateScopes(owner, created.Principal.BotId, BotScope.TellReceive | BotScope.TellSend);
            Assert.AreEqual(BotScope.TellReceive | BotScope.TellSend, repository.FindPrincipal(created.Principal.BotId).Scopes);
            service.RevokeCredentials(owner, created.Principal.BotId);
            Assert.IsFalse(repository.FindPrincipal(created.Principal.BotId).Enabled);
            Assert.IsNull(repository.FindCurrentCredential(created.Principal.BotId));
        }

        [TestMethod]
        [TestCategory("BotService")]
        public void InboundTellIsScopeGatedAndDeliveredOnce()
        {
            BotInboundDeliveryQueue queue = new BotInboundDeliveryQueue();
            BotSession allowed = Session(Guid.NewGuid(), Guid.NewGuid(), BotScope.TellReceive, null);
            BotSession denied = Session(Guid.NewGuid(), Guid.NewGuid(), BotScope.TellSend, null);
            queue.Register(allowed, 0xE0000001);
            queue.Register(denied, 0xE0000002);

            Assert.IsTrue(queue.TryPublishTell(0xE0000001, 42, "Player", "hello", DateTime.UtcNow));
            Assert.IsFalse(queue.TryPublishTell(0xE0000002, 42, "Player", "blocked", DateTime.UtcNow));
            Assert.AreEqual("hello", BotInboundEventCodec.Decode(queue.Poll(allowed).Detail).Text);
            Assert.IsNull(queue.Poll(allowed).Detail);
        }

        [TestMethod]
        [TestCategory("BotService")]
        public void OrganizationDeliveryRequiresScopeSubscriptionAndMatchingOrganization()
        {
            BotInboundDeliveryQueue queue = new BotInboundDeliveryQueue();
            BotSession matching = Session(Guid.NewGuid(), Guid.NewGuid(), BotScope.OrganizationRead, 77);
            BotSession wrongOrganization = Session(Guid.NewGuid(), Guid.NewGuid(), BotScope.OrganizationRead, 88);
            queue.Register(matching, 0xE0000001);
            queue.Register(wrongOrganization, 0xE0000002);
            queue.Subscribe(matching, 3, 77);
            queue.Subscribe(wrongOrganization, 3, 77);

            Assert.AreEqual(1, queue.PublishChannel(3, 77, 42, "Player", "org", DateTime.UtcNow, 3));
            Assert.AreEqual(BotInboundEventKind.Organization, BotInboundEventCodec.Decode(queue.Poll(matching).Detail).Kind);
            Assert.IsNull(queue.Poll(wrongOrganization).Detail);
        }

        [TestMethod]
        [TestCategory("BotService")]
        public void ReconnectedSessionReplacesOldSessionWithoutDuplicateDelivery()
        {
            Guid botId = Guid.NewGuid();
            BotInboundDeliveryQueue queue = new BotInboundDeliveryQueue();
            BotSession first = Session(Guid.NewGuid(), botId, BotScope.TellReceive, null);
            BotSession second = Session(Guid.NewGuid(), botId, BotScope.TellReceive, null);
            queue.Register(first, 0xE0000001);
            queue.Register(second, 0xE0000001);
            queue.TryPublishTell(0xE0000001, 42, "Player", "once", DateTime.UtcNow);

            Assert.IsFalse(queue.Poll(first).Succeeded);
            Assert.AreEqual("once", BotInboundEventCodec.Decode(queue.Poll(second).Detail).Text);
            Assert.IsNull(queue.Poll(second).Detail);
        }

        [TestMethod]
        [TestCategory("BotService")]
        public void HostCycleRecoversAfterGatewayFailureAndKeepsOneSessionPerBot()
        {
            InMemoryPersistentBotRepository repository = new InMemoryPersistentBotRepository();
            AddEnabledBot(repository, 101, "HostedBot");
            FlakyGateway gateway = new FlakyGateway();
            RecordingEventSink sink = new RecordingEventSink();
            BotServiceHostLoop host = new BotServiceHostLoop(
                repository,
                gateway,
                sink,
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromMilliseconds(4));

            AssertThrows<InvalidOperationException>(() => host.RunCycle());
            Assert.AreEqual(1, host.RunCycle());
            Assert.AreEqual(1, host.SessionCount);
            Assert.AreEqual(1, sink.Events.Count);
        }

        private static BotAccountManagementService CreateManagement(
            IPersistentBotRepository repository,
            IBotOrganizationAuthority authority)
        {
            return new BotAccountManagementService(
                repository,
                new PersistentBotCredentialIssuer(),
                authority,
                new DefaultBotScopePolicy());
        }

        private static BotManagementContext Context(long identityId)
        {
            return new BotManagementContext { AuthenticatedIdentityId = identityId, AuditIdentity = "test:" + identityId };
        }

        private static BotSession Session(Guid sessionId, Guid botId, BotScope scopes, long? organizationId)
        {
            return new BotSession
            {
                SessionId = sessionId,
                BotId = botId,
                DisplayName = "Bot",
                OwningAccountId = 101,
                OrganizationId = organizationId,
                PublicCredentialId = new string('a', 32),
                CredentialVersion = 1,
                GrantedScopes = scopes,
                RateLimitProfile = "default",
                AuditIdentity = "test",
                EnabledSnapshot = true,
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        private static void AddEnabledBot(InMemoryPersistentBotRepository repository, long owner, string name)
        {
            DateTime now = DateTime.UtcNow;
            BotPrincipal principal = new BotPrincipal
            {
                BotId = Guid.NewGuid(),
                DisplayName = name,
                OwningAccountId = owner,
                Enabled = true,
                CurrentCredentialVersion = 1,
                Scopes = BotScope.TellReceive,
                RateLimitProfile = "default",
                AuditIdentity = "test",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            BotCredentialRecord credential;
            new PersistentBotCredentialIssuer().Issue(principal.BotId, 1, out credential);
            repository.Create(
                principal,
                credential,
                new BotAuditEvent
                {
                    Kind = BotAuditKind.PrincipalCreated,
                    BotId = principal.BotId,
                    AccountId = owner,
                    Succeeded = true,
                    TimestampUtc = now
                });
        }

        private static void AssertThrows<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            Assert.Fail("Expected exception " + typeof(T).Name + ".");
        }

        private sealed class AllowOrganizationAuthority : IBotOrganizationAuthority
        {
            public bool CanAssign(long authenticatedIdentityId, long organizationId)
            {
                return true;
            }
        }

        private sealed class DenyOrganizationAuthority : IBotOrganizationAuthority
        {
            public bool CanAssign(long authenticatedIdentityId, long organizationId)
            {
                return false;
            }
        }

        private sealed class FlakyGateway : IHostedBotChatGateway
        {
            private int calls;

            public BotInboundEvent Poll(BotSession session)
            {
                if (this.calls++ == 0)
                {
                    throw new InvalidOperationException("ChatEngine unavailable.");
                }

                return new BotInboundEvent
                {
                    EventId = Guid.NewGuid(),
                    Kind = BotInboundEventKind.Tell,
                    Text = "reconnected",
                    CreatedAtUtc = DateTime.UtcNow
                };
            }
        }

        private sealed class RecordingEventSink : IBotInboundEventSink
        {
            public readonly List<BotInboundEvent> Events = new List<BotInboundEvent>();

            public void Receive(BotPrincipal principal, BotInboundEvent inboundEvent)
            {
                this.Events.Add(inboundEvent);
            }
        }
    }
}
