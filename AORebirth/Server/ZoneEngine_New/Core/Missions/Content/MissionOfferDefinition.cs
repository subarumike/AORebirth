namespace ZoneEngine_New.Core.Missions;

using System.Collections.Immutable;
using System.Text.Json.Serialization;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

// Version 1 preserves the typed fields of the supported QuestAlternative cohort.
// Empty arrays and zero-only fields omitted here are checked by the native-codec
// round trip against every historical response. UnknownN names follow AOtomation.
internal sealed record MissionOfferContent(int FormatVersion, MissionOfferSource Provenance,
    ImmutableArray<MissionResponseDefinition> Responses);

internal sealed record MissionOfferSource(string InputSha256, string SourceRevision);

[method: JsonConstructor]
internal readonly record struct MissionWireIdentity(IdentityType Type, int Instance)
{
    internal Identity Create() => new() { Type = Type, Instance = Instance };
}

internal sealed record MissionResponseDefinition(string BodySha256, MissionWireIdentity SourceCharacter,
    MissionWireIdentity SourceTerminal, byte Unknown, byte VersionId, byte LevelSlider, byte GoodBadSlider,
    byte OrderChaosSlider, byte OpenHiddenSlider, byte PhysicalMysticalSlider, byte HeadOnStealthSlider,
    byte MoneyExperienceSlider, int Unknown4, byte Unknown5, ImmutableArray<MissionOfferDefinition> Offers)
{
    internal QuestAlternativeMessage Create(Identity character, Identity terminal) => new()
    {
        Identity = character,
        MissionTerminalIdentity = terminal,
        Unknown = Unknown,
        VersionId = VersionId,
        LevelSlider = LevelSlider,
        GoodBadSlider = GoodBadSlider,
        OrderChaosSlider = OrderChaosSlider,
        OpenHiddenSlider = OpenHiddenSlider,
        PhysicalMysticalSlider = PhysicalMysticalSlider,
        HeadOnStealthSlider = HeadOnStealthSlider,
        MoneyExperienceSlider = MoneyExperienceSlider,
        Unknown4 = Unknown4,
        Unknown5 = Unknown5
    };
}

internal sealed record MissionOfferDefinition(MissionWireIdentity SourceIdentity, int Unknown1, string ShortInfo,
    string Info, MissionWireIdentity Unknown5, int RewardDescriptorVersion, int CashReward, int ExperienceReward,
    MissionOfferRewardDefinition Reward, int UnknownHash, int Quality, MissionWireIdentity Unknown14,
    int MissionIconId, int Unknown16, MissionOfferActionDefinition Action, int Unknown20, int Unknown22,
    MissionWireIdentity Unknown23, int Unknown26, byte Unknown27)
{
    internal QuestInfo Create() => new()
    {
        QuestIdentity = SourceIdentity.Create(),
        Unknown1 = Unknown1,
        ShortInfo = ShortInfo,
        Info = Info,
        Unknown5 = Unknown5.Create(),
        RewardDescriptorVersion = RewardDescriptorVersion,
        CashReward = CashReward,
        ExperienceReward = ExperienceReward,
        UnknownIdentities1 = [],
        UnknownIdentities2 = [],
        ItemRewards = [Reward.Create()],
        UnknownHash = UnknownHash,
        Quality = Quality,
        Unknown14 = Unknown14.Create(),
        MissionIconId = MissionIconId,
        Unknown16 = Unknown16,
        QuestActions = [Action.Create()],
        Unknown17 = [],
        Unknown18 = [],
        Unknown19 = [],
        CharInfos = [],
        Unknown20 = Unknown20,
        UnknownIdentities20 = [],
        Unknown22 = Unknown22,
        Unknown23 = Unknown23.Create(),
        QuestIdentities = [],
        Unknown26 = Unknown26,
        FactionInfo = [],
        Unknown27 = Unknown27
    };
}

internal sealed record MissionOfferRewardDefinition(int LowId, int HighId, int Quality, int Unknown1)
{
    internal QuestItemShort Create() => new() { LowId = LowId, HighId = HighId, Quality = Quality, Unknown1 = Unknown1 };
}

internal sealed record MissionOfferActionDefinition(int Version, MissionWireIdentity Action, MissionWireIdentity Unknown1,
    MissionWireIdentity Unknown2, int UnknownHash15, MissionWireIdentity Playfield, int Unknown18, int Unknown19,
    float X, float Y, float Z)
{
    internal QuestActionList Create() => new()
    {
        Version = Version,
        Action = Action.Create(),
        Unknown1 = Unknown1.Create(),
        Unknown2 = Unknown2.Create(),
        UnknownHash15 = UnknownHash15,
        Playfield = Playfield.Create(),
        Unknown18 = Unknown18,
        Unknown19 = Unknown19,
        X = X,
        Y = Y,
        Z = Z
    };
}
