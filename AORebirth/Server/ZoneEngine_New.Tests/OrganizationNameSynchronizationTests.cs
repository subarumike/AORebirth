namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

using AORebirth.Interfaces.Persistence.Characters;
using AORebirth.World.Contracts.V1;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages.OrgServerMessages;

using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Chat;
using ZoneEngine_New.Core.Configuration;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.MessageHandlers;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Organizations;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Teams;

using Vector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
public sealed class OrganizationNameSynchronizationTests
{
    const string FirstName = "Initial Organization";
    const string NewName = "Renamed Organization";
    const string LastName = "Final Organization";
    const int LeaderId = 7001;
    const int SamePlayfieldMemberId = 7007;
    const int AnotherPlayfieldMemberId = 7008;
    const int UnrelatedLeaderId = 7009;
    const int UnrelatedOrgId = 987654;

    [TestMethod]
    public void RenameBeforeInvitationPublishesCurrentNameInSameAndAnotherPlayfield()
    {
        using var scene = new Scene();
        Player leader = scene.FoundOrganization();
        Player samePlayfieldMember = scene.Connect(SamePlayfieldMemberId, scene.First);
        Player anotherPlayfieldMember = scene.Connect(AnotherPlayfieldMemberId, scene.Second);
        Player unrelated = scene.Connect(UnrelatedLeaderId, scene.First);
        int organizationId = leader.Organization!.Id;

        scene.ClearPackets();
        scene.Command(leader, OrgClientCommand.Name, args: NewName);

        Assert.AreEqual(NewName, scene.Store.GetById(organizationId)?.Name);
        Assert.AreEqual(organizationId, leader.Organization?.Id);
        scene.AssertOrgPacket(leader, leader.Identity, organizationId, NewName);
        Assert.AreEqual((LeaderId, "#aorebirth-org-refresh " + organizationId),
            scene.Chat.Commands.Single(command => command.Text.StartsWith("#aorebirth-org-refresh ", StringComparison.Ordinal)));
        Assert.IsFalse(scene.Session(unrelated).Packets<OrgInfoPacketMessage>().Any(
            packet => packet.OrganizationId == organizationId));

        scene.Invite(leader, samePlayfieldMember, organizationId, NewName);
        scene.Invite(leader, anotherPlayfieldMember, organizationId, NewName);

        foreach (Player member in new[] { samePlayfieldMember, anotherPlayfieldMember })
        {
            Assert.AreEqual(organizationId, scene.Store.GetOrganizationIdOf(member.Identity.Instance));
            Assert.AreEqual(2, scene.Store.GetStoredRank(member.Identity.Instance));
            Assert.AreEqual(organizationId, member.Stats.GetOrZero(CharacterStat.Clan, StatDetail.Base));
            Assert.AreEqual(NewName, member.Organization?.Name);
            Assert.AreEqual(1, scene.Chat.Commands.Count(command =>
                command == (member.Identity.Instance, "#aorebirth-org-join " + organizationId)));
        }

        Assert.AreEqual(UnrelatedOrgId, scene.Store.GetOrganizationIdOf(UnrelatedLeaderId));
        Assert.AreEqual("Unrelated Organization", unrelated.Organization?.Name);
        Assert.AreEqual(8, scene.Store.GetMemberIds(organizationId).Count);
    }

