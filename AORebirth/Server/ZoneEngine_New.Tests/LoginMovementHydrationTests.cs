namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Movement;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.WorldSimulation;

[TestClass]
public sealed class LoginMovementHydrationTests
{
    const int NanoId = 700101;

    [TestMethod]
    public void Fresh_login_with_saved_sitting_mode_can_cast_until_the_player_sits()
    {
        CharacterHydrationResult hydration = SavedSeatedCharacter();
        NanoSpell spell = TestNanos.Create(NanoId, durationCentiseconds: 0, attackDelay: 400);
        var items = new StubItemBuilder().Add(spell);
        var gameData = new GameDataStore(new StubLogger(), null, Path.Combine(AppContext.BaseDirectory,
            "LoginMovementFixture-" + Guid.NewGuid().ToString("N")));
        using var services = new ServiceCollection()
            .AddSingleton<IItemBuilder>(items)
            .AddSingleton<IGameData>(gameData)
            .AddSingleton<IItemTemplateCatalog>(new StubCatalog())
            .AddSingleton<WorldSimulationAccess>()
            .BuildServiceProvider();

        var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(Playfield).GetField("_serviceProvider", Fields)!.SetValue(playfield, services);
        typeof(Playfield).GetField("<Identity>k__BackingField", Fields)!.SetValue(playfield,
            new Identity { Type = IdentityType.Playfield2, Instance = hydration.Character.Playfield });
        Player player = TestWorld.CreatePlayer(hydration.Character.Id);
        player.Playfield = playfield;
        new PlayerHydrator(items, gameData).Apply(player, hydration);
        player.Motor.Vehicle.LandNow(player.Position.yf);

        Assert.AreEqual((byte)MovementState.Run, player.BuildSpawnMessage().MovementStatus.ModeId);
        DateTime now = DateTime.UtcNow;
        Assert.AreEqual(NanoCastRefusal.None, NanoRuntime.TryStartCast(player, NanoId, player.Identity, now));
        var full = player.BuildFullCharacterMessage();
        Assert.AreEqual((byte)MovementState.Run, full.Stats3
            .Single(stat => stat.Value1 == (byte)CharacterStat.CurrentMovementMode).Value2);

        player.CancelNanoCast();
        Assert.IsTrue(player.Motor.ApplyAction(MovementAction.SwitchToSit));
        Assert.AreEqual(NanoCastRefusal.MustStandUp, NanoRuntime.TryStartCast(player, NanoId, player.Identity, now));
    }

    static CharacterHydrationResult SavedSeatedCharacter()
    {
        var stats = new Dictionary<CharacterStat, int>
        {
            [CharacterStat.Flags] = 0x00081241, [CharacterStat.Breed] = 1, [CharacterStat.Sex] = 2,
            [CharacterStat.Profession] = 1, [CharacterStat.Fatness] = 0, [CharacterStat.Race] = 1,
            [CharacterStat.HeadMesh] = 40683, [CharacterStat.VisualFlags] = 31, [CharacterStat.Scale] = 100,
            [CharacterStat.Level] = 1, [CharacterStat.TitleLevel] = 1, [CharacterStat.Side] = 0,
            [CharacterStat.Expansion] = 171,
            [CharacterStat.Strength] = 6, [CharacterStat.Agility] = 6, [CharacterStat.Stamina] = 6,
            [CharacterStat.Intelligence] = 6, [CharacterStat.Sense] = 6, [CharacterStat.Psychic] = 6,
            [CharacterStat.BodyDevelopment] = 5, [CharacterStat.NanoPool] = 5,
            [CharacterStat.MaxHealth] = 31, [CharacterStat.Health] = 25,
            [CharacterStat.MaxNanoEnergy] = 29, [CharacterStat.CurrentNano] = 20,
            [CharacterStat.RunSpeed] = 100, [CharacterStat.CurrentMovementMode] = (int)MovementState.Sit,
        };
        return new CharacterHydrationResult
        {
            Character = new CharacterRecord
            {
                Id = 1, Name = "LoginMovementFixture", Playfield = 4582, X = 100, Z = 100, HeadingW = 1,
            },
            Stats = stats.Select(pair => new StatRecord { StatId = (int)pair.Key, StatValue = pair.Value }).ToArray(),
            UploadedNanoIds = [NanoId],
        };
    }
}
