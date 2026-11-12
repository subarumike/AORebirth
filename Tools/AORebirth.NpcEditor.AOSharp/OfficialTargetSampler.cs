using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AORebirth.NpcEditor.Official;
using AOSharp.Common.GameData;
using AOSharp.Core;

namespace AORebirth.NpcEditor.AOSharp;

internal sealed class OfficialTargetSampler
{
    private const int EventLimit = 24;
    private const int VisibleEffectLimit = 12;
    private const string ClientExposed = "CLIENT_EXPOSED";
    private const string NotAvailable = "NOT_AVAILABLE";
    private const string SelectedObservation = "Selected NPC observation";

    private static readonly Stat[] ObservedStats =
    {
        Stat.AddAllOff, Stat.AddAllDef,
        Stat.ProjectileAC, Stat.MeleeAC, Stat.EnergyAC, Stat.ChemicalAC,
        Stat.RadiationAC, Stat.ColdAC, Stat.PoisonAC, Stat.FireAC,
        Stat.DuckExp, Stat.DodgeRanged, Stat.EvadeClsC, Stat.NanoResist,
        Stat.MeleeInit, Stat.RangedInit, Stat.PhysicalInit, Stat.NanoCInit,
        Stat.AttackRange, Stat.RunSpeed, Stat.VisualProfession
    };

    private static readonly Stat[] AppearanceStats =
    {
        Stat.Mesh, Stat.HeadMesh, Stat.HairMesh, Stat.BackMesh, Stat.ShoulderMesh,
        Stat.WeaponMesh, Stat.MonsterTexture, Stat.FaceTexture, Stat.HairTexture, Stat.Scale
    };

    private readonly int processId;
    private readonly string sessionId;
    private readonly Queue<OfficialEvent> events = new();
    private OfficialSnapshot? previous;
    private long sequence;

    public OfficialTargetSampler(int processId, string sessionId)
    {
        this.processId = processId;
        this.sessionId = sessionId;
    }