    [TestMethod]
    public void LaterRenameSurvivesPlayfieldChangeAndNewSessionsAfterServiceReconstruction()
    {
        var store = new OrganizationStore();
        int organizationId;
        using (var scene = new Scene(store))
        {
            Player leader = scene.FoundOrganization();
            Player member = scene.Connect(SamePlayfieldMemberId, scene.First);
            organizationId = leader.Organization!.Id;
            scene.Invite(leader, member, organizationId, FirstName);
            scene.ClearPackets();

            scene.Command(leader, OrgClientCommand.Name, args: LastName);

            scene.AssertOrgPacket(leader, leader.Identity, organizationId, LastName);
            scene.AssertOrgPacket(member, member.Identity, organizationId, LastName);

            scene.Move(member, scene.Second);
            scene.AssertOrgInfo(member, organizationId, LastName, "Member");

            WireSession leaderSession = scene.Session(leader);
            WireSession memberSession = scene.Session(member);
            scene.Disconnect(leader);
            scene.Disconnect(member);
            leader = scene.Connect(LeaderId, scene.First);
            member = scene.Connect(SamePlayfieldMemberId, scene.Second);
            Assert.AreNotSame(leaderSession, leader.Session);
            Assert.AreNotSame(memberSession, member.Session);
            scene.AssertOrgInfo(leader, organizationId, LastName, "Leader");
            scene.AssertOrgInfo(member, organizationId, LastName, "Member");
        }

        using var restarted = new Scene(store);
        Player restoredLeader = restarted.Connect(LeaderId, restarted.First);
        Player restoredMember = restarted.Connect(SamePlayfieldMemberId, restarted.Second);
        Player unrelated = restarted.Connect(UnrelatedLeaderId, restarted.Second);
        restarted.AssertOrgInfo(restoredLeader, organizationId, LastName, "Leader");
        restarted.AssertOrgInfo(restoredMember, organizationId, LastName, "Member");
        restarted.AssertOrgInfo(unrelated, UnrelatedOrgId, "Unrelated Organization", "Leader");
        Assert.AreEqual(7, store.GetMemberIds(organizationId).Count);
    }

    [TestMethod]
    public void WorldNameInvalidationReadsCurrentNameAndPublishesToLocalMembersOnly()
    {
        using var scene = new Scene();
        Player leader = scene.FoundOrganization();
        Player member = scene.Connect(SamePlayfieldMemberId, scene.Second);
        Player unrelated = scene.Connect(UnrelatedLeaderId, scene.Second);
        int organizationId = leader.Organization!.Id;
        scene.Invite(leader, member, organizationId, FirstName);
        scene.ClearPackets();

        // Another Zone has already committed the authoritative name by the time World routes the invalidation.
        scene.Store.SaveText(organizationId, OrganizationTextField.Name, NewName);
        int reads = scene.Store.OrganizationReads;
        scene.ReceiveWorldNameRefresh("invalid");
        scene.ReceiveWorldNameRefresh("900000");
        Assert.AreEqual(reads, scene.Store.OrganizationReads);
        Assert.AreEqual(FirstName, leader.Organization?.Name);
        Assert.IsFalse(scene.Session(leader).Packets<OrgInfoPacketMessage>().Any());

        scene.ReceiveWorldNameRefresh(organizationId.ToString(CultureInfo.InvariantCulture));

        Assert.AreEqual(reads + 1, scene.Store.OrganizationReads);
        scene.AssertOrgPacket(leader, leader.Identity, organizationId, NewName);
        scene.AssertOrgPacket(member, member.Identity, organizationId, NewName);
        Assert.AreEqual("Unrelated Organization", unrelated.Organization?.Name);
        Assert.IsFalse(scene.Session(unrelated).Packets<OrgInfoPacketMessage>().Any());
    }

    sealed class Scene : IDisposable
    {
        readonly PlayfieldManager _manager = (PlayfieldManager)RuntimeHelpers.GetUninitializedObject(typeof(PlayfieldManager));
        readonly TeamService _teams = new(dispatchOnOwner: (_, action) => action());
        readonly StubGameData _data = new(HashItemCatalog.Parse(null))
        {
            AllowMissingXpLevels = true,
            GoverningForms = new GoverningFormCatalog
            {
                Forms = { [0] = new GoverningForm { Name = "Department", Ranks = ["Leader", "Officer", "Member"] } }
            }
        };
        readonly StubLogger _logger = new();
        readonly PlayerHydrator _hydrator;
        readonly ZoneMessageCodec _codec = new();
        readonly OrgClientMessageHandler _handler;
        readonly OrgLockoutClock _lockouts;
        readonly OrgVoteService _votes;

