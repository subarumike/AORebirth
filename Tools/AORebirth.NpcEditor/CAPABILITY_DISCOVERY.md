# Official AO NPC capability discovery

The standalone editor reads the currently selected, in-play, non-pet NPC
through the installed AOSharp runtime. The plugin uses the existing x86
.NET Framework 4.8 loader. The desktop opens a separate Official AO read-only
window and consumes local snapshots without creating a ZoneEngine API client.

## Evidence and interpretation

- **OBSERVED** in the installed AOSharp Core and Common assemblies, version
  `1.0.0.0`. Offline inspection followed managed getter call sites through
  the existing target, playfield, dynel, vehicle, weapon and nano APIs.
- **PROVEN** by implementation and offline validation. The plugin schedules a
  game update observation callback and a local file publisher. The desktop
  consumes those files, marks unavailable values, and exports observation
  records with their sources. The other mode retains the ZoneEngine workflows.
- **UNVERIFIED** on a running official session. Field population, refresh
  behavior, and network behavior beyond the inspected managed/native call
  boundary remain manual acceptance items.

`CLIENT_EXPOSED` identifies a value returned through the client API.
`DERIVED_FROM_OBSERVATIONS` identifies a change between sampled values.
`GAMEPLAY_OBSERVED` identifies the user's manual loot observation.
`NOT_AVAILABLE` identifies an absent or ambiguous reading. The desktop
displays `UNKNOWN` or `NOT EXPOSED` for unavailable properties. A positive
generic stat is recorded as the raw client reading. Nonpositive generic
reads are withheld because the API can return zero when state is unavailable.
The separate AORebirth comparison shows authored values in their own column.
The observation path does not add RDB references to client observations.

## Selected NPC properties

| Property investigated | Availability and source | Interpretation boundary |
| --- | --- | --- |
| Target, NPC classification, in-play status | `Targeting.TargetChar`, `SimpleChar.IsNpc`, `IsPet`, and `IsInPlay`. Zoning is read from `Game.IsZoning`. | Client selection and availability. |
| Name | `Dynel.Name` when available. | Display names can be shared. |
| Character identity | `Dynel.Identity.Type` and `Instance` with bridge process, session and selection IDs. | Runtime identity. AORebirth template identity is unresolved. |
| Playfield | `Playfield.ModelIdentity.Instance`. | Client model identity. A model can have multiple runtime instances. |
| MonsterData | Positive `Dynel.GetStat(Stat.MonsterData)`. | Body resource stat. Family, equipment and loot require independent evidence. |
| Level | Positive `SimpleChar.Level`. | Available when the client populates it. |
| NPC family | Positive `Dynel.GetStat(Stat.NPCFamily)`. | Raw stat 455. The relation to authored hash families is unverified. |
| Breed, profession, side | `SimpleChar.Breed`, `Profession`, `Side`. | Raw positive values. |
| Current health and maximum health | `SimpleChar.Health` and `MaxHealth`. Zero current is retained only alongside a positive maximum. | Client reads can be incomplete. Corpse identity needs separate evidence. |
| Health percentage | `SimpleChar.HealthPercent` when current and maximum are available. | The installed getter scales its ratio by 100. |
| Current nano, maximum nano and percentage | `SimpleChar.Nano`, `MaxNano`, `NanoPercent` with the same availability rules. | Client readings. |
| Attack and defense | Positive `AddAllOff`, `AddAllDef`, and `AttackRange` through `Dynel.GetStat`. | Offense and defense modifiers are stat IDs 276 and 277. Full attack and defense ratings are unavailable. |
| Armor classes | Positive `ProjectileAC`, `MeleeAC`, `EnergyAC`, `ChemicalAC`, `RadiationAC`, `ColdAC`, `PoisonAC`, and `FireAC`. | Raw client stats. Actual damage mitigation is unverified. |
| Evades and resistance | Positive `DuckExp`, `DodgeRanged`, `EvadeClsC`, and `NanoResist`. | Raw client stats. |
| Initiatives | Positive `MeleeInit`, `RangedInit`, `PhysicalInit`, and `NanoCInit`. | Attack and casting cadence are unavailable. |
| Other candidate stats | Positive `RunSpeed` and `VisualProfession`. | RunSpeed is a stat. Client velocity is shown separately. |
| Coordinates | Finite `Dynel.Position.X/Y/Z`. | Client-local coordinates in the reported playfield. |
| Orientation and heading | Finite `Dynel.Rotation` quaternion and `Quaternion.Yaw`. | The installed yaw implementation uses `Atan2` and adds 2π to negative results. Heading is in radians. |
| Movement state and speed | `Dynel.MovementState`, `Velocity`, and `IsMoving`. | Client vehicle state. Velocity is shown in the API's raw units. |
| Patrol | `NOT EXPOSED`. | An authored patrol route remains unresolved. |
| Appearance IDs | Positive `Mesh`, `HeadMesh`, `HairMesh`, `BackMesh`, `ShoulderMesh`, `WeaponMesh`, `MonsterTexture`, `FaceTexture`, `HairTexture`, and `Scale`. | Raw appearance stats. The mesh-to-item association is unresolved. |
| Visible weapons and names | `SimpleChar.Weapons` reads the local weapon holder. Exposed entries can include hand slot, `Dynel.Identity`, and `Dynel.Name`. | Runtime weapon identity and display name. RDB template pairs and QL remain unavailable. |
| Armor, clothing slots and item template IDs | `NOT EXPOSED` by the selected NPC observation path. | Appearance IDs alone do not establish equipment identity. |
| Alive and attack state | `SimpleChar.IsAlive` with a health reading, plus `SimpleChar.IsAttacking`. | Client flags. A change is timestamped when sampled. |
| Observed damage, attack animation and timing | `NOT EXPOSED`. The timeline can show a changed health sample or attack flag. | Exact hit time, damage type, attack source and animation remain unresolved. |
| Weapon changes | Change in the exposed weapon-holder sample for the same observed selection. | `DERIVED_FROM_OBSERVATIONS`, with sample timestamp. |
| Nano casts | `NOT EXPOSED`. | Caster, cast time and outcome remain unresolved. |
| Buffs and debuffs | `SimpleChar.Buffs` reads a local list of nano template information. Up to 12 exposed IDs and names are shown. Changes can be recorded. | Beneficial and harmful effects are unclassified. Complete server effect state is unavailable. |
| Death and corpse identity | Alive-state changes can be sampled. Automatic corpse association is unavailable. | Enter an exact corpse identifier manually if known. |
| Loot name, low/high template IDs, QL, quantity and credits | Manual `OBSERVED_DROP`, `UNKNOWN_LOOT`, or `UNRESOLVED_ITEM` records for legitimately opened corpses. | Optional fields remain unknown when omitted. Each record describes the reported observation. |

