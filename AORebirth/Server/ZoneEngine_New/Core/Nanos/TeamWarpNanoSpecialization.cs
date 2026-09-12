namespace ZoneEngine_New.Core.Nanos;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using ZoneEngine_New.Core.Teams;
using Vector3 = AORebirth.Core.Vector.Vector3;
using Quaternion = AORebirth.Core.Vector.Quaternion;

/// <summary>
/// Accepted TeamWarpRuntime/SummonPlayer/SummonTeamMates contract. Cost/timing belong
/// to NanoService; foreign member movement is queued on its existing playfield owner.
/// This is not an arbitrary summon-function interpreter or a new teleport protocol.
/// </summary>
public sealed class TeamWarpNanoSpecialization(TeamService teams, Lazy<PlayfieldManager> playfields)
    : INanoSpecialization, INanoCastContextSpecialization
{
    public const int SelectedNanoId = 154914, TeamNanoId = 154913;
    private sealed record Member(Player Player, ZoneSession Session, Playfield Source, Vector3 Landing);

    public bool Handles(int id) => id is SelectedNanoId or TeamNanoId;
    public bool TryPrepare(Player caster, Player target, NanoDefinition nano, out NanoSpecializationPlan plan)
    {
        plan = new(false, 0);
        if (!ReferenceEquals(caster, target) || !Handles(nano.Id) || nano.DurationCentiseconds != 0
            || caster.Playfield == null || caster.Playfield.IsDisposed || !AllowedPlayfield(caster.Playfield.Identity.Instance)
            || caster.Stats.GetOrZero((CharacterStat)531) != 0 || nano.Template.SpellList.Count != 1
            || !nano.Template.SpellList.TryGetValue(EventType.OnUse, out var spells) || spells.Count != 1) return false;
        ItemSpell spell = spells[0];
        return spell.FunctionType == (nano.Id == SelectedNanoId ? 53154 : 53155)
            && spell.Target == (int)ItemTarget.Wearer && spell.TickCount == 1 && spell.TickInterval == 0
            && spell.Arguments.Count == 0 && spell.Requirements.Count == 0;
    }

    bool INanoCastContextSpecialization.TryPrepareCast(Player caster, NanoDefinition nano, Identity requestedTarget,
        Func<bool> casterStillCurrent, out Action afterCommit)
    {
        afterCommit = () => { };
        if (!TryPrepare(caster, caster, nano, out _) || caster.Session == null
            || !playfields.Value.FindPlayer(caster.Identity.Instance, out var current) || !ReferenceEquals(current, caster)
            || teams.GetTeam(caster) is not { } team) return false;
        Playfield destination = caster.Playfield!;
        Quaternion heading = new(caster.Rotation.x, caster.Rotation.y, caster.Rotation.z, caster.Rotation.w);
        Vector3 origin = new(caster.Position.x, caster.Position.y, caster.Position.z);
        double magnitude = heading.magnitude;
        if (!Finite(origin) || !float.IsFinite(heading.xf) || !float.IsFinite(heading.yf)
            || !float.IsFinite(heading.zf) || !float.IsFinite(heading.wf)
            || !double.IsFinite(magnitude) || magnitude <= 0) return false;
        var members = new List<Member>();
        if (nano.Id == SelectedNanoId)
        {
            // Legacy assigns nonzero incoming Target immediately before OnUse; an omitted
            // target retains the current selection at completion, not the cast-start selection.
            Identity selected = requestedTarget.Instance != 0 ? requestedTarget : caster.Target;
            if (selected.Type != IdentityType.CanbeAffected || selected.Instance == caster.Identity.Instance
                || !team.MemberIds.Contains(selected.Instance) || !TryMember(selected.Instance, 0, out var member)) return false;
            members.Add(member!);
        }
        else
        {
            int slot = 0;
            foreach (int id in team.MemberIds)
            {
                if (id <= 0 || id == caster.Identity.Instance) continue;
                if (!playfields.Value.FindPlayer(id, out Player member) || !Connected(member)) continue;
                // A resolved online member consumes a slot even if its PF is restricted.
                if (TryMember(id, slot, out var planned)) members.Add(planned!);
                slot++;
            }
        }
        if (members.Count == 0) return false;
        bool Authorized(Member member) => casterStillCurrent() && !destination.IsDisposed
            && playfields.Value.FindPlayer(caster.Identity.Instance, out var owner) && ReferenceEquals(owner, caster)
            && playfields.Value.FindPlayer(member.Player.Identity.Instance, out var recipient) && ReferenceEquals(recipient, member.Player)
            && teams.GetTeam(caster)?.TeamId == team.TeamId && teams.AreTeammates(caster, member.Player);
        int published = 0;
        afterCommit = () =>
        {
            if (Interlocked.Exchange(ref published, 1) != 0) return;
            foreach (Member member in members)
            {
                int moved = 0;
                member.Source.DispatchPlayerProjection(member.Player, () =>
                {
                    if (Interlocked.Exchange(ref moved, 1) != 0 || !Authorized(member)
                        || !ReferenceEquals(member.Player.Playfield, member.Source) || !Connected(member.Player)
                        || !ReferenceEquals(member.Player.Session, member.Session) || member.Player.IsDead
                        || member.Player.Stats.GetOrZero(CharacterStat.Health) <= 0) return;
                    try
                    {
                        if (ReferenceEquals(member.Source, destination)) WarpSamePlayfield(member.Player, member.Landing, heading);
                        else member.Session.TransferToPlayfield(destination, member.Landing, heading, () => Authorized(member));
                    }
                    catch (Exception exception) { member.Player.Logger.Error(exception, "Prepared Team Warp could not complete on its member owner."); }
                });
            }
        };
        return true;

        bool TryMember(int id, int slot, out Member? result)
        {
            result = null;
            if (!playfields.Value.FindPlayer(id, out Player player) || !Connected(player)
                || player.Session is not ZoneSession session || player.Playfield is not { } source
                || source.IsDisposed || !AllowedPlayfield(source.Identity.Instance) || !teams.AreTeammates(caster, player)) return false;
            // Only references/immutable PF identity are read here. Health and other member state
            // are checked later on the recipient tick, never through its foreign Stats dictionary.
            Vector3 landing = ComputeLanding(origin, heading, slot);
            if (!Finite(landing)) return false;
            result = new(player, session, source, landing);
            return true;
        }
    }

    internal static bool AllowedPlayfield(int id) => id > 0 && id is not (127 or 647 or 1931) && (id < 4000 || id > 4999);
    private static bool Finite(Vector3 position) => float.IsFinite(position.xf) && float.IsFinite(position.yf) && float.IsFinite(position.zf);
    internal static Vector3 ComputeLanding(Vector3 origin, Quaternion heading, int slot)
    {
        float angle = slot * 0.7f;
        var offset = Quaternion.RotateVector3(heading, new Vector3(2f * (float)Math.Sin(angle), 0, 2f * (float)Math.Cos(angle)));
        return new Vector3(origin.x + offset.xf, origin.y, origin.z + offset.zf);
    }

    private static bool Connected(Player player) => !player.IsPersistenceQuarantined
        && player.ConnectionPhase == PlayerConnectionPhase.Online
        && player.Session is { State: SessionState.InPlay } session && ReferenceEquals(session.Player, player);

    private static void WarpSamePlayfield(Player member, Vector3 landing, Quaternion heading)
    {
        Playfield playfield = member.Playfield!;
        PlayfieldLocality locality = playfield.GetRequiredService<PlayfieldLocality>();
        var recipients = locality.SnapshotObservers(member, includeSelf: true)
            .Select(p => (Player: p, Session: p.Session!)).ToArray();
        locality.UnregisterDynel(member);
        member.InterruptTimedActions(TimedActionInterrupt.LeavePlayfield);
        member.Motor.Warp(landing, heading);
        var setPos = new SetPosMessage { Identity = member.Identity,
            Coordinates = new SmokeLounge.AOtomation.Messaging.GameData.Vector3 { X = landing.xf, Y = landing.yf, Z = landing.zf }, Unknown1 = 1 };
        foreach (var recipient in recipients)
            if (ReferenceEquals(recipient.Player.Playfield, playfield) && ReferenceEquals(recipient.Player.Session, recipient.Session)
                && ReferenceEquals(recipient.Session.Player, recipient.Player) && recipient.Session.State == SessionState.InPlay)
                recipient.Session.Send(setPos);
        locality.RegisterDynel(member);
        locality.ActivatePlayerVisibility(member);
        locality.Announce(member, member.BuildAppearanceUpdateMessage(), includeSelf: true);
        member.Session?.Send(member.BuildSpawnMessage());
    }

    public void Applied(Player caster, Player target, NanoDefinition nano, ActiveNanoRecord? active) { }
    public void Removed(Player target, int nanoId) { }
    public void Restored(Player target, NanoDefinition nano, ActiveNanoRecord active) { }
    public void Tick(Player player) { }
    public void Detached(Player player) { }
}