        public Scene(OrganizationStore? store = null)
        {
            Store = store ?? new OrganizationStore();
            SetField(_manager, "_sync", new Lock());
            SetField(_manager, "_playersByCharacterId", new Dictionary<int, Player>());
            SetField(_manager, "_pendingMoveByCharacter", new System.Collections.Concurrent.ConcurrentDictionary<int, int>());
            SetField(_manager, "<Teams>k__BackingField", _teams);

            First = new Location(1001);
            Second = new Location(1002);
            _hydrator = new PlayerHydrator(new StubItemBuilder(), _data);
            var settings = new GameplaySettings { Org = new GameplayOrg { CreateGmOnly = false } };
            var lazyManager = new Lazy<PlayfieldManager>(() => _manager);
            _lockouts = new OrgLockoutClock(Store);
            var tax = new OrgTaxTickService(Store, new CharacterPlayedClock(Store), settings, lazyManager);
            _votes = new OrgVoteService(Store, Chat, _logger, settings);
            var membership = new OrgMembershipService(Store, _lockouts, Chat, null!, lazyManager, _data, settings, tax);
            _handler = new OrgClientMessageHandler(_data, lazyManager, Store, _logger, Chat, _lockouts, membership, tax, _votes, settings);
        }

        public OrganizationStore Store { get; }
        public ChatRecording Chat { get; } = new();
        public Location First { get; }
        public Location Second { get; }

        public Player Connect(int id, Location location)
        {
            Player player = TestWorld.CreatePlayer(id, "OrgFixture" + id);
            OrganizationRecord? org = Store.GetById(Store.GetOrganizationIdOf(id));
            var stats = CharacterHydrationValidator.RequiredSpawnStats.Select(stat => new StatRecord
            {
                StatId = (int)stat,
                StatValue = stat is CharacterStat.Health or CharacterStat.MaxHealth
                    or CharacterStat.CurrentNano or CharacterStat.MaxNanoEnergy ? 100 : 1
            }).ToList();
            stats.Add(new StatRecord { StatId = (int)CharacterStat.Clan, StatValue = org?.Id ?? 0 });
            stats.Add(new StatRecord { StatId = (int)CharacterStat.ClanLevel, StatValue = Store.GetStoredRank(id) });

            _hydrator.Apply(player, new CharacterHydrationResult
            {
                Character = new CharacterRecord
                {
                    Id = id, Name = "OrgFixture" + id, Playfield = location.Playfield.Identity.Instance,
                    X = id - LeaderId, HeadingW = 1
                },
                Stats = stats,
                Organization = org
            });
            var session = new WireSession { State = SessionState.InPlay };
            player.EnterOnline(session);
            location.Arrive(player);
            _manager.RegisterPlayer(player);
            _teams.AttachPlayer(player);
            return player;
        }

        public void Move(Player player, Location destination)
        {
            Location current = Site(player);
            current.Remove(player);
            destination.Arrive(player);
        }

        public void Disconnect(Player player)
        {
            WireSession session = Session(player);
            Site(player).Remove(player);
            _manager.UnregisterPlayer(player);
            player.Session = null;
            session.UnbindPlayer();
            session.Close();
        }

        public Player FoundOrganization()
        {
            Player leader = Connect(LeaderId, First);
            for (int id = LeaderId + 1; id < SamePlayfieldMemberId; id++)
            {
                Player founder = Connect(id, First);
                _teams.TryHandle(leader, new CharacterActionMessage
                {
                    Identity = leader.Identity, Target = founder.Identity, Action = CharacterActionType.TeamRequestInvite
                });
                _teams.TryHandle(founder, new CharacterActionMessage
                {
                    Identity = founder.Identity, Target = leader.Identity, Action = CharacterActionType.TeamRequestReply, Parameter2 = 1
                });
            }

            Command(leader, OrgClientCommand.Create, args: FirstName);
            int organizationId = Store.GetOrganizationIdOf(LeaderId);
            Assert.IsTrue(organizationId > 0, "The six founders did not create the organization.");
            Assert.AreEqual(6, Store.GetMemberIds(organizationId).Count);
            AssertOrgPacket(leader, leader.Identity, organizationId, FirstName);
            return leader;
        }