## Original-client research used

The existing [NPC Inspect Probe](../aosharp_npc_inspect/README.md) documents a
native Inspect request and its responses. Phase 5 uses the passive target
bridge.

The AO Stripdown research files `Docs/AO_RB_ACTORPRESENTATION_035.md` and
`Docs/AO_RB_REMOTECHARVISUAL_001.md` describe remote appearance carriers and
the partial mapping from equipment resources to visible state.
`Docs/AO_RB_DEATHCORPSE_030.md` identifies separate actor and corpse
identities in a typed update. The installed AOSharp API has generic container
access. An ordinary opened-corpse association to the selected NPC was not
established through that API. The editor supports manual records.

## Local isolation and first manual test

The bridge publishes the latest sample in a per-process, per-session file on
a local drive. File readers reject stale samples and isolate multiple sessions.
Target locks also include a selection generation. Zoning, a cleared target, an
expired publication or a changed generation invalidates the lock. As with any
bounded sampler, a target transition entirely between samples may be missed.
The plugin does not determine the connected server. Confirm the intended AO
session when you choose Official mode.

1. Load the built bridge through the existing AOSharp loader in the session
   you are authorized to observe.
2. Open **Official AO — Read Only**. Select a client session if prompted, then
   select an NPC in the client and review the observed and unavailable fields.
3. Change target, then lock a target and change or clear it again. Confirm the
   locked selection becomes unavailable and unlock it to follow the new target.
4. During ordinary gameplay, manually record a legitimately opened corpse
   observation and export JSON and CSV. Check the source, timestamp, session,
   target, optional item fields and observation status.
5. Optionally compare against a local AORebirth `NpcTemplates.json`. Treat
   name and MonsterData matches as candidates. In-client field population and
   observed network behavior are manual acceptance items.