    public OfficialSnapshot Sample(SimpleChar? target, string state, long captured, int playfieldId,
        int identityType, int instanceId)
    {
        var snapshot = new OfficialSnapshot
        {
            ProcessId = processId,
            SessionId = sessionId,
            CapturedUtcTicks = captured,
            Sequence = ++sequence,
            State = state
        };

        if (target == null || playfieldId <= 0 || identityType <= 0 || instanceId == 0
            || !Read(() => target.IsInPlay, false))
        {
            if (target != null)
                snapshot.State = "unavailable";
            previous = null;
            events.Clear();
            return snapshot;
        }

        snapshot.State = "selected";
        snapshot.PlayfieldId = playfieldId;
        snapshot.IdentityType = identityType;
        snapshot.InstanceId = instanceId;
        snapshot.Name = VisibleText(Read(() => target.Name, null), 256);
        snapshot.MonsterData = ReadPositive(() => target.GetStat(Stat.MonsterData));
        int? level = ReadPositive(() => target.Level);
        snapshot.Level = level <= ushort.MaxValue ? level : null;

        var fields = new List<OfficialField>();
        Add(fields, "identity.name", snapshot.Name, "Dynel.Name");
        Add(fields, "identity.type", identityType, "Dynel.Identity.Type");
        Add(fields, "identity.kind", "NPC", "Targeting.TargetChar / SimpleChar.IsNpc / SimpleChar.IsPet");
        Add(fields, "identity.instance", instanceId, "Dynel.Identity.Instance");
        Add(fields, "identity.playfield", playfieldId, "Playfield.ModelIdentity.Instance");
        Add(fields, "identity.monster_data", snapshot.MonsterData, "Dynel.GetStat(MonsterData)");
        Add(fields, "identity.level", snapshot.Level, "SimpleChar.Level");
        Add(fields, "identity.family", ReadPositive(() => target.GetStat(Stat.NPCFamily)), "Dynel.GetStat(NPCFamily)");
        Add(fields, "identity.profession", ReadPositive(() => (int)target.Profession), "SimpleChar.Profession");
        Add(fields, "identity.breed", ReadPositive(() => (int)target.Breed), "SimpleChar.Breed");
        Add(fields, "identity.side", ReadPositive(() => (int)target.Side), "SimpleChar.Side");

        AddVitals(fields, "health", () => target.Health, () => target.MaxHealth, () => target.HealthPercent,
            "Health", "MaxHealth", "HealthPercent");
        AddVitals(fields, "nano", () => target.Nano, () => target.MaxNano, () => target.NanoPercent,
            "Nano", "MaxNano", "NanoPercent");

        var position = Read<Vector3?>(() => target.Position, null);
        Add(fields, "position.x", position.HasValue ? Finite(position.Value.X) : null, "Dynel.Position.X");
        Add(fields, "position.y", position.HasValue ? Finite(position.Value.Y) : null, "Dynel.Position.Y");
        Add(fields, "position.z", position.HasValue ? Finite(position.Value.Z) : null, "Dynel.Position.Z");

        var rotation = Read(() => target.Rotation, default);
        bool hasRotation = Finite(rotation.X) != null && Finite(rotation.Y) != null
            && Finite(rotation.Z) != null && Finite(rotation.W) != null
            && (rotation.X != 0 || rotation.Y != 0 || rotation.Z != 0 || rotation.W != 0);
        Add(fields, "movement.rotation_x", hasRotation ? rotation.X : (float?)null, "Dynel.Rotation.X");
        Add(fields, "movement.rotation_y", hasRotation ? rotation.Y : (float?)null, "Dynel.Rotation.Y");
        Add(fields, "movement.rotation_z", hasRotation ? rotation.Z : (float?)null, "Dynel.Rotation.Z");
        Add(fields, "movement.rotation_w", hasRotation ? rotation.W : (float?)null, "Dynel.Rotation.W");
        Add(fields, "movement.heading", hasRotation ? Read(() => Finite(rotation.Yaw), null) : null,
            "Dynel.Rotation.Yaw", "rad");
        Add(fields, "movement.state", Read(() => target.MovementState.ToString(), null), "Dynel.MovementState");
        Add(fields, "movement.velocity", Read(() => Finite(target.Velocity), null), "Dynel.Velocity");
        Add(fields, "movement.is_moving", Read<bool?>(() => target.IsMoving, null), "Dynel.IsMoving");

        foreach (Stat stat in ObservedStats)
            AddStat(fields, "stats.", target, stat);
        foreach (Stat stat in AppearanceStats)
            AddStat(fields, "appearance.", target, stat);

        bool hasHealth = fields.Any(field => field.Key == "health.current" && field.Value != null);
        Add(fields, "combat.is_alive", hasHealth ? Read<bool?>(() => target.IsAlive, null) : null, "SimpleChar.IsAlive");
        Add(fields, "combat.is_attacking", Read<bool?>(() => target.IsAttacking, null), "SimpleChar.IsAttacking");
        Add(fields, "equipment.weapons", Read(() => VisibleWeapons(target), null), "SimpleChar.Weapons / Dynel.Identity / Dynel.Name");
        Add(fields, "nano.buffs", Read(() => VisibleEffects(target), null), "SimpleChar.Buffs / Buff.Id / Buff.Name");

        foreach (string key in new[] { "equipment.armor", "equipment.template_ids", "nano.casting", "combat.damage",
            "combat.animation", "combat.timing", "movement.patrol", "loot.contents" })
        {
            fields.Add(new OfficialField { Key = key, Source = SelectedObservation, Classification = NotAvailable });
        }

        snapshot.Fields = fields.ToArray();
        UpdateTimeline(snapshot);
        previous = snapshot;
        return snapshot;
    }

    private static void AddVitals(List<OfficialField> fields, string key, Func<int> currentGetter,
        Func<int> maximumGetter, Func<float> percentGetter, string currentName, string maximumName, string percentName)
    {
        int? maximum = ReadPositive(maximumGetter);
        int? current = Read<int?>(() => currentGetter(), null);
        if (current < 0 || (current == 0 && maximum == null))
            current = null;

        Add(fields, key + ".current", current, "SimpleChar." + currentName);
        Add(fields, key + ".maximum", maximum, "SimpleChar." + maximumName);
        float? percent = current != null && maximum != null ? Read(() => Finite(percentGetter()), null) : null;
        Add(fields, key + ".percent", percent, "SimpleChar." + percentName, "%");
    }

    private static void AddStat(List<OfficialField> fields, string prefix, SimpleChar target, Stat stat)
    {
        string name = stat.ToString();
        Add(fields, prefix + name.ToLowerInvariant(), ReadPositive(() => target.GetStat(stat)), "Dynel.GetStat(" + name + ")");
    }