        public void Invite(Player leader, Player member, int organizationId, string name)
        {
            ClearPackets();
            Command(leader, OrgClientCommand.Invite, member.Identity);

            OrgInviteMessage invitation = Session(member).Packets<OrgInviteMessage>().Single();
            Assert.AreEqual(new Identity { Type = IdentityType.Organization, Instance = organizationId }, invitation.Organization);
            Assert.AreEqual(name, invitation.OrganizationName);

            Command(member, OrgClientCommand.Join, invitation.Organization);
            AssertOrgPacket(member, member.Identity, organizationId, name);
            AssertOrgPacket(leader, member.Identity, organizationId, name);
        }

        public void Command(Player player, OrgClientCommand command, Identity? target = null, string? args = null)
        {
            var outgoing = new OrgClientMessage
            {
                Identity = player.Identity, Target = target ?? player.Identity, Command = command, Unknown1 = 1, CommandArgs = args ?? ""
            };
            byte[] wire = _codec.Serialize(outgoing, player.Identity.Instance, 0);
            OrgClientMessage request = (OrgClientMessage)_codec.Deserialize(wire)!.Body;
            Site(player).OnTick(() => _handler.Handle(request, Session(player)));
            First.Drain();
            Second.Drain();
        }

        public void ReceiveWorldNameRefresh(string organizationId)
        {
            var command = new InstanceCommand { Name = "org.refresh-name", Arguments = { organizationId } };
            typeof(OrgClientMessageHandler).GetMethod("OnOrganizationNameRefresh", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_handler, [command]);
            First.Drain();
            Second.Drain();
        }

        public WireSession Session(Player player) => (WireSession)player.Session!;

        public void AssertOrgPacket(Player observer, Identity subject, int orgId, string name)
        {
            Assert.IsTrue(Session(observer).Packets<OrgInfoPacketMessage>().Any(
                packet => packet.Identity.Equals(subject) && packet.OrganizationId == orgId && packet.Name == name),
                $"Session {observer.Identity.Instance} lacks organization {orgId} '{name}' for {subject.Instance}.");
        }

        public void AssertOrgInfo(Player player, int orgId, string name, string rank)
        {
            Session(player).Clear();
            Command(player, OrgClientCommand.Info);
            OrgInfoMessage info = Session(player).Packets<OrgInfoMessage>().Single();
            Assert.AreEqual(new Identity { Type = IdentityType.Organization, Instance = orgId }, info.Organization);
            Assert.AreEqual(name, info.OrganizationName);
            Assert.AreEqual(rank, info.Rank);
        }

        public void ClearPackets()
        {
            foreach (Player player in _manager.SnapshotPlayers())
                Session(player).Clear();
        }

        Location Site(Player player) => ReferenceEquals(player.Playfield, First.Playfield) ? First : Second;

        public void Dispose()
        {
            foreach (Player player in _manager.SnapshotPlayers())
                Disconnect(player);
            _votes.Dispose();
            First.Dispose();
            Second.Dispose();
        }
    }

