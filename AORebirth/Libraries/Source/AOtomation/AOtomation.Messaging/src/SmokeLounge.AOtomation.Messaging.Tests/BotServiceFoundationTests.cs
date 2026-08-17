namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Linq;
    using System.Net;

    using AORebirth.BotService;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BotServiceFoundationTests
    {
        private static readonly DateTime Now = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void CredentialAuthenticationSupportsFailureRotationAndRevocationContracts()
        {
            Fixture fixture = CreateFixture(BotScope.TellSend, 42, 10);
            Assert.IsTrue(fixture.Authenticator.Authenticate(fixture.Issue.Credential).Succeeded);

            char replacement = fixture.Issue.Credential[fixture.Issue.Credential.Length - 1] == '0' ? '1' : '0';
            string wrongSecret = fixture.Issue.Credential.Substring(0, fixture.Issue.Credential.Length - 1) + replacement;
            Assert.AreNotEqual(fixture.Issue.Credential, wrongSecret);
            Assert.IsFalse(fixture.Authenticator.Authenticate(wrongSecret).Succeeded);

            fixture.Credentials.SetEnabled(fixture.Principal.BotId, false);
            Assert.AreEqual(
                BotAuthenticationFailure.DisabledBot,
                fixture.Authenticator.Authenticate(fixture.Issue.Credential).Failure);
            fixture.Credentials.SetEnabled(fixture.Principal.BotId, true);

            BotCredentialIssue rotated = fixture.Credentials.Rotate(fixture.Principal.BotId);
            Assert.IsFalse(fixture.Authenticator.Authenticate(fixture.Issue.Credential).Succeeded);
            Assert.IsTrue(fixture.Authenticator.Authenticate(rotated.Credential).Succeeded);

            fixture.Credentials.Revoke(rotated.PublicCredentialId);
            Assert.AreEqual(
                BotAuthenticationFailure.RevokedCredential,
                fixture.Authenticator.Authenticate(rotated.Credential).Failure);
        }

        [TestMethod]
        public void CredentialsUseDocumentedShapeAndNeverRenderSecretDiagnostics()
        {
            Fixture fixture = CreateFixture(BotScope.TellSend, null, 10);
            StringAssert.StartsWith(fixture.Issue.Credential, "bot_v1_");
            Assert.AreEqual(4, fixture.Issue.Credential.Split('_').Length);
            string secret = fixture.Issue.Credential.Split('_')[3];
            Assert.IsFalse(fixture.Issue.ToString().Contains(secret));
            Assert.IsFalse(fixture.Authenticator.Authenticate(fixture.Issue.Credential).ToString().Contains(secret));
            Assert.IsFalse(string.Join("\n", fixture.Audit.Events.Select(item => item.ToString())).Contains(secret));
            StringAssert.Contains(BotCredentialManager.CredentialFormat, "64 lowercase hex secret");
        }

        [TestMethod]
        public void AuthorizationIsTypedDenyByDefaultAndOrganizationBound()
        {
            BotAuthorizationEvaluator evaluator = new BotAuthorizationEvaluator();
            BotSession session = Session(BotScope.TellSend | BotScope.OrganizationSend | BotScope.ChannelJoin, 42);

            Assert.IsTrue(evaluator.Authorize(session, BotChatRequest.Tell(123, "hello")).Allowed);
            Assert.IsFalse(evaluator.Authorize(Session(BotScope.None, 42), BotChatRequest.Tell(123, "hello")).Allowed);
            Assert.IsTrue(evaluator.Authorize(session, BotChatRequest.Organization(42, "hello")).Allowed);
            Assert.AreEqual(
                "ORGANIZATION_MISMATCH",
                evaluator.Authorize(session, BotChatRequest.Organization(43, "hello")).ReasonCode);
            Assert.AreEqual(
                "SCOPE_REQUIRED",
                evaluator.Authorize(Session(BotScope.None, 42), BotChatRequest.Organization(42, "hello")).ReasonCode);
            Assert.AreEqual(
                "OPERATION_NOT_AUTHORIZED",
                evaluator.Authorize(session, new BotChatRequest { Operation = BotOperation.Unknown }).ReasonCode);
        }

        [TestMethod]
        public void ChannelAndRosterScopesAreCheckedPerOperation()
        {
            BotAuthorizationEvaluator evaluator = new BotAuthorizationEvaluator();
            BotChatRequest join = new BotChatRequest { Operation = BotOperation.ChannelJoin, ChannelType = 7, ChannelId = 1 };
            BotChatRequest read = new BotChatRequest { Operation = BotOperation.ChannelRead, ChannelType = 7, ChannelId = 1 };
            BotChatRequest send = new BotChatRequest { Operation = BotOperation.ChannelSend, ChannelType = 7, ChannelId = 1 };
            BotChatRequest roster = new BotChatRequest { Operation = BotOperation.RosterRead };

            Assert.IsTrue(evaluator.Authorize(Session(BotScope.ChannelJoin, null), join).Allowed);
            Assert.IsFalse(evaluator.Authorize(Session(BotScope.ChannelJoin, null), read).Allowed);
            Assert.IsTrue(evaluator.Authorize(Session(BotScope.ChannelSend, null), send).Allowed);
            Assert.IsTrue(evaluator.Authorize(Session(BotScope.RosterRead, null), roster).Allowed);
            Assert.IsFalse(evaluator.Authorize(Session(BotScope.None, null), roster).Allowed);
        }

        [TestMethod]
        public void SessionRetainsBotIdentityAndRevocationInvalidatesIt()
        {
            Fixture fixture = CreateFixture(BotScope.TellSend, 42, 10);
            BotSession session = fixture.Sessions.AuthenticateAndCreate(fixture.Issue.Credential);
            Assert.IsNotNull(session);
            Assert.AreEqual(fixture.Principal.BotId, session.BotId);
            Assert.AreEqual(fixture.Principal.OwningAccountId, session.OwningAccountId);
            Assert.AreEqual(fixture.Principal.OrganizationId, session.OrganizationId);
            Assert.IsTrue(fixture.Sessions.Validate(session).Succeeded);

            fixture.Credentials.Revoke(fixture.Issue.PublicCredentialId);
            Assert.AreEqual("CREDENTIAL_REVOKED_OR_MISSING", fixture.Sessions.Validate(session).ReasonCode);
            Assert.IsNull(fixture.Sessions.AuthenticateAndCreate(fixture.Issue.Credential));
        }

        [TestMethod]
        public void RuntimeAllowsScopedTellAndOrganizationOperationsAndAuditsDenials()
        {
            Fixture fixture = CreateFixture(BotScope.TellSend | BotScope.OrganizationSend, 42, 10);
            BotSession session = fixture.Sessions.AuthenticateAndCreate(fixture.Issue.Credential);
            Assert.IsTrue(fixture.Runtime.Execute(session, BotChatRequest.Tell(123, "hello")).Succeeded);
            Assert.IsTrue(fixture.Runtime.Execute(session, BotChatRequest.Organization(42, "org")).Succeeded);

            session.GrantedScopes = BotScope.None;
            Assert.AreEqual(
                "SCOPE_REQUIRED",
                fixture.Runtime.Execute(session, BotChatRequest.Tell(123, "denied")).ReasonCode);
            Assert.IsTrue(fixture.Audit.Events.Any(item => item.Kind == BotAuditKind.PermissionDenied));
            Assert.IsTrue(fixture.Audit.Events.Any(item => item.Kind == BotAuditKind.TellSend && item.Succeeded));
            Assert.IsTrue(fixture.Audit.Events.Any(item => item.Kind == BotAuditKind.OrganizationMessageSend && item.Succeeded));
        }

        [TestMethod]
        public void RateLimitsAreControlledAndSeparatedByBotIdentity()
        {
            InMemoryBotRateLimitPolicyResolver policies = new InMemoryBotRateLimitPolicyResolver();
            policies.SetRule("standard", BotOperation.TellSend, 1, TimeSpan.FromMinutes(1));
            InMemoryBotRateLimiter limiter = new InMemoryBotRateLimiter(policies);
            Guid first = Guid.NewGuid();
            Guid second = Guid.NewGuid();

            Assert.IsTrue(limiter.TryAcquire(first, "standard", BotOperation.TellSend, Now).Allowed);
            Assert.IsFalse(limiter.TryAcquire(first, "standard", BotOperation.TellSend, Now).Allowed);
            Assert.IsTrue(limiter.TryAcquire(second, "standard", BotOperation.TellSend, Now).Allowed);
        }

        [TestMethod]
        public void PrivateLoopbackProtocolAuthenticatesAndPreservesBotContext()
        {
            byte[] key = new byte[32];
            for (int index = 0; index < key.Length; index++)
            {
                key[index] = (byte)(index + 1);
            }

            BotSession observedSession = null;
            BotChatRequest observedRequest = null;
            using (BotPrivateTcpServer server = new BotPrivateTcpServer(
                new IPEndPoint(IPAddress.Loopback, 0),
                key,
                new Handler((session, request) =>
                {
                    observedSession = session;
                    observedRequest = request;
                    return BotOperationResult.Allowed("ROUTED");
                })))
            {
                server.Start();
                BotSession session = Session(BotScope.TellSend, 42);
                BotOperationResult result = new BotPrivateTcpClient(server.BoundEndpoint, key)
                    .Execute(session, BotChatRequest.Tell(123, "hello"));

                Assert.IsTrue(result.Succeeded);
                Assert.AreEqual(session.BotId, observedSession.BotId);
                Assert.AreEqual(session.OwningAccountId, observedSession.OwningAccountId);
                Assert.AreEqual(BotOperation.TellSend, observedRequest.Operation);
                Assert.AreEqual((uint)123, observedRequest.TargetCharacterId);
            }
        }

        [TestMethod]
        public void PrivateTransportRejectsNonLoopbackAndWeakServiceKeys()
        {
            AssertThrows<ArgumentException>(
                () => new BotPrivateTcpClient(new IPEndPoint(IPAddress.Any, 7512), new byte[32]));
            AssertThrows<ArgumentException>(
                () => new BotPrivateTcpClient(new IPEndPoint(IPAddress.Loopback, 7512), new byte[16]));
        }

        private static Fixture CreateFixture(BotScope scopes, long? organizationId, int limit)
        {
            InMemoryBotIdentityRepository repository = new InMemoryBotIdentityRepository();
            RecordingBotAuditSink audit = new RecordingBotAuditSink();
            BotCredentialManager credentials = new BotCredentialManager(repository, () => Now);
            BotPrincipal principal = new BotPrincipal
            {
                BotId = Guid.NewGuid(),
                DisplayName = "OrgHelper",
                OwningAccountId = 1001,
                OrganizationId = organizationId,
                Enabled = true,
                Scopes = scopes,
                RateLimitProfile = "standard",
                AuditIdentity = "bot:org-helper"
            };
            BotCredentialIssue issue = credentials.CreateInitial(principal);
            BotCredentialAuthenticator authenticator = new BotCredentialAuthenticator(repository, audit, () => Now);
            BotSessionService sessions = new BotSessionService(repository, authenticator, audit, () => Now);
            InMemoryBotRateLimitPolicyResolver policies = new InMemoryBotRateLimitPolicyResolver();
            foreach (BotOperation operation in Enum.GetValues(typeof(BotOperation)))
            {
                if (operation != BotOperation.Unknown)
                {
                    policies.SetRule("standard", operation, limit, TimeSpan.FromMinutes(1));
                }
            }

            BotRuntime runtime = new BotRuntime(
                sessions,
                new BotAuthorizationEvaluator(),
                new InMemoryBotRateLimiter(policies),
                new DelegatingBotChatGateway((session, request) => BotOperationResult.Allowed("TEST_GATEWAY")),
                audit,
                () => Now);
            return new Fixture
            {
                Repository = repository,
                Audit = audit,
                Credentials = credentials,
                Authenticator = authenticator,
                Sessions = sessions,
                Principal = principal,
                Issue = issue,
                Runtime = runtime
            };
        }

        private static BotSession Session(BotScope scopes, long? organizationId)
        {
            return new BotSession
            {
                SessionId = Guid.NewGuid(),
                BotId = Guid.NewGuid(),
                DisplayName = "OrgHelper",
                OwningAccountId = 1001,
                OrganizationId = organizationId,
                PublicCredentialId = "test-public-id",
                CredentialVersion = 1,
                GrantedScopes = scopes,
                RateLimitProfile = "standard",
                AuditIdentity = "bot:org-helper",
                CreatedAtUtc = Now,
                EnabledSnapshot = true
            };
        }

        private static void AssertThrows<T>(Action action) where T : Exception
        {
            try
            {
                action();
                Assert.Fail("Expected exception " + typeof(T).Name + ".");
            }
            catch (T)
            {
            }
        }

        private sealed class Handler : IBotChatRequestHandler
        {
            private readonly Func<BotSession, BotChatRequest, BotOperationResult> handler;

            public Handler(Func<BotSession, BotChatRequest, BotOperationResult> handler)
            {
                this.handler = handler;
            }

            public BotOperationResult Handle(BotSession session, BotChatRequest request)
            {
                return this.handler(session, request);
            }
        }

        private sealed class Fixture
        {
            public InMemoryBotIdentityRepository Repository { get; set; }

            public RecordingBotAuditSink Audit { get; set; }

            public BotCredentialManager Credentials { get; set; }

            public BotCredentialAuthenticator Authenticator { get; set; }

            public BotSessionService Sessions { get; set; }

            public BotPrincipal Principal { get; set; }

            public BotCredentialIssue Issue { get; set; }

            public BotRuntime Runtime { get; set; }
        }
    }
}