    private static string? VisibleWeapons(SimpleChar target)
    {
        var weapons = target.Weapons;
        if (weapons == null)
            return null;

        var values = new List<string>();
        foreach (var slot in weapons.OrderBy(pair => pair.Key))
        {
            var weapon = slot.Value;
            if (weapon == null)
                continue;

            var identity = weapon.Identity;
            string? name = VisibleText(Read(() => weapon.Name, null), 128);
            string? id = (int)identity.Type > 0 && identity.Instance != 0
                ? FormattableString.Invariant($"{(int)identity.Type}:{identity.Instance}") : null;
            if (id != null || name != null)
                values.Add(string.Join(" ", new[] { slot.Key.ToString(), id, name }.Where(part => part != null)));
        }
        return values.Count == 0 ? "No weapons exposed in this sample" : string.Join(", ", values);
    }

    private static string? VisibleEffects(SimpleChar target)
    {
        var effects = target.Buffs;
        if (effects == null)
            return null;

        var visible = effects.Where(effect => effect != null && effect.Id > 0)
            .OrderBy(effect => effect.Id).Take(VisibleEffectLimit + 1).ToArray();
        if (visible.Length == 0)
            return "No active effects exposed in this sample";

        string names = string.Join(", ", visible.Take(VisibleEffectLimit).Select(effect =>
        {
            string? name = VisibleText(effect.Name, 96);
            string id = effect.Id.ToString(CultureInfo.InvariantCulture);
            return name == null ? id : $"{id} {name}";
        }));
        return visible.Length > VisibleEffectLimit ? names + ", additional effects omitted" : names;
    }

    private void UpdateTimeline(OfficialSnapshot snapshot)
    {
        if (previous == null || previous.PlayfieldId != snapshot.PlayfieldId || previous.IdentityType != snapshot.IdentityType
            || previous.InstanceId != snapshot.InstanceId || previous.CapturedUtcTicks > snapshot.CapturedUtcTicks)
        {
            events.Clear();
            snapshot.SelectionId = Guid.NewGuid().ToString("N");
            Record(snapshot, "Selected NPC", "The selected NPC was available in this sample.", "AOSharp Targeting.TargetChar");
        }
        else
        {
            snapshot.SelectionId = previous.SelectionId;
            RecordChange(snapshot, "health.current", "Health sample changed");
            RecordChange(snapshot, "combat.is_alive", "Alive state changed");
            RecordChange(snapshot, "combat.is_attacking", "Attack state changed");
            RecordChange(snapshot, "movement.is_moving", "Movement state changed");
            RecordChange(snapshot, "equipment.weapons", "Visible weapons changed");
            RecordChange(snapshot, "nano.buffs", "Visible active effects changed");
        }
        snapshot.Events = events.ToArray();
    }

    private void RecordChange(OfficialSnapshot snapshot, string key, string kind)
    {
        var oldField = previous?.Fields.FirstOrDefault(field => field.Key == key);
        var newField = snapshot.Fields.FirstOrDefault(field => field.Key == key);
        if (oldField?.Value != null && newField?.Value != null && oldField.Value != newField.Value)
            Record(snapshot, kind, "Sample value " + newField.Value, newField.Source);
    }

    private void Record(OfficialSnapshot snapshot, string kind, string detail, string source)
    {
        if (events.Count == EventLimit)
            events.Dequeue();

        events.Enqueue(new OfficialEvent
        {
            CapturedUtcTicks = snapshot.CapturedUtcTicks,
            Kind = kind,
            Detail = detail,
            Source = source,
            Classification = "DERIVED_FROM_OBSERVATIONS",
            Confidence = "Bounded client sampling"
        });
    }

    private static void Add(List<OfficialField> fields, string key, object? value, string member, string? unit = null)
    {
        fields.Add(new OfficialField
        {
            Key = key,
            Value = value is bool flag ? (flag ? "true" : "false")
                : value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture),
            Unit = unit,
            Source = "AOSharp " + member,
            Classification = value == null ? NotAvailable : ClientExposed,
            Confidence = value == null ? null : "Client API reading"
        });
    }

    private static int? ReadPositive(Func<int> getter)
    {
        int value = Read(getter, 0);
        return value > 0 ? value : (int?)null;
    }

    private static T Read<T>(Func<T> getter, T unavailable)
    {
        try
        {
            return getter();
        }
        catch (Exception)
        {
            return unavailable;
        }
    }

    private static float? Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? (float?)null : value;

    private static string? VisibleText(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.IndexOf('\0') >= 0 ? null : value;
}