    // Mirrors the playfield shell in PlayfieldTransferTests. The real inbound queue owns cross-playfield projections.
    sealed class Location : IDisposable
    {
        readonly Lock _tick = new();
        readonly PlayfieldInboundQueue _incoming = new();
        readonly PlayfieldLocality _locality;
        readonly ServiceProvider _services;
        readonly DynelRegistry _dynels = new();
        public Playfield Playfield { get; } = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));

        public Location(int id)
        {
            _locality = new PlayfieldLocality(id, null);
            _services = new ServiceCollection().AddSingleton(_locality).BuildServiceProvider();
            SetField(Playfield, "<Identity>k__BackingField", new Identity { Type = IdentityType.Playfield2, Instance = id });
            SetField(Playfield, "_tickSync", _tick);
            SetField(Playfield, "_lifecycleSync", new Lock());
            SetField(Playfield, "_inbound", _incoming);
            SetField(Playfield, "_dynelRegistry", _dynels);
            SetField(Playfield, "_serviceProvider", _services);
        }

        public void Arrive(Player player)
        {
            player.Playfield = Playfield;
            _dynels.Register(player);
            _locality.RegisterDynel(player);
        }

        public void Remove(Player player)
        {
            _locality.UnregisterDynel(player);
            _dynels.UnregisterExact(player);
            player.Playfield = null;
        }

        public void OnTick(Action action)
        {
            lock (_tick)
                action();
        }

        public void Drain() => OnTick(() => _incoming.Drain(null!, null!, Playfield));

        public void Dispose() => _services.Dispose();
    }

    sealed class WireSession : IZoneSession
    {
        readonly ZoneMessageCodec _codec = new();
        readonly List<byte[]> _sent = new();

        public SessionState State { get; set; }
        public Player? Player { get; private set; }

        public IEnumerable<T> Packets<T>() where T : MessageBody =>
            _sent.Select(bytes => _codec.Deserialize(bytes)?.Body).OfType<T>();

        public void Clear() => _sent.Clear();
        public void BindPlayer(Player player) => Player = player;
        public void UnbindPlayer() => Player = null;
        public void Close() => State = SessionState.Closed;
        public void Send(byte[] packet) => _sent.Add(packet);
        public void Send(Message message) => Send(message.Body, message.Header.Sender, message.Header.Receiver);
        public void Send(MessageBody body) => Send(body, 0, Player!.Identity.Instance);
        public void Send(MessageBody body, int sender, int receiver)
        {
            if (body is OrgInfoPacketMessage or OrgInviteMessage or OrgInfoMessage)
                Send(_codec.Serialize(body, sender, receiver));
        }
        public void SendInitiateCompression() => throw new NotSupportedException();
        public bool TransferToPlayfield(Playfield destination, Vector3 landing) => throw new NotSupportedException();
        public void SendSamePlayfieldRespawnTeleport(Vector3 landing) => throw new NotSupportedException();
    }

    sealed class ChatRecording : IChatEngineLink
    {
        public List<(int CharacterId, string Text)> Commands { get; } = new();
        public bool SendChatCommand(int characterId, string command)
        {
            Commands.Add((characterId, command));
            return true;
        }
        public bool SendOrganizationMessage(int organizationId, string text) => true;
        public bool SendVicinity(int senderId, string? senderName, byte messageType, string text, IReadOnlyList<int> recipients)
            => throw new NotSupportedException();
        public bool SendAnonymousVicinity(string text, IReadOnlyList<int> recipients, byte[]? blob = null)
            => throw new NotSupportedException();
        public void Start() { }
        public void Dispose() { }
    }

    sealed class OrganizationStore : IOrganizationRepository, ICharacterPlayedBornRepository
    {
        readonly Dictionary<int, OrganizationRecord> _organizations = new()
        {
            [UnrelatedOrgId] = Row(UnrelatedOrgId, UnrelatedLeaderId, "Unrelated Organization")
        };
        readonly Dictionary<int, int> _members = new() { [UnrelatedLeaderId] = UnrelatedOrgId };
        readonly Dictionary<int, int> _ranks = new() { [UnrelatedLeaderId] = 0 };
        readonly Dictionary<int, PersistedOrgMemberTaxData> _tax = new();
        int _nextOrgId = 21000;

        static OrganizationRecord Row(int orgId, int leader, string name) =>
            new(orgId, DateTime.UnixEpoch, name, leader, "OrgFixture" + leader, 0, "", "", "");

        public int OrganizationReads { get; private set; }

        public OrganizationRecord? GetById(int organizationId)
        {
            OrganizationReads++;
            return _organizations.GetValueOrDefault(organizationId);
        }
        public bool IsNameTaken(string name, int exceptOrganizationId) => _organizations.Values.Any(
            row => row.Id != exceptOrganizationId && row.Name == name);

        public PersistedOrganizationCreateResult Create(string name, int leaderId, IList<int> memberIds, int leaderRank, int memberRank)
        {
            int id = _nextOrgId++;
            _organizations.Add(id, Row(id, leaderId, name));
            foreach (int memberId in memberIds)
            {
                _members.Add(memberId, id);
                _ranks.Add(memberId, memberId == leaderId ? leaderRank : memberRank);
            }
            return new PersistedOrganizationCreateResult
            {
                Outcome = OrganizationCreateOutcome.Created,
                OrganizationId = id
            };
        }

        public void SaveText(int organizationId, OrganizationTextField field, string value)
        {
            Assert.AreEqual(OrganizationTextField.Name, field);
            _organizations[organizationId] = _organizations[organizationId] with { Name = value };
        }

        public void SaveMembership(int characterId, int organizationId) => _members.Add(characterId, organizationId);
        public void SaveRank(int characterId, int rank) => _ranks[characterId] = rank;
        public int GetOrganizationIdOf(int characterId) => _members.GetValueOrDefault(characterId);
        public int GetStoredRank(int characterId) => _ranks.GetValueOrDefault(characterId);
        public IList<int> GetMemberIds(int organizationId) =>
            _members.Where(row => row.Value == organizationId).Select(row => row.Key).ToList();

        public PersistedOrgLockoutData GetLockout(int characterId) => default!;
        public PersistedOrgMemberTaxData GetMemberTax(int characterId) => _tax.GetValueOrDefault(characterId)!;
        public void SaveMemberTax(int characterId, PersistedOrgMemberTaxData data) => _tax[characterId] = data;
        public PersistedCharacterPlayedBornData? Load(int characterId) => null;
        public void Save(int characterId, string characterName, long playedSeconds, long? createdAtUnix) => throw new NotSupportedException();
        public void SaveGoverningForm(int organizationId, int governingForm, int leaderId, int lowestRank) => throw new NotSupportedException();
        public void SaveLeader(int organizationId, int leaderId) => throw new NotSupportedException();
        public void SaveTax(int organizationId, int tax) => throw new NotSupportedException();
        public void SaveLockout(int characterId, string characterName, long secondsRemaining) => throw new NotSupportedException();
        public PersistedAnarchistKickResult RecordAnarchistKick(int organizationId, int kickedId, string kickedName,
            int kickerId, string kickerName, int memberQuorum, int leaderQuorum) => throw new NotSupportedException();
        public PersistedOrganizationBankResult TransferBank(int organizationId, int characterId,
            bool deposit, long amount, long bankCap, long cashCap) => throw new NotSupportedException();
        public void ClearMembership(int characterId) => throw new NotSupportedException();
        public void Disband(int organizationId) => throw new NotSupportedException();
        public PersistedOrgVoteStartResult StartVote(int organizationId, int starterId, string description,
            int durationMinutes, IList<string> choices, IList<int> electorateIds, long startedUnix,
            long endsUnix, long cooldownSeconds) => throw new NotSupportedException();
        public PersistedOrgVoteData? GetShownVote(int organizationId) => throw new NotSupportedException();
        public PersistedOrgVoteData? GetVote(int voteId) => throw new NotSupportedException();
        public IList<int> GetDueVoteIds(long nowUnix) => Array.Empty<int>();
        public OrgVoteCastOutcome CastVote(int voteId, int characterId, int choiceNo, long nowUnix) => throw new NotSupportedException();
        public bool CloseVote(int voteId, string status, string? stopReason, long nowUnix) => throw new NotSupportedException();
    }

    static void SetField<T>(object target, string name, T value)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}
