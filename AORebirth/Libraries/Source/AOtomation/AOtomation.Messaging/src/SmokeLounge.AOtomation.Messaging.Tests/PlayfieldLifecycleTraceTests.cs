// This source code is licensed under the MIT license that can be found in the LICENSE file.

namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Playfields;

    #endregion

    [TestClass]
    public class PlayfieldLifecycleTraceTests
    {
        [TestMethod]
        public void PrivateCityReadyInitKeepsOrgStateBeforeFullCharacterAndReadyBlockAfter()
        {
            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                RecordExpected(
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.ExpectedPrivateCityReadyInitOrder);

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.ExpectedPrivateCityReadyInitOrder);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityReadyBlockBegin,
                    PlayfieldLifecycleTrace.StagePrivateCityOrgInfoPacket);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityOrgInfoPacket,
                    PlayfieldLifecycleTrace.StagePrivateCityFullCharacter);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityOrgInitSent,
                    PlayfieldLifecycleTrace.StagePrivateCityFullCharacter);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityFullCharacter,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllTowers);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllTowers,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllCities);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllCities,
                    PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent,
                    PlayfieldLifecycleTrace.StagePrivateCityReadyBlockEnd);
            }
        }

        [TestMethod]
        public void PrivateCityReadyInitRecorderGuardsPacketMessageOrderAndDetails()
        {
            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                RecordPrivateCityReadyInitCurrentPacketSequence();

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.ExpectedPrivateCityReadyInitOrder);
                Assert.AreEqual(
                    PlayfieldLifecycleTrace.ExpectedPrivateCityReadyInitOrder.Length,
                    CountFlow(capture.Events, PlayfieldLifecycleTrace.FlowPrivateCityReadyInit));
                Assert.AreEqual(
                    4,
                    CountStage(capture.Events, PlayfieldLifecycleTrace.StagePrivateCitySocialStatus),
                    "Private-city org init must preserve the captured repeated SocialStatus=4 sequence.");

                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityReadyBlockBegin,
                    PlayfieldLifecycleTrace.MessagePrivateCityReadyBlockBegin);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCitySimpleCharFullUpdateBroadcast,
                    PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityOrgInfoPacket,
                    PlayfieldLifecycleTrace.MessageOrgInfoPacket);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityOrgInitSent,
                    PlayfieldLifecycleTrace.MessagePrivateCityOrgInitSent);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityFullCharacter,
                    PlayfieldLifecycleTrace.MessageFullCharacter);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllTowers,
                    PlayfieldLifecycleTrace.MessagePlayfieldAllTowers);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllCities,
                    PlayfieldLifecycleTrace.MessagePlayfieldAllCities);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent,
                    PlayfieldLifecycleTrace.MessagePrivateCityTowersCitiesSent);
                AssertMessageForStage(
                    capture.Events,
                    PlayfieldLifecycleTrace.StagePrivateCityReadyBlockEnd,
                    PlayfieldLifecycleTrace.MessagePrivateCityReadyBlockEnd);

                Assert.IsTrue(
                    HasDetail(capture.Events, PlayfieldLifecycleTrace.StagePrivateCityOrgInfoPacket, "Est. 2024"),
                    "Private-city org info must remain before FullCharacter.");
                Assert.IsTrue(
                    HasDetailContains(capture.Events, PlayfieldLifecycleTrace.StagePrivateCityOrgInitSent, "org=1970177"),
                    "Private-city org init summary must preserve the captured organization identity.");
                Assert.IsTrue(
                    HasDetailContains(capture.Events, PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent, "cityPayloadBytes=0"),
                    "Captured Montroyal private-city ready block currently sends the empty towers/cities fallback.");
            }
        }

        [TestMethod]
        public void SamePlayfieldVisibilityKeepsCharInPlayAndExistingPlayerSnapshotOrder()
        {
            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                RecordExpected(
                    PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                    PlayfieldLifecycleTrace.ExpectedCharInPlayEntryOrder);
                RecordExpected(
                    PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                    PlayfieldLifecycleTrace.ExpectedSamePlayfieldVisibilityOrder);

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                    PlayfieldLifecycleTrace.ExpectedCharInPlayEntryOrder);
                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                    PlayfieldLifecycleTrace.ExpectedSamePlayfieldVisibilityOrder);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageVisibilityJoinerReady,
                    PlayfieldLifecycleTrace.StageExistingCharacterSimpleCharFullUpdate);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageExistingCharacterSimpleCharFullUpdate,
                    PlayfieldLifecycleTrace.StageExistingCharacterCharInPlay);
            }
        }

        [TestMethod]
        public void CleaningRobotDeathOrderIncludesStopFightDeathCorpseAndDespawnScheduling()
        {
            Identity attacker = new Identity { Type = IdentityType.CanbeAffected, Instance = 1001 };
            Identity robot = new Identity { Type = IdentityType.CanbeAffected, Instance = 2001 };
            Identity corpse = new Identity { Type = IdentityType.Corpse, Instance = 3001 };

            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageAttackerStopFight,
                    PlayfieldLifecycleTrace.MessageStopFight,
                    attacker,
                    "deadTarget=" + robot);
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageRobotStopFight,
                    PlayfieldLifecycleTrace.MessageStopFight,
                    robot);
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageCharacterActionDeathParameter2,
                    PlayfieldLifecycleTrace.MessageCharacterActionDeath,
                    robot,
                    "Parameter2=500");
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageCorpseSpawnScheduled,
                    "CorpseSpawnScheduled",
                    corpse,
                    "deadNpc=" + robot + " delayMs=600");
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageDeadNpcDespawnScheduled,
                    "DeadNpcDespawnScheduled",
                    robot,
                    "delayMs=10000");
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.StageCorpseFullUpdate,
                    PlayfieldLifecycleTrace.MessageCorpseFullUpdate,
                    corpse,
                    "deadNpc=" + robot);

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowCleaningRobotDeathCorpseDespawn,
                    PlayfieldLifecycleTrace.ExpectedCleaningRobotDeathOrder);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageAttackerStopFight,
                    PlayfieldLifecycleTrace.StageRobotStopFight);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageRobotStopFight,
                    PlayfieldLifecycleTrace.StageCharacterActionDeathParameter2);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageCharacterActionDeathParameter2,
                    PlayfieldLifecycleTrace.StageCorpseFullUpdate);
                Assert.IsTrue(
                    HasDetail(capture.Events, PlayfieldLifecycleTrace.StageCharacterActionDeathParameter2, "Parameter2=500"),
                    "Robot death trace must preserve captured CharacterAction Death Parameter2=500.");
            }
        }

        [TestMethod]
        public void CleaningRobotNpcAttackOrderKeepsSpecialAttackWeaponBeforeAttackInfo()
        {
            Identity robot = new Identity { Type = IdentityType.CanbeAffected, Instance = 2001 };
            Identity target = new Identity { Type = IdentityType.CanbeAffected, Instance = 1001 };

            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                    PlayfieldLifecycleTrace.StageRobotSpecialAttackWeaponContext,
                    PlayfieldLifecycleTrace.MessageSpecialAttackWeapon,
                    robot,
                    "target=" + target);
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                    PlayfieldLifecycleTrace.StageRobotAttackStartContext,
                    PlayfieldLifecycleTrace.MessageAttack,
                    robot,
                    "target=" + target);
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                    PlayfieldLifecycleTrace.StageRobotAttackInfo,
                    PlayfieldLifecycleTrace.MessageAttackInfo,
                    robot,
                    "target=" + target);

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowCleaningRobotNpcAttack,
                    PlayfieldLifecycleTrace.ExpectedCleaningRobotNpcAttackOrder);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageRobotSpecialAttackWeaponContext,
                    PlayfieldLifecycleTrace.StageRobotAttackStartContext);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageRobotAttackStartContext,
                    PlayfieldLifecycleTrace.StageRobotAttackInfo);
            }
        }

        [TestMethod]
        public void NpcCorpseLifecycleRulesPreserveCapturedCleaningRobotDeathTimings()
        {
            Assert.AreEqual(
                600,
                (int)NpcCorpseLifecycleRules.CorpseSpawnDelay.TotalMilliseconds,
                "Cleaning robot corpse spawn delay must stay capture-backed.");
            Assert.AreEqual(
                10000,
                (int)NpcCorpseLifecycleRules.DeadNpcDespawnDelay.TotalMilliseconds,
                "Dead NPC despawn delay must stay capture-backed.");
            Assert.AreEqual(
                500,
                NpcCorpseLifecycleRules.CapturedCleaningRobotDeathActionParameter2,
                "Cleaning robot CharacterAction Death Parameter2 must stay capture-backed.");
        }

        [TestMethod]
        public void NpcCombatAttackRulesPreserveCapturedCleaningRobotContextDecision()
        {
            Assert.AreEqual(10, NpcCombatAttackRules.CapturedCleaningRobotRightHandDamage);
            Assert.AreEqual(8, NpcCombatAttackRules.CapturedCleaningRobotLeftHandDamage);
            Assert.AreEqual(
                2700,
                (int)(NpcCombatAttackRules.CapturedCleaningRobotCombatTickSeconds * 1000));
            Assert.IsTrue(
                NpcCombatAttackRules.ShouldSendCapturedCleaningRobotAttackStartContext(
                    true,
                    false,
                    null,
                    1001));
            Assert.IsFalse(
                NpcCombatAttackRules.ShouldSendCapturedCleaningRobotAttackStartContext(
                    true,
                    false,
                    1001,
                    1001));
            Assert.IsFalse(
                NpcCombatAttackRules.ShouldSendCapturedCleaningRobotAttackStartContext(
                    true,
                    true,
                    null,
                    1001));
        }

        [TestMethod]
        public void CapturedAreteRobotContentProviderPreservesSpawnDefinitions()
        {
            var provider = new CapturedAreteRobotContentProvider();
            CapturedAreteRobotSpawnDefinition[] spawns = provider.GetSpawnDefinitions();

            Assert.AreEqual(7, spawns.Length);
            Assert.AreEqual("Malfunctioning Cleaning Robot", CapturedAreteRobotContentProvider.RobotName);
            Assert.AreEqual(297023, CapturedAreteRobotContentProvider.MonsterData);
            Assert.AreEqual(0x79225E7C, spawns[0].SourceInstance);
            Assert.AreEqual(12, spawns[0].Health);
            Assert.AreEqual(1, spawns[0].Level);
            Assert.AreEqual(6, spawns[0].RunSpeed);
            Assert.AreEqual(3617.86938f, spawns[0].X);
            Assert.AreEqual(51.7449989f, spawns[0].Y);
            Assert.AreEqual(784.657471f, spawns[0].Z);
            Assert.AreEqual(3622.77563f, spawns[0].PatrolX);
            Assert.AreEqual(52.5f, spawns[0].PatrolY);
            Assert.AreEqual(798.800964f, spawns[0].PatrolZ);
        }

        [TestMethod]
        public void CapturedAreteRobotContentProviderPreservesPatrolReplayPathAndMissingFileFallback()
        {
            Assert.AreEqual(
                @"Content\Captured\Arete\cleaning_robot_patrol_replay.csv",
                CapturedAreteRobotContentProvider.PatrolReplayRelativePath);
            Assert.AreEqual(
                @"tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260629-193121\movement-packets.csv",
                CapturedAreteRobotContentProvider.EvidenceCapturePatrolReplayRelativePath);

            var provider = new CapturedAreteRobotContentProvider(
                new[] { Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "movement-packets.csv") });

            Assert.AreEqual(string.Empty, provider.FindPatrolReplayPath());
            Assert.AreEqual(0, provider.GetPatrolReplaySegments(0x79225E7C).Length);
        }

        [TestMethod]
        public void CapturedAreteRobotContentProviderLoadsCommittedPatrolReplayData()
        {
            var provider = new CapturedAreteRobotContentProvider();
            string replayPath = provider.FindPatrolReplayPath();

            Assert.IsTrue(File.Exists(replayPath));
            Assert.IsTrue(
                replayPath.IndexOf("tools-temp", StringComparison.OrdinalIgnoreCase) < 0,
                "Runtime replay data must load from committed content, not tools-temp captures.");

            Assert.AreEqual(35, provider.GetPatrolReplaySegments(0x79225E7D).Length);
            Assert.AreEqual(40, provider.GetPatrolReplaySegments(0x79225E7C).Length);
            Assert.AreEqual(38, provider.GetPatrolReplaySegments(0x79225E77).Length);
            Assert.AreEqual(31, provider.GetPatrolReplaySegments(0x79225E7A).Length);
            Assert.AreEqual(39, provider.GetPatrolReplaySegments(0x79225E78).Length);
            Assert.AreEqual(29, provider.GetPatrolReplaySegments(0x79225E79).Length);
            Assert.AreEqual(18, provider.GetPatrolReplaySegments(0x79225E76).Length);

            CapturedAreteRobotPatrolReplaySegment first =
                provider.GetPatrolReplaySegments(0x79225E7D)[0];
            Assert.AreEqual(3605.55493f, first.StartX);
            Assert.AreEqual(51.7449989f, first.StartY);
            Assert.AreEqual(773.164246f, first.StartZ);
            Assert.AreEqual(3602.2915f, first.EndX);
            Assert.AreEqual(52.5f, first.EndY);
            Assert.AreEqual(787.929504f, first.EndZ);

            CapturedAreteRobotPatrolReplaySegment[] lastRoute =
                provider.GetPatrolReplaySegments(0x79225E7C);
            CapturedAreteRobotPatrolReplaySegment last = lastRoute[lastRoute.Length - 1];
            Assert.AreEqual(3612.93481f, last.StartX);
            Assert.AreEqual(52.1349983f, last.StartY);
            Assert.AreEqual(787.84082f, last.StartZ);
            Assert.AreEqual(3611.29053f, last.EndX);
            Assert.AreEqual(52.5f, last.EndY);
            Assert.AreEqual(778.074585f, last.EndZ);
        }

        [TestMethod]
        public void NpcPatrolReplayCoordinatorAssignsCapturedReplaySegmentsFromProvider()
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.WriteAllLines(
                path,
                new[]
                {
                    "CapturedUtc,MessageType,SourceInstance,FollowKind,CurrentX,CurrentY,CurrentZ,DestinationX,DestinationY,DestinationZ",
                    "2026-06-29T19:31:21.0000000Z,FollowTarget,79225E7C,NpcPath,1,2,3,4,5,6",
                    "2026-06-29T19:31:22.5000000Z,FollowTarget,79225E7C,NpcPath,4,5,6,7,8,9"
                });

            try
            {
                var provider = new CapturedAreteRobotContentProvider(new[] { path });
                var coordinator = new NpcPatrolReplayCoordinator(provider);
                NpcPatrolReplaySegment[] assigned = null;

                coordinator.AssignCapturedAreteRobotReplay(
                    0x79225E7C,
                    segments => assigned = segments);

                Assert.IsNotNull(assigned);
                Assert.AreEqual(2, assigned.Length);
                Assert.AreEqual(1.5, assigned[0].DelayAfterSeconds);
                Assert.AreEqual(1f, assigned[0].StartX);
                Assert.AreEqual(2f, assigned[0].StartY);
                Assert.AreEqual(3f, assigned[0].StartZ);
                Assert.AreEqual(4f, assigned[0].EndX);
                Assert.AreEqual(5f, assigned[0].EndY);
                Assert.AreEqual(6f, assigned[0].EndZ);
                Assert.AreEqual(0.25, assigned[1].DelayAfterSeconds);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void NpcPatrolReplayCoordinatorAssignsEmptyReplayForMissingProviderData()
        {
            var provider = new CapturedAreteRobotContentProvider(
                new[] { Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "movement-packets.csv") });
            var coordinator = new NpcPatrolReplayCoordinator(provider);
            NpcPatrolReplaySegment[] assigned = null;

            coordinator.AssignCapturedAreteRobotReplay(
                0x79225E7C,
                segments => assigned = segments);

            Assert.IsNotNull(assigned);
            Assert.AreEqual(0, assigned.Length);
        }

        [TestMethod]
        public void CapturedAreteRobotSpawnOrchestrationTraceKeepsSetupReplayAndScfuOrder()
        {
            var provider = new CapturedAreteRobotContentProvider();
            var coordinator = new NpcPatrolReplayCoordinator(provider);
            CapturedAreteRobotSpawnDefinition[] spawns = provider.GetSpawnDefinitions();
            CapturedAreteRobotSpawnDefinition spawn = spawns[0];
            Identity playfield = new Identity { Type = IdentityType.Playfield, Instance = 6553 };
            Identity robot = new Identity { Type = IdentityType.CanbeAffected, Instance = 2001 };
            string spawnCreatedDetail =
                PlayfieldLifecycleTrace.FormatCapturedAreteRobotSpawnCreatedDetail(
                    spawn.SourceInstance,
                    CapturedAreteRobotContentProvider.MonsterData,
                    spawn.Health,
                    spawn.Level,
                    spawn.RunSpeed,
                    spawn.X,
                    spawn.Y,
                    spawn.Z,
                    spawn.PatrolX,
                    spawn.PatrolY,
                    spawn.PatrolZ);

            using (PlayfieldLifecycleCapture capture = PlayfieldLifecycleTrace.Capture())
            {
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnRowsLoaded,
                    PlayfieldLifecycleTrace.MessageCapturedAreteRobotSpawnRowsLoaded,
                    playfield,
                    PlayfieldLifecycleTrace.FormatCapturedAreteRobotSpawnRowsDetail(
                        spawns.Length,
                        CapturedAreteRobotContentProvider.MonsterData));
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnCreated,
                    PlayfieldLifecycleTrace.MessageCapturedAreteRobotSpawnCreated,
                    robot,
                    spawnCreatedDetail);

                NpcPatrolReplaySegment[] assigned = null;
                coordinator.AssignCapturedAreteRobotReplay(spawn.SourceInstance, segments => assigned = segments);
                Assert.IsNotNull(assigned);
                Assert.AreEqual(40, assigned.Length);

                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotPatrolReplayAssigned,
                    PlayfieldLifecycleTrace.MessageCapturedAreteRobotPatrolReplayAssigned,
                    robot,
                    PlayfieldLifecycleTrace.FormatCapturedAreteRobotPatrolReplayAssignedDetail(
                        spawn.SourceInstance,
                        assigned.Length));
                PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSimpleCharFullUpdateBroadcast,
                    PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate,
                    robot,
                    PlayfieldLifecycleTrace.FormatCapturedAreteRobotSimpleCharFullUpdateDetail(spawn.SourceInstance));

                AssertExpectedOrder(
                    capture.Events,
                    PlayfieldLifecycleTrace.FlowCapturedAreteRobotSpawn,
                    PlayfieldLifecycleTrace.ExpectedCapturedAreteRobotSpawnOrder);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnRowsLoaded,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnCreated);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnCreated,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotPatrolReplayAssigned);
                AssertStageBefore(
                    capture.Events,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotPatrolReplayAssigned,
                    PlayfieldLifecycleTrace.StageCapturedAreteRobotSimpleCharFullUpdateBroadcast);
                Assert.IsTrue(
                    HasDetail(
                        capture.Events,
                        PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnRowsLoaded,
                        "count=7 monsterData=297023"));
                Assert.IsTrue(
                    HasDetail(
                        capture.Events,
                        PlayfieldLifecycleTrace.StageCapturedAreteRobotSpawnCreated,
                        spawnCreatedDetail));
                Assert.IsTrue(
                    spawnCreatedDetail.IndexOf(
                        "sourceInstance=79225E7C monsterData=297023 hp=12 level=1 runSpeed=6",
                        StringComparison.Ordinal) >= 0);
                Assert.IsTrue(
                    HasDetail(
                        capture.Events,
                        PlayfieldLifecycleTrace.StageCapturedAreteRobotPatrolReplayAssigned,
                        "sourceInstance=79225E7C segments=40"));
            }
        }

        [TestMethod]
        public void PlayfieldContentModulesDoNotOwnRuntimeSystems()
        {
            string contentDirectory = Path.Combine(
                FindRepositoryRoot(),
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Content");
            string[] sourceFiles = Directory.GetFiles(contentDirectory, "*.cs", SearchOption.AllDirectories);

            Assert.IsTrue(sourceFiles.Length >= 4, "Expected current Playfield content-module files to be scanned.");

            foreach (string sourceFile in sourceFiles)
            {
                string text = File.ReadAllText(sourceFile);
                Assert.IsTrue(
                    text.Contains("namespace ZoneEngine.Core.Playfields.Content"),
                    "Content guardrail only applies to the content namespace: " + sourceFile);

                for (int i = 0; i < ForbiddenContentModuleReferences.Length; i++)
                {
                    ForbiddenReference forbidden = ForbiddenContentModuleReferences[i];
                    Assert.IsFalse(
                        text.IndexOf(forbidden.Pattern, StringComparison.Ordinal) >= 0,
                        string.Format(
                            "Playfield content modules must define content only; forbidden {0} reference '{1}' found in {2}.",
                            forbidden.Category,
                            forbidden.Pattern,
                            sourceFile));
                }
            }
        }

        [TestMethod]
        public void PrivateCityContentModuleSkeletonIsRegisteredWithoutRuntimeOwnership()
        {
            string repositoryRoot = FindRepositoryRoot();
            string modulePath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\PrivateCityContentModule.cs");
            string runtimeSystemsPath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs");
            string projectPath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj");

            string moduleText = File.ReadAllText(modulePath);
            string runtimeSystemsText = File.ReadAllText(runtimeSystemsPath);
            string projectText = File.ReadAllText(projectPath);

            Assert.IsTrue(moduleText.Contains("public sealed class PrivateCityContentModule : IPlayfieldContentModule"));
            Assert.IsTrue(moduleText.Contains("public bool Supports(Identity playfieldIdentity)"));
            Assert.IsTrue(moduleText.Contains("public void Register(PlayfieldContentRegistration registration)"));
            Assert.IsTrue(
                runtimeSystemsText.Contains("new PrivateCityContentModule()"),
                "PlayfieldRuntimeSystems content coordinator must register the private-city content module skeleton.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\Content\PrivateCityContentModule.cs"),
                "ZoneEngine project must compile the private-city content module skeleton.");
        }

        [TestMethod]
        public void MontroyalContentModuleSkeletonIsRegisteredWithoutRuntimeOwnership()
        {
            string repositoryRoot = FindRepositoryRoot();
            string modulePath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\MontroyalContentModule.cs");
            string runtimeSystemsPath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs");
            string projectPath = Path.Combine(
                repositoryRoot,
                @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj");

            string moduleText = File.ReadAllText(modulePath);
            string runtimeSystemsText = File.ReadAllText(runtimeSystemsPath);
            string projectText = File.ReadAllText(projectPath);

            Assert.IsTrue(moduleText.Contains("public sealed class MontroyalContentModule : IPlayfieldContentModule"));
            Assert.IsTrue(moduleText.Contains("private const int MontroyalPlayfieldInstance = 655"));
            Assert.IsTrue(moduleText.Contains("public bool Supports(Identity playfieldIdentity)"));
            Assert.IsTrue(moduleText.Contains("public void Register(PlayfieldContentRegistration registration)"));
            Assert.IsTrue(
                runtimeSystemsText.Contains("new MontroyalContentModule()"),
                "PlayfieldRuntimeSystems content coordinator must register the Montroyal content module skeleton.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\Content\MontroyalContentModule.cs"),
                "ZoneEngine project must compile the Montroyal content module skeleton.");
        }

        [TestMethod]
        public void KnownPlayfieldContentModulesAreRegisteredExactlyOnceThroughCoordinatorPath()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string coordinatorText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\PlayfieldContentCoordinator.cs"));
            string registrationText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\PlayfieldContentRegistration.cs"));

            string[] expectedModules =
                {
                    "AreteContentModule",
                    "MontroyalContentModule",
                    "PrivateCityContentModule"
                };

            Assert.IsTrue(
                playfieldText.Contains("private readonly PlayfieldRuntimeSystems runtimeSystems"),
                "Playfield must own runtime systems through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.RegisterContent(playfieldIdentity);"),
                "Playfield must enter content registration through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("private readonly PlayfieldContentCoordinator content"),
                "PlayfieldRuntimeSystems must own PlayfieldContentCoordinator.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("this.content.RegisterContent(this.playfield, playfieldIdentity);"),
                "PlayfieldRuntimeSystems must delegate content registration through PlayfieldContentCoordinator.");

            int coordinatorIndex = runtimeSystemsText.IndexOf("new PlayfieldContentCoordinator(", StringComparison.Ordinal);
            Assert.IsTrue(coordinatorIndex >= 0, "Missing PlayfieldContentCoordinator construction.");

            int previousIndex = coordinatorIndex;
            for (int i = 0; i < expectedModules.Length; i++)
            {
                string constructor = "new " + expectedModules[i] + "()";
                Assert.AreEqual(
                    1,
                    CountOccurrences(runtimeSystemsText, constructor),
                    expectedModules[i] + " must be registered exactly once.");
                Assert.AreEqual(
                    0,
                    CountOccurrences(playfieldText, constructor),
                    "Playfield must not directly construct " + expectedModules[i] + ".");

                int moduleIndex = runtimeSystemsText.IndexOf(constructor, coordinatorIndex, StringComparison.Ordinal);
                Assert.IsTrue(moduleIndex > previousIndex, expectedModules[i] + " is not in expected coordinator order.");
                previousIndex = moduleIndex;
            }

            Assert.IsTrue(
                coordinatorText.Contains("new PlayfieldContentRegistration(playfield, playfieldIdentity)"),
                "PlayfieldContentCoordinator must create PlayfieldContentRegistration.");
            Assert.IsTrue(
                coordinatorText.Contains("module.Register(registration)"),
                "PlayfieldContentCoordinator must dispatch registrations through PlayfieldContentRegistration.");
            Assert.IsTrue(
                registrationText.Contains("public sealed class PlayfieldContentRegistration"),
                "PlayfieldContentRegistration must remain the registration boundary.");
        }

        [TestMethod]
        public void PlayfieldRuntimeSystemsFacadeOwnsSeparatedRuntimeCoordinators()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string projectText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj"));

            string[] runtimeCoordinatorConstructors =
                {
                    "new NpcCorpseLifecycleCoordinator(playfield)",
                    "new NpcCombatTickCoordinator(playfield)",
                    "new PrivateCityReadyInitCoordinator("
                };
            for (int i = 0; i < runtimeCoordinatorConstructors.Length; i++)
            {
                Assert.AreEqual(
                    1,
                    CountOccurrences(runtimeSystemsText, runtimeCoordinatorConstructors[i]),
                    "PlayfieldRuntimeSystems must own " + runtimeCoordinatorConstructors[i] + ".");
                Assert.AreEqual(
                    0,
                    CountOccurrences(playfieldText, runtimeCoordinatorConstructors[i]),
                    "Playfield must not directly construct " + runtimeCoordinatorConstructors[i] + ".");
            }

            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.SendPrivateCityPlayfieldReadyBlock(client, character);"),
                "Playfield must delegate private-city ready block sending through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.ProcessNpcCombatTick(attacker);"),
                "Playfield must delegate NPC combat ticks through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.BeginNpcDeath(attacker, target);"),
                "Playfield must delegate NPC corpse lifecycle start through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                playfieldText.Contains("this.runtimeSystems.ProcessDeadNpc(dynel)"),
                "Playfield heartbeat must delegate dead NPC processing through PlayfieldRuntimeSystems.");
            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldRuntimeSystems.cs"),
                "ZoneEngine project must compile PlayfieldRuntimeSystems.");
        }

        [TestMethod]
        public void PlayfieldContentDataProviderOwnsStaticContentDataResolution()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldContentDataProvider.cs"));
            string projectText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj"));

            Assert.IsTrue(
                providerText.Contains("internal sealed class PlayfieldContentDataProvider"),
                "PlayfieldContentDataProvider must be the named content data boundary.");
            Assert.IsTrue(
                providerText.Contains("internal List<StatelData> ResolveStatels(Identity playfieldIdentity)"),
                "Provider must own statel resolution.");
            Assert.IsTrue(
                providerText.Contains(
                    "internal bool TryResolveVendorStatels("),
                "Provider must own vendor statel resolution.");
            Assert.IsTrue(
                providerText.Contains("internal StatelData[] ResolveCollisionStatels(IEnumerable<StatelData> statels)"),
                "Provider must own collision-capable statel filtering.");
            Assert.IsTrue(
                providerText.Contains(
                    "internal IEnumerable<PlayfieldStaticDynelDefinition> ResolveStaticDynels(Identity playfieldIdentity)"),
                "Provider must own static dynel definition resolution.");
            Assert.IsTrue(
                providerText.Contains("PlayfieldLoader.PFData.TryGetValue"),
                "Provider must own PlayfieldLoader statel data access.");
            Assert.IsTrue(
                providerText.Contains("StaticDynelDao.Instance.GetWhere"),
                "Provider must own static dynel DB row access.");
            Assert.IsTrue(
                providerText.Contains("MessagePackZip.DeserializeData"),
                "Provider must own static dynel stat payload deserialization.");
            Assert.IsTrue(
                providerText.Contains("IdentityType.VendingMachine"),
                "Provider must preserve the existing vendor statel filter.");
            Assert.IsTrue(
                providerText.Contains("x.EventType == EventType.OnCollide")
                && providerText.Contains("x.EventType == EventType.OnEnter")
                && providerText.Contains("x.EventType == EventType.OnTargetInVicinity"),
                "Provider must preserve the existing collision statel event filter.");
            Assert.IsTrue(
                providerText.Contains("internal sealed class PlayfieldStaticDynelDefinition"),
                "Provider must expose static dynel definitions rather than spawning runtime objects.");

            Assert.IsTrue(
                runtimeSystemsText.Contains("private readonly PlayfieldContentDataProvider contentData"),
                "PlayfieldRuntimeSystems must own PlayfieldContentDataProvider.");
            Assert.IsTrue(
                runtimeSystemsText.Contains(
                    "this.contentData = new PlayfieldContentDataProvider(isPrivateCityPlayfieldCandidate);"),
                "PlayfieldRuntimeSystems must construct the content data provider.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("return this.contentData.ResolveStatels(playfieldIdentity);"),
                "Runtime systems must delegate statel data resolution to the provider.");
            Assert.IsTrue(
                runtimeSystemsText.Contains(
                    "return this.contentData.TryResolveVendorStatels(playfieldIdentity, statels, out vendorStatels);"),
                "Runtime systems must delegate vendor statel data resolution to the provider.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("return this.contentData.ResolveCollisionStatels(statels);"),
                "Runtime systems must delegate collision statel filtering to the provider.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("return this.contentData.ResolveStaticDynels(playfieldIdentity);"),
                "Runtime systems must delegate static dynel data resolution to the provider.");

            string constructor = ExtractMethodBlock(playfieldText, "public Playfield(ZoneServer zoneServer, Identity playfieldIdentity)");
            AssertTextBefore(
                constructor,
                "this.runtimeSystems.ResolveStatels(playfieldIdentity)",
                "this.runtimeSystems.RegisterStatels(this.statels);");
            AssertTextBefore(
                constructor,
                "this.runtimeSystems.RegisterStatels(this.statels);",
                "this.collisionStatels = this.runtimeSystems.ResolveCollisionStatels(this.statels);");
            AssertTextBefore(
                constructor,
                "this.collisionStatels = this.runtimeSystems.ResolveCollisionStatels(this.statels);",
                "this.LoadMobSpawns(playfieldIdentity);");
            AssertTextBefore(
                constructor,
                "this.LoadMobSpawns(playfieldIdentity);",
                "this.runtimeSystems.RegisterContent(playfieldIdentity);");
            AssertTextBefore(
                constructor,
                "this.runtimeSystems.RegisterContent(playfieldIdentity);",
                "this.LoadVendors(playfieldIdentity);");
            AssertTextBefore(
                constructor,
                "this.LoadVendors(playfieldIdentity);",
                "this.LoadStaticDynels(playfieldIdentity);");
            AssertTextBefore(
                constructor,
                "this.LoadStaticDynels(playfieldIdentity);",
                "this.runtimeSystems.RefreshDynelRegistry();");

            string loadVendors = ExtractMethodBlock(playfieldText, "private void LoadVendors(Identity playfieldIdentity)");
            Assert.IsTrue(
                loadVendors.Contains("this.runtimeSystems.TryResolveVendorStatels(playfieldIdentity, this.statels, out vendorStatels)"),
                "Playfield vendor loading must ask runtime systems for vendor statels.");
            Assert.IsFalse(
                loadVendors.Contains("PlayfieldLoader.PFData"),
                "Playfield vendor loading must not own PlayfieldLoader access.");
            Assert.IsFalse(
                loadVendors.Contains("IdentityType.VendingMachine"),
                "Playfield vendor loading must not own vendor statel filtering.");

            string checkStatelCollision = ExtractMethodBlock(playfieldText, "private void CheckStatelCollision(ICharacter dynel)");
            string primeStatelCollisionContacts =
                ExtractMethodBlock(playfieldText, "private void PrimeStatelCollisionContacts(ICharacter dynel)");
            Assert.IsTrue(
                playfieldText.Contains("private readonly StatelData[] collisionStatels"),
                "Playfield must keep a provider-filtered collision statel view.");
            Assert.IsTrue(
                checkStatelCollision.Contains("foreach (StatelData sd in this.collisionStatels)"),
                "CheckStatelCollision must use provider-filtered collision statels.");
            Assert.IsTrue(
                primeStatelCollisionContacts.Contains("foreach (StatelData sd in this.collisionStatels)"),
                "PrimeStatelCollisionContacts must use provider-filtered collision statels.");
            Assert.IsFalse(
                primeStatelCollisionContacts.Contains("sd.Events.Any"),
                "PrimeStatelCollisionContacts must not own collision-capable statel selection.");

            string loadStaticDynels =
                ExtractMethodBlock(playfieldText, "private void LoadStaticDynels(Identity playfieldIdentity)");
            Assert.IsTrue(
                loadStaticDynels.Contains("this.runtimeSystems.ResolveStaticDynels(playfieldIdentity)"),
                "Playfield static dynel loading must ask runtime systems for static dynel definitions.");
            Assert.IsTrue(
                loadStaticDynels.Contains("new StaticDynel(this.Identity, staticDynel.Identity, staticDynel.Template)"),
                "Playfield must remain the runtime static dynel construction owner in this slice.");
            Assert.IsFalse(
                loadStaticDynels.Contains("StaticDynelDao.Instance.GetWhere"),
                "Playfield static dynel loading must not own DB row access.");
            Assert.IsFalse(
                loadStaticDynels.Contains("MessagePackZip.DeserializeData"),
                "Playfield static dynel loading must not own static dynel stat deserialization.");

            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldContentDataProvider.cs"),
                "ZoneEngine project must compile PlayfieldContentDataProvider.");
        }

        [TestMethod]
        public void PlayfieldContentDataProviderDoesNotOwnRuntimeSystemsOrPacketFlows()
        {
            string repositoryRoot = FindRepositoryRoot();
            string providerText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldContentDataProvider.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));

            Assert.IsTrue(
                providerText.Contains("StaticDynelDao.Instance.GetWhere"),
                "Provider must own static dynel definition data selection.");
            Assert.IsTrue(
                providerText.Contains("private StatelData[] ResolveVendorStatels(IEnumerable<StatelData> statels)"),
                "Provider must own vendor statel filtering.");
            Assert.IsTrue(
                providerText.Contains("internal StatelData[] ResolveCollisionStatels(IEnumerable<StatelData> statels)"),
                "Provider must own collision-capable statel filtering.");

            string[] forbiddenRuntimeOwnershipPatterns =
                {
                    "new StaticDynel",
                    "VendorHandler.SpawnVendorsForPlayfield",
                    "SendCompressed",
                    "N3Messages",
                    "SystemMessages",
                    "MessageHandler",
                    "GenericCmd",
                    "NpcCombat",
                    "CombatDamageRules",
                    "NpcCorpse",
                    "Inventory",
                    "ContainerAddItem",
                    "ClientMoveItem",
                    "OrgClient",
                    "OrgServer",
                    "PrivateCityReadyInitCoordinator",
                    "AOSharpLiveCapture",
                    "tools-temp"
                };
            for (int i = 0; i < forbiddenRuntimeOwnershipPatterns.Length; i++)
            {
                Assert.IsFalse(
                    providerText.Contains(forbiddenRuntimeOwnershipPatterns[i]),
                    "PlayfieldContentDataProvider must not own runtime or packet behavior: "
                    + forbiddenRuntimeOwnershipPatterns[i]);
            }

            string loadVendors = ExtractMethodBlock(playfieldText, "private void LoadVendors(Identity playfieldIdentity)");
            string loadStaticDynels =
                ExtractMethodBlock(playfieldText, "private void LoadStaticDynels(Identity playfieldIdentity)");
            string checkStatelCollision = ExtractMethodBlock(playfieldText, "private void CheckStatelCollision(ICharacter dynel)");

            Assert.IsTrue(
                loadVendors.Contains("VendorHandler.SpawnVendorsForPlayfield(this, vendorStatels);"),
                "Playfield must remain the vendor runtime spawning owner.");
            Assert.IsTrue(
                loadStaticDynels.Contains("new StaticDynel(this.Identity, staticDynel.Identity, staticDynel.Template)"),
                "Playfield must remain the StaticDynel runtime construction owner.");
            Assert.IsTrue(
                checkStatelCollision.Contains("IsInStatelCollisionRange(sd, dynel)")
                && checkStatelCollision.Contains("ev.Perform(dynel, sd);"),
                "Playfield must remain the statel collision runtime check/event owner.");
        }

        [TestMethod]
        public void ZoneClientSessionLifecycleCoordinatorModelsSessionPhasesWithoutPacketOwnership()
        {
            var lifecycle = new ZoneClientSessionLifecycleCoordinator();

            lifecycle.BeginCharacterLoading();
            lifecycle.EnterPlayfieldLoadingForCharacterLoadOrZoningExit();
            lifecycle.EnterReadyBlockForSessionInit();
            lifecycle.EnterFullCharacterBoundaryForSessionInit();
            lifecycle.EnterCharInPlayForVisibilityEntry();
            lifecycle.CompleteInPlayForSessionInit();
            lifecycle.EnterZoningForPlayfieldTransfer();
            lifecycle.EnterDisconnectingForSessionDispose();
            lifecycle.EnterDisconnectingForSessionDispose();

            var expected =
                new[]
                {
                    ZoneClientSessionPhase.Connected,
                    ZoneClientSessionPhase.CharacterLoading,
                    ZoneClientSessionPhase.PlayfieldLoading,
                    ZoneClientSessionPhase.ReadyBlock,
                    ZoneClientSessionPhase.FullCharacterBoundary,
                    ZoneClientSessionPhase.CharInPlay,
                    ZoneClientSessionPhase.InPlay,
                    ZoneClientSessionPhase.Zoning,
                    ZoneClientSessionPhase.Disconnecting
                };

            Assert.AreEqual(expected[expected.Length - 1], lifecycle.Phase);
            Assert.AreEqual(expected.Length, lifecycle.PhaseHistory.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], lifecycle.PhaseHistory[i]);
            }
            Assert.AreEqual("ZoneClientSession.Disconnecting", lifecycle.PhaseTraceName);

            string repositoryRoot = FindRepositoryRoot();
            string coordinatorText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\ZoneClientSessionLifecycleCoordinator.cs"));
            Assert.IsTrue(
                coordinatorText.Contains("public bool CanTransitionTo(ZoneClientSessionPhase phase)"),
                "Coordinator must own lifecycle transition validation.");
            Assert.IsTrue(
                coordinatorText.Contains("Invalid ZoneClient session transition"),
                "Coordinator must reject invalid lifecycle transitions.");
            Assert.IsTrue(
                coordinatorText.Contains("case ZoneClientSessionPhase.Zoning:"),
                "Coordinator must explicitly model zoning return transitions.");

            string[] forbiddenRuntimeOwnershipPatterns =
                {
                    "SendCompressed",
                    "MessageHandler",
                    "GenericCmd",
                    "NpcCombat",
                    "Inventory",
                    "OrgClient",
                    "CityController",
                    "GuestKey",
                    "MessagePackZip",
                    "Dao.Instance",
                    "AOSharpLiveCapture",
                    "tools-temp"
                };
            for (int i = 0; i < forbiddenRuntimeOwnershipPatterns.Length; i++)
            {
                Assert.IsFalse(
                    coordinatorText.Contains(forbiddenRuntimeOwnershipPatterns[i]),
                    "ZoneClient session lifecycle coordinator must not own packet, gameplay, DB, or capture behavior: "
                    + forbiddenRuntimeOwnershipPatterns[i]);
            }
        }

        [TestMethod]
        public void ZoneClientSessionLifecycleCoordinatorRejectsInvalidTransitions()
        {
            var lifecycle = new ZoneClientSessionLifecycleCoordinator();

            Assert.IsFalse(lifecycle.CanTransitionTo(ZoneClientSessionPhase.ReadyBlock));
            AssertInvalidTransition(
                lifecycle.EnterReadyBlockForSessionInit,
                "ZoneClientSession.Connected to ZoneClientSession.ReadyBlock");

            lifecycle.BeginCharacterLoading();
            Assert.IsFalse(lifecycle.CanTransitionTo(ZoneClientSessionPhase.FullCharacterBoundary));
            AssertInvalidTransition(
                lifecycle.EnterFullCharacterBoundaryForSessionInit,
                "ZoneClientSession.CharacterLoading to ZoneClientSession.FullCharacterBoundary");

            lifecycle.EnterPlayfieldLoadingForCharacterLoadOrZoningExit();
            lifecycle.EnterReadyBlockForSessionInit();
            Assert.IsFalse(lifecycle.CanTransitionTo(ZoneClientSessionPhase.InPlay));
            AssertInvalidTransition(
                lifecycle.CompleteInPlayForSessionInit,
                "ZoneClientSession.ReadyBlock to ZoneClientSession.InPlay");
        }

        [TestMethod]
        public void ZoneClientSessionLifecycleCoordinatorAllowsZoningReturnOptionsAndDisconnects()
        {
            var zoningToPlayfieldLoading = CreateInPlayLifecycle();
            zoningToPlayfieldLoading.EnterZoningForPlayfieldTransfer();
            Assert.IsTrue(zoningToPlayfieldLoading.CanTransitionTo(ZoneClientSessionPhase.PlayfieldLoading));
            zoningToPlayfieldLoading.EnterPlayfieldLoadingForCharacterLoadOrZoningExit();
            Assert.AreEqual(ZoneClientSessionPhase.PlayfieldLoading, zoningToPlayfieldLoading.Phase);

            var zoningToReadyBlock = CreateInPlayLifecycle();
            zoningToReadyBlock.EnterZoningForPlayfieldTransfer();
            Assert.IsTrue(zoningToReadyBlock.CanTransitionTo(ZoneClientSessionPhase.ReadyBlock));
            zoningToReadyBlock.EnterReadyBlockForSessionInit();
            Assert.AreEqual(ZoneClientSessionPhase.ReadyBlock, zoningToReadyBlock.Phase);

            var disconnectingFromConnected = new ZoneClientSessionLifecycleCoordinator();
            disconnectingFromConnected.EnterDisconnectingForSessionDispose();
            Assert.AreEqual(ZoneClientSessionPhase.Disconnecting, disconnectingFromConnected.Phase);

            var disconnectingFromZoning = CreateInPlayLifecycle();
            disconnectingFromZoning.EnterZoningForPlayfieldTransfer();
            disconnectingFromZoning.EnterDisconnectingForSessionDispose();
            Assert.AreEqual(ZoneClientSessionPhase.Disconnecting, disconnectingFromZoning.Phase);
        }

        [TestMethod]
        public void ZoneClientSessionLifecycleBoundaryIsWiredAroundExistingLoginReadyAndZoningFlow()
        {
            string repositoryRoot = FindRepositoryRoot();
            string zoneClientText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\ZoneClient.cs"));
            string zoneLoginText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\ZoneLoginMessageHandler.cs"));
            string clientConnectedText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string projectText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj"));
            string teleportMethod = ExtractMethodBlock(
                playfieldText,
                "public void Teleport(Dynel dynel, Coordinate destination, IQuaternion heading, Identity playfield)");
            string disposeMethod = ExtractMethodBlock(zoneClientText, "protected override void Dispose(bool disposing)");

            Assert.IsTrue(
                zoneClientText.Contains("private readonly ZoneClientSessionLifecycleCoordinator sessionLifecycle"),
                "ZoneClient must own the session lifecycle coordinator.");
            Assert.IsTrue(
                zoneClientText.Contains("public ZoneClientSessionLifecycleCoordinator SessionLifecycle"),
                "ZoneClient must expose the session lifecycle boundary to existing handlers.");
            Assert.IsTrue(
                projectText.Contains(@"Core\ZoneClientSessionLifecycleCoordinator.cs"),
                "ZoneEngine project must compile the session lifecycle coordinator.");

            AssertTextBefore(
                zoneLoginText,
                "zc.SessionLifecycle.BeginCharacterLoading();",
                "zc.CreateCharacter(message.CharacterId);");
            AssertTextBefore(
                zoneClientText,
                "this.SessionLifecycle.EnterPlayfieldLoadingForCharacterLoadOrZoningExit();",
                "this.server.PlayfieldById(");
            AssertTextBefore(
                clientConnectedText,
                "client.SessionLifecycle.EnterReadyBlockForSessionInit();",
                "PlayfieldAnarchyFMessageHandler.Default.Send");
            AssertTextBefore(
                clientConnectedText,
                "client.SessionLifecycle.EnterFullCharacterBoundaryForSessionInit();",
                "FullCharacterMessageHandler.Default.Send(client.Controller.Character);");
            AssertTextBefore(
                clientConnectedText,
                "client.SessionLifecycle.EnterCharInPlayForVisibilityEntry();",
                "currentPlayfield.AnnouncePlayerVisibility(client.Controller.Character);");
            AssertTextBefore(
                clientConnectedText,
                "client.SessionLifecycle.CompleteInPlayForSessionInit();",
                "client.Controller.Character.DoNotDoTimers = false;");
            AssertTextBefore(
                teleportMethod,
                "lifecycleClient.SessionLifecycle.EnterZoningForPlayfieldTransfer();",
                "TeleportMessageHandler.Default.Send(");
            AssertTextBefore(
                disposeMethod,
                "this.sessionLifecycle.EnterDisconnectingForSessionDispose();",
                "this.stopDispatcher = true;");
        }

        [TestMethod]
        public void ZoneClientSessionLifecycleCheckpointKeepsPhaseOwnershipOutOfPacketCode()
        {
            var lifecycle = new ZoneClientSessionLifecycleCoordinator();
            lifecycle.BeginCharacterLoading();
            lifecycle.BeginCharacterLoading();

            Assert.AreEqual(ZoneClientSessionPhase.CharacterLoading, lifecycle.Phase);
            Assert.AreEqual(2, lifecycle.PhaseHistory.Count, "Duplicate same-phase transitions must remain no-op.");

            string repositoryRoot = FindRepositoryRoot();
            string coordinatorText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\ZoneClientSessionLifecycleCoordinator.cs"));
            string zoneLoginText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\ZoneLoginMessageHandler.cs"));
            string clientConnectedText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));

            Assert.IsTrue(
                coordinatorText.Contains("private static bool IsAllowedTransition(ZoneClientSessionPhase from, ZoneClientSessionPhase to)"),
                "ZoneClientSessionLifecycleCoordinator must own allowed transition rules.");
            Assert.IsTrue(
                coordinatorText.Contains("if (from == to)") && coordinatorText.Contains("return true;"),
                "ZoneClientSessionLifecycleCoordinator must keep duplicate same-phase transitions legal.");
            Assert.IsTrue(
                coordinatorText.Contains("throw new InvalidOperationException("),
                "ZoneClientSessionLifecycleCoordinator must guard invalid transitions.");
            Assert.IsTrue(
                coordinatorText.Contains("public void EnterReadyBlockForSessionInit()")
                && coordinatorText.Contains("public void EnterFullCharacterBoundaryForSessionInit()")
                && coordinatorText.Contains("public void EnterCharInPlayForVisibilityEntry()")
                && coordinatorText.Contains("public void CompleteInPlayForSessionInit()"),
                "ZoneClientSessionLifecycleCoordinator must own named ready/full-character/CharInPlay sequencing surfaces.");
            Assert.IsTrue(
                coordinatorText.Contains("public void EnterPlayfieldLoadingForCharacterLoadOrZoningExit()")
                && coordinatorText.Contains("public void EnterZoningForPlayfieldTransfer()")
                && coordinatorText.Contains("public void EnterDisconnectingForSessionDispose()"),
                "ZoneClientSessionLifecycleCoordinator must own named playfield-loading/zoning/disconnect sequencing surfaces.");

            string[] packetAndRuntimePatterns =
                {
                    "SendCompressed",
                    "PlayfieldAnarchyFMessageHandler",
                    "FullCharacterMessageHandler",
                    "CharInPlayMessage",
                    "TeleportMessageHandler",
                    "PrivateCityReadyInitCoordinator",
                    "NpcCombat",
                    "NpcPatrol",
                    "Movement",
                    "GenericCmd",
                    "Inventory",
                    "OrgClient",
                    "OrgServer",
                    "MessagePackZip",
                    "Dao.Instance",
                    "AOSharpLiveCapture",
                    "tools-temp"
                };
            for (int i = 0; i < packetAndRuntimePatterns.Length; i++)
            {
                Assert.IsFalse(
                    coordinatorText.Contains(packetAndRuntimePatterns[i]),
                    "ZoneClient session lifecycle coordinator must remain phase-only before packet sequencing moves: "
                    + packetAndRuntimePatterns[i]);
            }

            Assert.IsTrue(
                clientConnectedText.Contains("FullCharacterMessageHandler.Default.Send(client.Controller.Character);"),
                "FullCharacter packet emission must still remain outside the lifecycle coordinator.");
            Assert.IsTrue(
                clientConnectedText.Contains("currentPlayfield.AnnouncePlayerVisibility(client.Controller.Character);"),
                "CharInPlay/visibility packet emission must still remain outside the lifecycle coordinator.");
            Assert.IsTrue(
                playfieldText.Contains("TeleportMessageHandler.Default.Send("),
                "Teleport packet emission must still remain outside the lifecycle coordinator.");

            string markerSurfaces = zoneLoginText + clientConnectedText + playfieldText;
            Assert.IsFalse(
                markerSurfaces.Contains("ZoneClientSessionPhase."),
                "Packet/runtime surfaces must not own lifecycle enum transition rules directly.");
            Assert.IsFalse(
                markerSurfaces.Contains("CanTransitionTo("),
                "Packet/runtime surfaces must call named coordinator transition methods instead of owning transition validity.");
            Assert.IsFalse(
                markerSurfaces.Contains("BeginReadyBlock()")
                || markerSurfaces.Contains("BeginFullCharacterBoundary()")
                || markerSurfaces.Contains("MarkCharInPlay()")
                || markerSurfaces.Contains("MarkInPlay()"),
                "Packet/runtime surfaces must not use loose ready/full-character/CharInPlay lifecycle marker names.");
            Assert.IsFalse(
                markerSurfaces.Contains("BeginPlayfieldLoading()")
                || markerSurfaces.Contains("BeginZoning()")
                || markerSurfaces.Contains("BeginDisconnecting()"),
                "Packet/runtime surfaces must not use loose playfield-loading/zoning/disconnect lifecycle marker names.");
        }

        [TestMethod]
        public void ZoneClientSessionLifecycleFinalPhaseOwnershipGuardrailKeepsRuntimeMechanicsOut()
        {
            string repositoryRoot = FindRepositoryRoot();
            string coordinatorText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\ZoneClientSessionLifecycleCoordinator.cs"));
            string zoneClientText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\ZoneClient.cs"));
            string clientConnectedText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs"));
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));

            string[] namedPhaseMethods =
                {
                    "EnterPlayfieldLoadingForCharacterLoadOrZoningExit",
                    "EnterReadyBlockForSessionInit",
                    "EnterFullCharacterBoundaryForSessionInit",
                    "EnterCharInPlayForVisibilityEntry",
                    "CompleteInPlayForSessionInit",
                    "EnterZoningForPlayfieldTransfer",
                    "EnterDisconnectingForSessionDispose"
                };
            for (int i = 0; i < namedPhaseMethods.Length; i++)
            {
                Assert.IsTrue(
                    coordinatorText.Contains("public void " + namedPhaseMethods[i] + "()"),
                    "Coordinator must expose named lifecycle phase ownership method " + namedPhaseMethods[i] + ".");
            }

            string runtimeSurfaces = zoneClientText + clientConnectedText + playfieldText;
            Assert.IsFalse(
                runtimeSurfaces.Contains("TransitionTo("),
                "Runtime packet/session surfaces must not call the raw phase transition helper.");
            Assert.IsFalse(
                runtimeSurfaces.Contains("ZoneClientSessionPhase."),
                "Runtime packet/session surfaces must not own direct lifecycle phase enum transitions.");

            Assert.IsTrue(
                zoneClientText.Contains("this.SessionLifecycle.EnterPlayfieldLoadingForCharacterLoadOrZoningExit();")
                && zoneClientText.Contains("this.sessionLifecycle.EnterDisconnectingForSessionDispose();"),
                "ZoneClient must use named coordinator methods for playfield-loading/zoning-exit and disconnect phases.");
            Assert.IsTrue(
                clientConnectedText.Contains("client.SessionLifecycle.EnterReadyBlockForSessionInit();")
                && clientConnectedText.Contains("client.SessionLifecycle.EnterFullCharacterBoundaryForSessionInit();")
                && clientConnectedText.Contains("client.SessionLifecycle.EnterCharInPlayForVisibilityEntry();")
                && clientConnectedText.Contains("client.SessionLifecycle.CompleteInPlayForSessionInit();"),
                "ClientConnected must use named coordinator methods for ready/full-character/CharInPlay/InPlay phases.");
            Assert.IsTrue(
                playfieldText.Contains("lifecycleClient.SessionLifecycle.EnterZoningForPlayfieldTransfer();"),
                "Playfield teleport must use the named coordinator method for zoning entry.");

            string[] forbiddenCoordinatorMechanics =
                {
                    "SendCompressed",
                    "TeleportMessageHandler",
                    "ZoneRedirectionMessage",
                    "PrivateCityReadyInitCoordinator",
                    "SendPrivateCity",
                    "SimpleCharFullUpdate",
                    "CharInPlayMessage",
                    "AnnouncePlayerVisibility",
                    "SendSCFUsToClient",
                    "stopDispatcher",
                    "zStream",
                    "netStream"
                };
            for (int i = 0; i < forbiddenCoordinatorMechanics.Length; i++)
            {
                Assert.IsFalse(
                    coordinatorText.Contains(forbiddenCoordinatorMechanics[i]),
                    "Coordinator must not own packet, teleport, visibility, private-city, or disposal mechanics: "
                    + forbiddenCoordinatorMechanics[i]);
            }

            Assert.IsTrue(
                playfieldText.Contains("TeleportMessageHandler.Default.Send(")
                && playfieldText.Contains("new ZoneRedirectionMessage")
                && playfieldText.Contains("client.SendCompressed(redirect);"),
                "Teleport/redirection packet mechanics must remain in Playfield.");
            Assert.IsTrue(
                playfieldText.Contains("SendPrivateCityPreFullCharacterReadyBlock")
                && playfieldText.Contains("SendPrivateCityPlayfieldReadyBlock")
                && playfieldText.Contains("this.runtimeSystems.SendPrivateCity"),
                "Private-city ready/init packet construction and delegation must remain outside the lifecycle coordinator.");
            Assert.IsTrue(
                playfieldText.Contains("this.Announce(SimpleCharFullUpdate.ConstructMessage(temp));")
                && playfieldText.Contains("this.Announce(new CharInPlayMessage { Identity = temp.Identity, Unknown = 0x00 });")
                && playfieldText.Contains("public void SendSCFUsToClient(IMSendPlayerSCFUs sendSCFUs)"),
                "SCFU and CharInPlay broadcast mechanics must remain in Playfield.");
            Assert.IsTrue(
                zoneClientText.Contains("this.stopDispatcher = true;")
                && zoneClientText.Contains("this.zStream.Close();")
                && zoneClientText.Contains("this.netStream.Close();"),
                "Engine/client disposal mechanics must remain in ZoneClient.");
        }

        [TestMethod]
        public void PlayfieldDynelRegistryIsOwnedByRuntimeSystemsAndFeedsSafeLookupPaths()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string registryText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldDynelRegistry.cs"));
            string projectText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj"));

            string[] registryApi =
                {
                    "internal void RefreshFromPool()",
                    "internal void Register(IEntity entity)",
                    "internal void Unregister(Identity identity)",
                    "internal void RegisterStatels(IEnumerable<StatelData> playfieldStatels)",
                    "internal IInstancedEntity FindByIdentity(Identity identity)",
                    "internal T FindByIdentity<T>(Identity identity)",
                    "internal ReadOnlyCollection<IDynel> FindDynelsInRange(IDynel dynel, float range)",
                    "internal ReadOnlyCollection<ICharacter> FindCharactersInRange(IDynel dynel, float range)",
                    "internal ReadOnlyCollection<ICharacter> Characters()",
                    "internal ReadOnlyCollection<Character> CharacterEntities()",
                    "internal ReadOnlyCollection<ICharacter> Players()",
                    "internal ReadOnlyCollection<ICharacter> Npcs()",
                    "internal ReadOnlyCollection<Vendor> Vendors()",
                    "internal ReadOnlyCollection<StaticDynel> StaticDynels()",
                    "internal ReadOnlyCollection<StatelData> Statels()",
                    "internal ReadOnlyCollection<StatelData> Terminals()",
                    "internal ReadOnlyCollection<StatelData> Doors()"
                };

            Assert.IsTrue(
                registryText.Contains("internal sealed class PlayfieldDynelRegistry"),
                "PlayfieldDynelRegistry must be the named server-side dynel registry boundary.");
            for (int i = 0; i < registryApi.Length; i++)
            {
                Assert.IsTrue(
                    registryText.Contains(registryApi[i]),
                    "Missing PlayfieldDynelRegistry API: " + registryApi[i]);
            }

            Assert.IsTrue(
                runtimeSystemsText.Contains("private readonly PlayfieldDynelRegistry dynelRegistry"),
                "PlayfieldRuntimeSystems must own PlayfieldDynelRegistry.");
            Assert.AreEqual(
                1,
                CountOccurrences(runtimeSystemsText, "new PlayfieldDynelRegistry(playfieldIdentity)"),
                "PlayfieldRuntimeSystems must construct one dynel registry.");
            Assert.AreEqual(
                0,
                CountOccurrences(playfieldText, "new PlayfieldDynelRegistry("),
                "Playfield must not directly construct PlayfieldDynelRegistry.");

            string[] runtimeDelegations =
                {
                    "this.dynelRegistry.RefreshFromPool();",
                    "this.dynelRegistry.Register(entity);",
                    "this.dynelRegistry.Unregister(identity);",
                    "this.dynelRegistry.RegisterStatels(statels);",
                    "return this.dynelRegistry.FindByIdentity(identity);",
                    "return this.dynelRegistry.FindByIdentity<T>(identity);",
                    "return this.dynelRegistry.FindDynelsInRange(dynel, range);",
                    "return this.dynelRegistry.FindCharactersInRange(dynel, range);",
                    "return this.dynelRegistry.Characters();",
                    "return this.dynelRegistry.CharacterEntities();",
                    "return this.dynelRegistry.StaticDynels();"
                };
            for (int i = 0; i < runtimeDelegations.Length; i++)
            {
                Assert.IsTrue(
                    runtimeSystemsText.Contains(runtimeDelegations[i]),
                    "PlayfieldRuntimeSystems must delegate through registry: " + runtimeDelegations[i]);
            }

            string[] playfieldDelegations =
                {
                    "this.runtimeSystems.RegisterStatels(this.statels);",
                    "this.runtimeSystems.RegisterDynel(cmob);",
                    "this.runtimeSystems.RegisterDynel(sdy);",
                    "this.runtimeSystems.RefreshDynelRegistry();",
                    "return this.runtimeSystems.FindByIdentity(identity);",
                    "return this.runtimeSystems.FindByIdentity<T>(identity);",
                    "return this.runtimeSystems.FindDynelsInRange(dynel, range).ToList();",
                    "return this.runtimeSystems.FindCharactersInRange(dynel, range).ToList();",
                    "this.runtimeSystems.CharacterEntities()",
                    "this.runtimeSystems.Characters()",
                    "this.runtimeSystems.StaticDynels()"
                };
            for (int i = 0; i < playfieldDelegations.Length; i++)
            {
                Assert.IsTrue(
                    playfieldText.Contains(playfieldDelegations[i]),
                    "Playfield must route the first safe dynel lookup slice through runtime systems: "
                    + playfieldDelegations[i]);
            }

            Assert.IsTrue(
                projectText.Contains(@"Core\Playfields\PlayfieldDynelRegistry.cs"),
                "ZoneEngine project must compile PlayfieldDynelRegistry.");
        }

        [TestMethod]
        public void PlayfieldVisibilityLookupsUseDynelRegistryBoundary()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));

            Assert.IsTrue(
                runtimeSystemsText.Contains("internal ReadOnlyCollection<ICharacter> Characters()")
                && runtimeSystemsText.Contains("return this.dynelRegistry.Characters();"),
                "PlayfieldRuntimeSystems must expose current-playfield character visibility views.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("internal ReadOnlyCollection<Character> CharacterEntities()")
                && runtimeSystemsText.Contains("return this.dynelRegistry.CharacterEntities();"),
                "PlayfieldRuntimeSystems must expose concrete Character views for existing broadcast paths.");

            string announce = ExtractMethodBlock(playfieldText, "public void Announce(MessageBody messageBody)");
            string announceOthers = ExtractMethodBlock(playfieldText, "public void AnnounceOthers(MessageBody messageBody, Identity dontSend)");
            string sendScfus = ExtractMethodBlock(playfieldText, "public void SendSCFUsToClient(IMSendPlayerSCFUs sendSCFUs)");
            string dynelDropPosition = ExtractMethodBlock(playfieldText, "private Coordinate DynelDropPosition(Identity identity)");
            string findNamed = ExtractMethodBlock(playfieldText, "public INamedEntity FindNamedEntityByIdentity(Identity identity)");

            Assert.IsTrue(
                announce.Contains("this.runtimeSystems.CharacterEntities()"),
                "Announce must use registry-backed character visibility views.");
            Assert.IsTrue(
                announceOthers.Contains("this.runtimeSystems.CharacterEntities()"),
                "AnnounceOthers must use registry-backed character visibility views.");
            Assert.IsTrue(
                sendScfus.Contains("this.runtimeSystems.Characters()"),
                "SendSCFUsToClient must use registry-backed current-playfield character views.");
            Assert.IsTrue(
                dynelDropPosition.Contains("this.runtimeSystems.FindByIdentity<IDynel>(identity)"),
                "Dynel drop lookup must use registry-backed identity lookup.");
            Assert.IsTrue(
                findNamed.Contains("this.runtimeSystems.FindByIdentity<INamedEntity>(identity)"),
                "Named entity lookup must use registry-backed typed identity lookup.");

            string[] visibilityLookupBlocks =
                {
                    announce,
                    announceOthers,
                    sendScfus,
                    dynelDropPosition,
                    findNamed
                };
            for (int i = 0; i < visibilityLookupBlocks.Length; i++)
            {
                Assert.IsFalse(
                    visibilityLookupBlocks[i].Contains("Pool.Instance.GetAll"),
                    "Visibility lookup blocks must not scan Pool directly.");
                Assert.IsFalse(
                    visibilityLookupBlocks[i].Contains("Pool.Instance.GetObject"),
                "Visibility lookup blocks must not use direct Pool identity lookup.");
            }
        }

        [TestMethod]
        public void PlayfieldRemainingSafeCharacterLoopsUseDynelRegistryBoundary()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));

            string heartBeat = ExtractMethodBlock(playfieldText, "private void HeartBeatTimer(object sender)");
            string corpseFullUpdate =
                ExtractMethodBlock(playfieldText, "private void SendCorpseFullUpdate(ICharacter target, Identity corpseIdentity)");
            string stopFightingDeadTarget =
                ExtractMethodBlock(playfieldText, "internal void StopFightingDeadTarget(Identity deadTarget)");

            string[] movedLoopBlocks =
                {
                    heartBeat,
                    corpseFullUpdate,
                    stopFightingDeadTarget
                };
            for (int i = 0; i < movedLoopBlocks.Length; i++)
            {
                Assert.IsTrue(
                    movedLoopBlocks[i].Contains("this.runtimeSystems.Characters()"),
                    "Current-playfield character loop must use registry-backed character view.");
                Assert.IsFalse(
                    movedLoopBlocks[i].Contains("Pool.Instance.GetAll"),
                    "Current-playfield character loop must not scan Pool directly.");
            }
        }

        [TestMethod]
        public void PlayfieldDirectPoolUsageIsLimitedToNamedGlobalAndCrossPlayfieldExceptions()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));

            string disconnectAllClients = ExtractMethodBlock(playfieldText, "public void DisconnectAllClients()");
            string numberOfDynels = ExtractMethodBlock(playfieldText, "public int NumberOfDynels()");
            string numberOfPlayers = ExtractMethodBlock(playfieldText, "public int NumberOfPlayers()");
            string teleport = ExtractMethodBlock(
                playfieldText,
                "public void Teleport(Dynel dynel, Coordinate destination, IQuaternion heading, Identity playfield)");

            string[] intentionalGlobalOrCrossPlayfieldExceptions =
                {
                    "DisconnectAllClients: global CanbeAffected character scan for server shutdown/dispose.",
                    "NumberOfDynels: global CanbeAffected count, not playfield-local registry count.",
                    "NumberOfPlayers: global CanbeAffected Character count, not playfield-local registry count.",
                    "Teleport: cross-playfield Pool.GetObject<Playfield> handoff path."
                };
            Assert.AreEqual(
                4,
                intentionalGlobalOrCrossPlayfieldExceptions.Length,
                "Every direct Playfield Pool exception must be named with ownership scope.");

            Assert.IsTrue(
                disconnectAllClients.Contains(
                    "Pool.Instance.GetAll<Character>((int)IdentityType.CanbeAffected).ToList()"),
                intentionalGlobalOrCrossPlayfieldExceptions[0]);
            Assert.IsTrue(
                numberOfDynels.Contains("Pool.Instance.GetAll((int)IdentityType.CanbeAffected).Count()"),
                intentionalGlobalOrCrossPlayfieldExceptions[1]);
            Assert.IsTrue(
                numberOfPlayers.Contains(
                    "Pool.Instance.GetAll<Character>((int)IdentityType.CanbeAffected).Count()"),
                intentionalGlobalOrCrossPlayfieldExceptions[2]);
            Assert.IsTrue(
                teleport.Contains("Pool.Instance.GetObject<Playfield>("),
                intentionalGlobalOrCrossPlayfieldExceptions[3]);

            Assert.AreEqual(
                3,
                CountOccurrences(playfieldText, "Pool.Instance.GetAll"),
                "Future direct Playfield Pool scans are blocked unless added to this explicit exception list.");
            Assert.AreEqual(
                1,
                CountOccurrences(playfieldText, "Pool.Instance.GetObject"),
                "Future direct Playfield Pool identity lookups are blocked unless added to this explicit exception list.");
        }

        [TestMethod]
        public void AreteCleaningRobotDbSpawnSuppressionKeepsCapturedPathAndLegacyDbBoundary()
        {
            string repositoryRoot = FindRepositoryRoot();
            string playfieldText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs"));
            string runtimeSystemsText = File.ReadAllText(
                Path.Combine(repositoryRoot, @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs"));
            string areteContentText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\AreteContentModule.cs"));
            string montroyalContentText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\MontroyalContentModule.cs"));
            string privateCityContentText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\PrivateCityContentModule.cs"));
            string coordinatorText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content\PlayfieldContentCoordinator.cs"));
            string providerText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedAreteRobotContentProvider.cs"));
            string orchestratorText = File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedAreteRobotSpawnOrchestrator.cs"));

            Assert.IsTrue(
                areteContentText.Contains("new CapturedAreteRobotContentProvider(LogCapturedAreteRobotContent)"),
                "Arete captured robot spawns must keep using CapturedAreteRobotContentProvider.");
            Assert.IsTrue(
                areteContentText.Contains(
                    "new CapturedAreteRobotSpawnOrchestrator(CapturedAreteRobotContent, NpcPatrolReplay)"),
                "Arete captured robot spawns must keep using CapturedAreteRobotSpawnOrchestrator.");
            Assert.IsTrue(
                areteContentText.Contains("registration.RegisterCapturedNpcSpawns(CapturedAreteRobotSpawns.SpawnForPlayfield)"),
                "Arete captured robot spawns must enter through content-module registration.");
            Assert.IsTrue(
                providerText.Contains("public CapturedAreteRobotSpawnDefinition[] GetSpawnDefinitions()"),
                "CapturedAreteRobotContentProvider must expose captured spawn definitions.");
            Assert.IsTrue(
                orchestratorText.Contains("CapturedAreteRobotSpawnDefinition[] spawns = this.capturedRobotContent.GetSpawnDefinitions();"),
                "CapturedAreteRobotSpawnOrchestrator must load captured spawns from the provider.");
            Assert.IsTrue(
                orchestratorText.Contains("foreach (CapturedAreteRobotSpawnDefinition spawn in spawns)"),
                "CapturedAreteRobotSpawnOrchestrator must spawn each captured robot definition.");

            Assert.IsFalse(
                playfieldText.Contains("private static bool IsAreteCleaningRobotTestSpawn"),
                "Arete DB suppression predicate must not remain inline in Playfield.");
            Assert.IsTrue(
                coordinatorText.Contains("module.ShouldSuppressDbMobSpawn(playfieldInstance, mobSpawnId)"),
                "PlayfieldContentCoordinator must dispatch DB spawn suppression through content modules.");
            Assert.IsTrue(
                runtimeSystemsText.Contains("return this.content.ShouldSuppressDbMobSpawn(mob.Playfield, mob.Id);"),
                "PlayfieldRuntimeSystems must ask the content coordinator for DB spawn suppression.");

            int filterIndex = playfieldText.IndexOf("if (this.runtimeSystems.ShouldSuppressDbMobSpawn(mob))", StringComparison.Ordinal);
            Assert.IsTrue(filterIndex >= 0, "Playfield DB mob loading must still call the Arete robot suppression guard.");
            int continueIndex = playfieldText.IndexOf("continue;", filterIndex, StringComparison.Ordinal);
            int loadStatsIndex = playfieldText.IndexOf("MobSpawnStatDao.Instance.GetWhere", filterIndex, StringComparison.Ordinal);
            Assert.IsTrue(
                continueIndex > filterIndex && continueIndex < loadStatsIndex,
                "Suppressed legacy DB rows must be skipped before DB spawn stats are loaded.");

            string suppressionMethod = ExtractMethodBlock(areteContentText, "public bool ShouldSuppressDbMobSpawn");
            string coordinatorMethod = ExtractMethodBlock(coordinatorText, "public bool ShouldSuppressDbMobSpawn");
            int playfieldGateIndex = suppressionMethod.IndexOf(
                "playfieldInstance != PrivateAretePlayfieldInstance",
                StringComparison.Ordinal);
            int idSwitchIndex = suppressionMethod.IndexOf("switch (mob.Id)", StringComparison.Ordinal);
            if (idSwitchIndex < 0)
            {
                idSwitchIndex = suppressionMethod.IndexOf("switch (mobSpawnId)", StringComparison.Ordinal);
            }

            Assert.IsTrue(
                areteContentText.Contains("private const int PrivateAretePlayfieldInstance = 6553"),
                "Suppression must preserve the Arete PF 6553 constant.");
            Assert.IsTrue(playfieldGateIndex >= 0, "Suppression must remain gated to Arete PF 6553.");
            Assert.IsTrue(
                idSwitchIndex > playfieldGateIndex,
                "Suppression must check the Arete PF 6553 gate before matching DB mob row ids.");
            Assert.IsTrue(
                coordinatorMethod.Contains("return true;") && coordinatorMethod.Contains("return false;"),
                "Coordinator must suppress only when a content module owns the DB row.");
            Assert.AreEqual(5, CountOccurrences(suppressionMethod, "case "), "Only the captured legacy DB rows may be suppressed.");

            string[] suppressedDbRows =
                {
                    "2027138231",
                    "2027138245",
                    "2027138246",
                    "2027138249",
                    "2027138259"
                };
            for (int i = 0; i < suppressedDbRows.Length; i++)
            {
                Assert.AreEqual(
                    1,
                    CountOccurrences(suppressionMethod, "case " + suppressedDbRows[i] + ":"),
                    "Legacy Arete DB row " + suppressedDbRows[i] + " must remain suppressed exactly once.");
            }

            Assert.IsFalse(
                montroyalContentText.Contains("case 2027138231:"),
                "Montroyal module must not suppress Arete DB rows.");
            Assert.IsFalse(
                privateCityContentText.Contains("case 2027138231:"),
                "Private-city module must not suppress Arete DB rows.");
            Assert.IsTrue(
                montroyalContentText.Contains("public bool ShouldSuppressDbMobSpawn")
                && montroyalContentText.Contains("return false;"),
                "Montroyal module must leave DB spawns unaffected.");
            Assert.IsTrue(
                privateCityContentText.Contains("public bool ShouldSuppressDbMobSpawn")
                && privateCityContentText.Contains("return false;"),
                "Private-city module must leave DB spawns unaffected.");
            Assert.IsTrue(
                suppressionMethod.Contains("default:") && suppressionMethod.Contains("return false;"),
                "Non-matching DB spawns, including non-Arete DB spawns, must remain unaffected.");
        }

        private static void AssertExpectedOrder(
            IList<PlayfieldLifecycleEvent> events,
            string flow,
            string[] expectedStages)
        {
            string failure;
            Assert.IsTrue(
                PlayfieldLifecycleTrace.ContainsExpectedOrder(events, flow, expectedStages, out failure),
                failure);
        }

        private static void AssertStageBefore(
            IList<PlayfieldLifecycleEvent> events,
            string firstStage,
            string secondStage)
        {
            int first = IndexOfStage(events, firstStage);
            int second = IndexOfStage(events, secondStage);
            Assert.IsTrue(first >= 0, "Missing lifecycle stage " + firstStage + ".");
            Assert.IsTrue(second >= 0, "Missing lifecycle stage " + secondStage + ".");
            Assert.IsTrue(first < second, firstStage + " must occur before " + secondStage + ".");
        }

        private static ZoneClientSessionLifecycleCoordinator CreateInPlayLifecycle()
        {
            var lifecycle = new ZoneClientSessionLifecycleCoordinator();
            lifecycle.BeginCharacterLoading();
            lifecycle.EnterPlayfieldLoadingForCharacterLoadOrZoningExit();
            lifecycle.EnterReadyBlockForSessionInit();
            lifecycle.EnterFullCharacterBoundaryForSessionInit();
            lifecycle.EnterCharInPlayForVisibilityEntry();
            lifecycle.CompleteInPlayForSessionInit();
            return lifecycle;
        }

        private static void AssertInvalidTransition(Action transition, string expectedMessage)
        {
            try
            {
                transition();
            }
            catch (InvalidOperationException exception)
            {
                Assert.IsTrue(
                    exception.Message.Contains(expectedMessage),
                    "Invalid transition message must identify the rejected transition.");
                return;
            }

            Assert.Fail("Expected invalid lifecycle transition to be rejected.");
        }

        private static void AssertTextBefore(string text, string firstText, string secondText)
        {
            int first = text.IndexOf(firstText, StringComparison.Ordinal);
            int second = text.IndexOf(secondText, StringComparison.Ordinal);
            Assert.IsTrue(first >= 0, "Missing text " + firstText + ".");
            Assert.IsTrue(second >= 0, "Missing text " + secondText + ".");
            Assert.IsTrue(first < second, firstText + " must occur before " + secondText + ".");
        }

        private static bool HasDetail(IList<PlayfieldLifecycleEvent> events, string stage, string detail)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Stage == stage && events[i].Detail == detail)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasDetailContains(IList<PlayfieldLifecycleEvent> events, string stage, string detail)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Stage == stage
                    && events[i].Detail.IndexOf(detail, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertMessageForStage(
            IList<PlayfieldLifecycleEvent> events,
            string stage,
            string messageType)
        {
            int index = IndexOfStage(events, stage);
            Assert.IsTrue(index >= 0, "Missing lifecycle stage " + stage + ".");
            Assert.AreEqual(messageType, events[index].MessageType, "Unexpected message type for stage " + stage + ".");
        }

        private static int CountFlow(IList<PlayfieldLifecycleEvent> events, string flow)
        {
            int count = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Flow == flow)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountStage(IList<PlayfieldLifecycleEvent> events, string stage)
        {
            int count = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Stage == stage)
                {
                    count++;
                }
            }

            return count;
        }

        private static int IndexOfStage(IList<PlayfieldLifecycleEvent> events, string stage)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Stage == stage)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int start = 0;
            while (start < text.Length)
            {
                int index = text.IndexOf(pattern, start, StringComparison.Ordinal);
                if (index < 0)
                {
                    return count;
                }

                count++;
                start = index + pattern.Length;
            }

            return count;
        }

        private static string ExtractMethodBlock(string text, string methodMarker)
        {
            int signatureIndex = text.IndexOf(methodMarker, StringComparison.Ordinal);
            Assert.IsTrue(signatureIndex >= 0, "Missing method " + methodMarker + ".");

            int startIndex = text.IndexOf("{", signatureIndex, StringComparison.Ordinal);
            Assert.IsTrue(startIndex >= 0, "Missing method body for " + methodMarker + ".");

            int depth = 0;
            for (int i = startIndex; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    depth++;
                }
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text.Substring(startIndex, i - startIndex + 1);
                    }
                }
            }

            Assert.Fail("Unterminated method body for " + methodMarker + ".");
            return string.Empty;
        }

        private static string FindRepositoryRoot([CallerFilePath] string sourcePath = null)
        {
            string current = Path.GetDirectoryName(sourcePath);
            while (!string.IsNullOrEmpty(current))
            {
                string candidate = Path.Combine(
                    current,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\Content");
                if (Directory.Exists(candidate))
                {
                    return current;
                }

                DirectoryInfo parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }

            Assert.Fail("Unable to find AORebirth repository root from " + sourcePath + ".");
            return string.Empty;
        }

        private static void RecordExpected(string flow, string[] stages)
        {
            Identity identity = new Identity { Type = IdentityType.CanbeAffected, Instance = 1 };
            for (int i = 0; i < stages.Length; i++)
            {
                PlayfieldLifecycleTrace.Record(flow, stages[i], stages[i], identity);
            }
        }

        private static void RecordPrivateCityReadyInitCurrentPacketSequence()
        {
            Identity character = new Identity { Type = IdentityType.CanbeAffected, Instance = 1001 };
            Identity playfield = new Identity { Type = IdentityType.Playfield2, Instance = 1196034 };

            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityReadyBlockBegin,
                PlayfieldLifecycleTrace.MessagePrivateCityReadyBlockBegin,
                character);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCitySimpleCharFullUpdateBroadcast,
                PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate,
                character);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityOrgInfoPacket,
                PlayfieldLifecycleTrace.MessageOrgInfoPacket,
                character,
                "Est. 2024");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCitySocialStatus,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "socialstatus=4");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityClan,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "clan=1970177");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityClanLevel,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "clanlevel=1");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCitySocialStatus,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "socialstatus=4");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCitySocialStatus,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "socialstatus=4");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCitySocialStatus,
                PlayfieldLifecycleTrace.MessageStat,
                character,
                "socialstatus=4");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityOrgInitSent,
                PlayfieldLifecycleTrace.MessagePrivateCityOrgInitSent,
                character,
                "org=1970177 orgName=Est. 2024 socialStatus=4 repeats=4");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityFullCharacter,
                PlayfieldLifecycleTrace.MessageFullCharacter,
                character);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllTowers,
                PlayfieldLifecycleTrace.MessagePlayfieldAllTowers,
                playfield);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityPlayfieldAllCities,
                PlayfieldLifecycleTrace.MessagePlayfieldAllCities,
                playfield);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityTowersCitiesSent,
                PlayfieldLifecycleTrace.MessagePrivateCityTowersCitiesSent,
                playfield,
                "cityUnknown=0 cityPayloadBytes=0");
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                PlayfieldLifecycleTrace.StagePrivateCityReadyBlockEnd,
                PlayfieldLifecycleTrace.MessagePrivateCityReadyBlockEnd,
                character);
        }

        private static readonly ForbiddenReference[] ForbiddenContentModuleReferences =
            new[]
            {
                new ForbiddenReference("combat logic", "NpcCombatTickCoordinator"),
                new ForbiddenReference("combat logic", "NpcCombatAttackRules"),
                new ForbiddenReference("combat logic", "CombatDamageRules"),
                new ForbiddenReference("combat packets", "AttackInfoMessage"),
                new ForbiddenReference("combat packets", "SpecialAttackWeaponMessage"),
                new ForbiddenReference("corpse lifecycle", "NpcCorpseLifecycleCoordinator"),
                new ForbiddenReference("corpse lifecycle", "NpcCorpseLifecycleRules"),
                new ForbiddenReference("player visibility", "CharInPlayMessageHandler"),
                new ForbiddenReference("player visibility", "SimpleCharFullUpdate"),
                new ForbiddenReference("player visibility", "FullCharacterMessageHandler"),
                new ForbiddenReference("GenericCmd routing", "GenericCmd"),
                new ForbiddenReference("GenericCmd routing", "GenericCmdMessageHandler"),
                new ForbiddenReference("inventory logic", "Inventory"),
                new ForbiddenReference("inventory logic", "ContainerAddItem"),
                new ForbiddenReference("inventory logic", "ClientMoveItem"),
                new ForbiddenReference("org commands", "OrgClient"),
                new ForbiddenReference("org commands", "OrgClientMessageHandler"),
                new ForbiddenReference("org commands", "OrgServer"),
                new ForbiddenReference("packet serialization internals", "SendCompressed"),
                new ForbiddenReference("packet serialization internals", "N3Messages"),
                new ForbiddenReference("packet serialization internals", "SystemMessages"),
                new ForbiddenReference("packet serialization internals", "Serializer"),
                new ForbiddenReference("database import", "AORebirth.Database"),
                new ForbiddenReference("database import", "ItemLoader"),
                new ForbiddenReference("database import", "NanoLoader"),
                new ForbiddenReference("database import", "CheckDatabase"),
                new ForbiddenReference("database import", "MessagePackZip"),
                new ForbiddenReference("capture tooling", "AOSharpLiveCapture"),
                new ForbiddenReference("capture tooling", "tools-temp")
            };

        private sealed class ForbiddenReference
        {
            public ForbiddenReference(string category, string pattern)
            {
                this.Category = category;
                this.Pattern = pattern;
            }

            public string Category { get; private set; }

            public string Pattern { get; private set; }
        }
    }
}
