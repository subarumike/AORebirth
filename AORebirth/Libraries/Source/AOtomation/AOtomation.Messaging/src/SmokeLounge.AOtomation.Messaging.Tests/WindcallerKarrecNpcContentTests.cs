namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Arete;
    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Subway.Quests;

    #endregion

    [TestClass]
    public class WindcallerKarrecNpcContentTests
    {
        [TestMethod]
        public void CaptureDefinesExactlyTheThreeQuestNpcsInPlayfield655()
        {
            WindcallerKarrecNpcDefinition[] definitions = WindcallerKarrecNpcContent.Definitions.ToArray();

            Assert.AreEqual(3, definitions.Length);
            CollectionAssert.AreEqual(
                new[]
                {
                    WindcallerKarrecNpcContent.KarrecSourceInstance,
                    WindcallerKarrecNpcContent.AnnoyingDudeSourceInstance,
                    WindcallerKarrecNpcContent.MaddyCardileSourceInstance
                },
                definitions.Select(definition => definition.SourceNpcInstance).ToArray());
            CollectionAssert.AreEqual(
                new[] { "Windcaller Karrec", "Annoying Dude", "Maddy Cardile" },
                definitions.Select(definition => definition.DisplayName).ToArray());
            Assert.IsTrue(definitions.All(definition => definition.PlayfieldId == 655));
            Assert.IsTrue(
                definitions.All(
                    definition => definition.Evidence == "AOSharpLiveCapture/20260717-223626"));
            WindcallerKarrecNpcDefinition missing;
            Assert.IsFalse(WindcallerKarrecNpcContent.TryGetBySourceInstance(12345, out missing));
        }

        [TestMethod]
        public void CapturedAppearancesAndInitialScfuWaypointsRemainExact()
        {
            WindcallerKarrecNpcDefinition karrec = WindcallerKarrecNpcContent.Karrec;
            WindcallerKarrecNpcDefinition annoying = WindcallerKarrecNpcContent.AnnoyingDude;
            WindcallerKarrecNpcDefinition maddy = WindcallerKarrecNpcContent.MaddyCardile;

            CollectionAssert.AreEqual(
                new[] { 1576, 1672, 1832 },
                new[] { karrec.AppearanceValue, annoying.AppearanceValue, maddy.AppearanceValue });
            CollectionAssert.AreEqual(
                new[] { 40818, 26103, 26090 },
                new[] { karrec.MonsterData, annoying.MonsterData, maddy.MonsterData });
            CollectionAssert.AreEqual(
                new[] { 121, 104, 121 },
                new[] { karrec.MonsterScale, annoying.MonsterScale, maddy.MonsterScale });
            CollectionAssert.AreEqual(
                new[] { 136, 103, 103 },
                new[] { karrec.NpcFamily, annoying.NpcFamily, maddy.NpcFamily });
            CollectionAssert.AreEqual(
                new[] { 200, 45, 200 },
                new[] { karrec.Level, annoying.Level, maddy.Level });
            CollectionAssert.AreEqual(
                new[] { 51008, 1958, 365 },
                new[] { karrec.Health, annoying.Health, maddy.Health });
            CollectionAssert.AreEqual(
                new uint[] { 170552011, 168512203, 168520395 },
                new[] { karrec.CapturedScfuFlags, annoying.CapturedScfuFlags, maddy.CapturedScfuFlags });
            CollectionAssert.AreEqual(
                new[] { 1, 0, 0 },
                new[] { karrec.VisibleTitle, annoying.VisibleTitle, maddy.VisibleTitle });
            Assert.AreEqual(28, karrec.CapturedScfuUnknown1.Count);
            Assert.AreEqual(28, annoying.CapturedScfuUnknown1.Count);
            Assert.AreEqual(28, maddy.CapturedScfuUnknown1.Count);
            Assert.AreEqual(5, karrec.Textures.Count);
            Assert.AreEqual(5, annoying.Textures.Count);
            Assert.AreEqual(5, maddy.Textures.Count);
            Assert.AreEqual(2, karrec.Meshes.Count);
            Assert.AreEqual(2, annoying.Meshes.Count);
            Assert.AreEqual(2, maddy.Meshes.Count);

            Assert.AreEqual(0, karrec.ScfuWaypoints.Count);
            Assert.AreEqual(1, annoying.ScfuWaypoints.Count);
            Assert.AreEqual(3185.87134f, annoying.ScfuWaypoints[0].X);
            Assert.AreEqual(963.378967f, annoying.ScfuWaypoints[0].Z);
            Assert.AreEqual(2, maddy.ScfuWaypoints.Count);
            Assert.AreEqual(3332.37524f, maddy.ScfuWaypoints[0].X);
            Assert.AreEqual(931.1814f, maddy.ScfuWaypoints[0].Z);
            Assert.AreEqual(3329.789f, maddy.ScfuWaypoints[1].X);
            Assert.AreEqual(932.215942f, maddy.ScfuWaypoints[1].Z);

            Assert.AreEqual(1, karrec.ActiveNanos.Count);
            Assert.AreEqual(53019, karrec.ActiveNanos[0].NanoIdentityType);
            Assert.AreEqual(0x3233F, karrec.ActiveNanos[0].NanoIdentityInstance);
            Assert.AreEqual(0, karrec.ActiveNanos[0].NanoInstance);
            Assert.AreEqual(29050327, karrec.ActiveNanos[0].Time1);
            Assert.AreEqual(29050327, karrec.ActiveNanos[0].Time2);
        }

        [TestMethod]
        public void ActiveCapturedPatrolProjectionEmitsCurrentPositionAndDestinationAfterMovementStarts()
        {
            WindcallerKarrecNpcDefinition maddy = WindcallerKarrecNpcContent.MaddyCardile;
            WindcallerKarrecNpcPatrolSegment activeSegment = maddy.PatrolSegments[0];
            float projectedX = (activeSegment.StartX + activeSegment.EndX) / 2.0f;
            float projectedY = (activeSegment.StartY + activeSegment.EndY) / 2.0f;
            float projectedZ = (activeSegment.StartZ + activeSegment.EndZ) / 2.0f;

            WindcallerKarrecNpcWaypointDefinition beforeMovementCoordinates =
                maddy.ResolveScfuCoordinates(
                    false,
                    maddy.X,
                    maddy.Y,
                    maddy.Z,
                    projectedX,
                    projectedY,
                    projectedZ);
            Assert.AreEqual(maddy.X, beforeMovementCoordinates.X);
            Assert.AreEqual(maddy.Y, beforeMovementCoordinates.Y);
            Assert.AreEqual(maddy.Z, beforeMovementCoordinates.Z);

            WindcallerKarrecNpcWaypointDefinition[] beforeMovement =
                maddy.ResolveScfuWaypoints(
                    false,
                    projectedX,
                    projectedY,
                    projectedZ,
                    activeSegment.EndX,
                    activeSegment.EndY,
                    activeSegment.EndZ);
            Assert.AreEqual(2, beforeMovement.Length);
            Assert.AreEqual(3332.37524f, beforeMovement[0].X);
            Assert.AreEqual(931.1814f, beforeMovement[0].Z);
            Assert.AreEqual(3329.789f, beforeMovement[1].X);
            Assert.AreEqual(932.215942f, beforeMovement[1].Z);

            WindcallerKarrecNpcWaypointDefinition whileMovingCoordinates =
                maddy.ResolveScfuCoordinates(
                    true,
                    maddy.X,
                    maddy.Y,
                    maddy.Z,
                    projectedX,
                    projectedY,
                    projectedZ);
            Assert.AreEqual(projectedX, whileMovingCoordinates.X);
            Assert.AreEqual(projectedY, whileMovingCoordinates.Y);
            Assert.AreEqual(projectedZ, whileMovingCoordinates.Z);

            WindcallerKarrecNpcWaypointDefinition[] whileMoving =
                maddy.ResolveScfuWaypoints(
                    true,
                    projectedX,
                    projectedY,
                    projectedZ,
                    activeSegment.EndX,
                    activeSegment.EndY,
                    activeSegment.EndZ);
            Assert.AreEqual(2, whileMoving.Length);
            Assert.AreEqual(projectedX, whileMoving[0].X);
            Assert.AreEqual(projectedY, whileMoving[0].Y);
            Assert.AreEqual(projectedZ, whileMoving[0].Z);
            Assert.AreEqual(activeSegment.EndX, whileMoving[1].X);
            Assert.AreEqual(activeSegment.EndY, whileMoving[1].Y);
            Assert.AreEqual(activeSegment.EndZ, whileMoving[1].Z);

            Assert.AreEqual(
                0,
                WindcallerKarrecNpcContent.Karrec.ResolveScfuWaypoints(
                    true,
                    projectedX,
                    projectedY,
                    projectedZ,
                    activeSegment.EndX,
                    activeSegment.EndY,
                    activeSegment.EndZ).Length);
        }

        [TestMethod]
        public void AnnoyingDudePatrolPreservesTheCapturedSixteenDestinationCycle()
        {
            WindcallerKarrecNpcPatrolSegment[] segments =
                WindcallerKarrecNpcContent.AnnoyingDude.PatrolSegments.ToArray();

            Assert.AreEqual(16, segments.Length);
            Assert.IsTrue(segments.All(segment => segment.MoveMode == 24));
            CollectionAssert.AreEqual(
                new[]
                {
                    3183.13818f, 3179.62305f, 3176.29053f, 3175.02539f,
                    3176.85815f, 3179.3623f, 3183.01025f, 3188.08887f,
                    3192.25293f, 3193.98438f, 3196.06567f, 3194.46362f,
                    3191.19824f, 3189.87671f, 3188.67847f, 3185.70703f
                },
                segments.Select(segment => segment.EndX).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    963.90625f, 966.895264f, 966.785339f, 964.88031f,
                    960.427917f, 956.972656f, 955.028564f, 955.118347f,
                    956.632202f, 963.076416f, 966.216187f, 969.180298f,
                    969.312805f, 965.374878f, 963.080872f, 963.35199f
                },
                segments.Select(segment => segment.EndZ).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    1.2518461, 3.0699980, 2.1199181, 1.1513932,
                    2.8999266, 2.7219631, 2.7397159, 3.6405055,
                    2.5103682, 4.3023294, 2.4452929, 1.9668067,
                    1.8002349, 2.2201903, 2.1293892, 4.2700076
                },
                segments.Select(segment => segment.DelayAfterSeconds).ToArray());
            Assert.AreEqual(3185.87134f, segments[0].StartX);
            Assert.AreEqual(963.378967f, segments[0].StartZ);
            Assert.AreNotEqual(segments[0].EndX, segments[1].StartX);
        }

        [TestMethod]
        public void MaddyCardilePatrolPreservesTheCapturedNineteenDestinationCycle()
        {
            WindcallerKarrecNpcPatrolSegment[] segments =
                WindcallerKarrecNpcContent.MaddyCardile.PatrolSegments.ToArray();

            Assert.AreEqual(19, segments.Length);
            Assert.IsTrue(segments.All(segment => segment.MoveMode == 24));
            CollectionAssert.AreEqual(
                new[]
                {
                    3328.16504f, 3334.11792f, 3344.75806f, 3352.49878f,
                    3352.76831f, 3350.84814f, 3347.55029f, 3336.63647f,
                    3332.91382f, 3331.51709f, 3326.88892f, 3323.97485f,
                    3321.20337f, 3320.11963f, 3326.41333f, 3333.34058f,
                    3337.28589f, 3334.62817f, 3329.78906f
                },
                segments.Select(segment => segment.EndX).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    938.848145f, 943.119141f, 945.087158f, 941.203552f,
                    932.732605f, 924.866455f, 920.69873f, 918.530457f,
                    915.320435f, 913.763184f, 915.19574f, 918.013f,
                    920.767212f, 923.598633f, 925.535889f, 924.618835f,
                    927.924072f, 930.946777f, 932.215942f
                },
                segments.Select(segment => segment.EndZ).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    4.3708276, 4.7539665, 6.8341767, 5.5713148, 5.4357095,
                    5.2456334, 3.8700161, 6.8971459, 3.0849565, 1.5295171,
                    2.8867470, 2.6604726, 2.5598438, 1.9879705, 3.7351056,
                    4.7399797, 3.2594041, 2.1707049, 3.2493866
                },
                segments.Select(segment => segment.DelayAfterSeconds).ToArray());
            Assert.AreEqual(3331.09204f, segments[0].StartX);
            Assert.AreEqual(931.694885f, segments[0].StartZ);
            Assert.AreNotEqual(segments[0].EndX, segments[1].StartX);
        }

        [TestMethod]
        public void EverySpawnDefinitionResolvesToCheckedInDialogueContent()
        {
            AreteFrameworkRegistries registries =
                AreteFrameworkBootstrap.InitializeCheckedInContent(AppDomain.CurrentDomain.BaseDirectory);

            Assert.IsTrue(registries.IsValid);
            foreach (WindcallerKarrecNpcDefinition definition in WindcallerKarrecNpcContent.Definitions)
            {
                DialogueNpcEntry npc;
                Assert.IsTrue(
                    registries.DialogueRegistry.TryGetNpc(definition.SourceNpcIdentity, out npc),
                    definition.SourceNpcIdentity);
                Assert.IsNotNull(npc);
            }
        }

        [TestMethod]
        public void RuntimeRegistryPreventsDuplicateEntriesAndTearsDownByPlayfield()
        {
            var playfield = new Identity { Type = IdentityType.Playfield, Instance = 655 };
            var otherPlayfield = new Identity { Type = IdentityType.Playfield, Instance = 127 };
            WindcallerKarrecNpcRuntimeRegistry.RemoveForPlayfield(playfield);
            WindcallerKarrecNpcRuntimeRegistry.RemoveForPlayfield(otherPlayfield);

            try
            {
                for (int index = 0; index < WindcallerKarrecNpcContent.Definitions.Count; index++)
                {
                    var runtimeIdentity =
                        new Identity
                        {
                            Type = IdentityType.CanbeAffected,
                            Instance = 1000000 + index
                        };
                    var runtime =
                        new WindcallerKarrecNpcRuntimeDefinition(
                            playfield,
                            runtimeIdentity,
                            WindcallerKarrecNpcContent.Definitions[index]);
                    WindcallerKarrecNpcRuntimeRegistry.Register(runtime);
                    WindcallerKarrecNpcRuntimeRegistry.Register(runtime);
                }

                Assert.IsTrue(WindcallerKarrecNpcRuntimeRegistry.ContainsPlayfield(playfield));
                Assert.IsFalse(WindcallerKarrecNpcRuntimeRegistry.ContainsPlayfield(otherPlayfield));
                Assert.AreEqual(3, WindcallerKarrecNpcRuntimeRegistry.CountForPlayfield(playfield));

                WindcallerKarrecNpcRuntimeDefinition resolved;
                Assert.IsTrue(WindcallerKarrecNpcRuntimeRegistry.TryGet(1000001, out resolved));
                Assert.AreSame(WindcallerKarrecNpcContent.AnnoyingDude, resolved.Content);
                Assert.IsTrue(
                    WindcallerKarrecNpcRuntimeRegistry.TryGet(
                        playfield,
                        new Identity { Type = IdentityType.CanbeAffected, Instance = 1000001 },
                        out resolved));
                Assert.IsFalse(
                    WindcallerKarrecNpcRuntimeRegistry.TryGet(
                        otherPlayfield,
                        new Identity { Type = IdentityType.CanbeAffected, Instance = 1000001 },
                        out resolved));
                Assert.IsFalse(
                    WindcallerKarrecNpcRuntimeRegistry.TryGet(
                        playfield,
                        new Identity { Type = IdentityType.Terminal, Instance = 1000001 },
                        out resolved));

                WindcallerKarrecNpcRuntimeRegistry.RemoveForPlayfield(playfield);
                Assert.IsFalse(WindcallerKarrecNpcRuntimeRegistry.ContainsPlayfield(playfield));
                Assert.AreEqual(0, WindcallerKarrecNpcRuntimeRegistry.CountForPlayfield(playfield));
            }
            finally
            {
                WindcallerKarrecNpcRuntimeRegistry.RemoveForPlayfield(playfield);
                WindcallerKarrecNpcRuntimeRegistry.RemoveForPlayfield(otherPlayfield);
            }
        }
    }
}
