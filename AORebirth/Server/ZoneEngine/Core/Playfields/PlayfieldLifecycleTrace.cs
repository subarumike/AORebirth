// This source code is licensed under the MIT license that can be found in the LICENSE file.

namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Threading;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    public interface IPlayfieldLifecycleRecorder
    {
        void Record(PlayfieldLifecycleEvent lifecycleEvent);
    }

    public sealed class PlayfieldLifecycleEvent
    {
        public PlayfieldLifecycleEvent(
            int order,
            string flow,
            string stage,
            string messageType,
            Identity identity,
            string detail)
        {
            this.Order = order;
            this.Flow = flow ?? string.Empty;
            this.Stage = stage ?? string.Empty;
            this.MessageType = messageType ?? string.Empty;
            this.Identity = identity;
            this.Detail = detail ?? string.Empty;
        }

        public int Order { get; private set; }

        public string Flow { get; private set; }

        public string Stage { get; private set; }

        public string MessageType { get; private set; }

        public Identity Identity { get; private set; }

        public string Detail { get; private set; }
    }

    public sealed class PlayfieldLifecycleCapture : IPlayfieldLifecycleRecorder, IDisposable
    {
        private readonly List<PlayfieldLifecycleEvent> events = new List<PlayfieldLifecycleEvent>();

        private readonly IPlayfieldLifecycleRecorder previousRecorder;

        private readonly int previousOrder;

        internal PlayfieldLifecycleCapture(IPlayfieldLifecycleRecorder previousRecorder, int previousOrder)
        {
            this.previousRecorder = previousRecorder;
            this.previousOrder = previousOrder;
        }

        public IList<PlayfieldLifecycleEvent> Events
        {
            get
            {
                return this.events.AsReadOnly();
            }
        }

        public void Dispose()
        {
            PlayfieldLifecycleTrace.Restore(this.previousRecorder, this.previousOrder);
        }

        public void Record(PlayfieldLifecycleEvent lifecycleEvent)
        {
            if (lifecycleEvent != null)
            {
                this.events.Add(lifecycleEvent);
            }
        }
    }

    public static class PlayfieldLifecycleTrace
    {
        public const string FlowPrivateCityReadyInit = "private-city-ready-init";

        public const string FlowSamePlayfieldVisibility = "same-playfield-visibility";

        public const string FlowCleaningRobotDeathCorpseDespawn = "cleaning-robot-death-corpse-despawn";

        public const string FlowCleaningRobotNpcAttack = "cleaning-robot-npc-attack";

        public const string MessageAttack = "Attack";

        public const string MessageAttackInfo = "AttackInfo";

        public const string MessageCharInPlay = "CharInPlay";

        public const string MessageCharacterActionDeath = "CharacterAction Death";

        public const string MessageCorpseFullUpdate = "CorpseFullUpdate";

        public const string MessageFullCharacter = "FullCharacter";

        public const string MessageOrgInfoPacket = "OrgInfoPacket";

        public const string MessagePlayfieldAllCities = "PlayfieldAllCities";

        public const string MessagePlayfieldAllTowers = "PlayfieldAllTowers";

        public const string MessageSimpleCharFullUpdate = "SimpleCharFullUpdate";

        public const string MessageStat = "Stat";

        public const string MessageSpecialAttackWeapon = "SpecialAttackWeapon";

        public const string MessageStopFight = "StopFight";

        public const string StagePrivateCitySimpleCharFullUpdateBroadcast =
            "private-city-simple-char-full-update-broadcast";

        public const string StagePrivateCityOrgInfoPacket = "private-city-org-info-packet";

        public const string StagePrivateCitySocialStatus = "private-city-social-status";

        public const string StagePrivateCityClan = "private-city-clan";

        public const string StagePrivateCityClanLevel = "private-city-clan-level";

        public const string StagePrivateCityFullCharacter = "private-city-full-character";

        public const string StagePrivateCityPlayfieldAllTowers = "private-city-playfield-all-towers";

        public const string StagePrivateCityPlayfieldAllCities = "private-city-playfield-all-cities";

        public const string StageCharInPlayReceived = "char-in-play-received";

        public const string StageCharInPlayAnnounce = "char-in-play-announce";

        public const string StageCharInPlayReady = "char-in-play-ready";

        public const string StageStaticDynelSnapshot = "static-dynel-snapshot";

        public const string StageWeaponDefinitions = "weapon-definitions";

        public const string StageTimersEnabled = "timers-enabled";

        public const string StageVisibilityJoinerReady = "visibility-joiner-ready";

        public const string StageJoiningCharacterSimpleCharFullUpdateBroadcast =
            "joining-character-simple-char-full-update-broadcast";

        public const string StageJoiningCharacterCharInPlayBroadcast =
            "joining-character-char-in-play-broadcast";

        public const string StageExistingCharacterSimpleCharFullUpdate =
            "existing-character-simple-char-full-update";

        public const string StageExistingCharacterCharInPlay = "existing-character-char-in-play";

        public const string StageAttackerStopFight = "attacker-stop-fight";

        public const string StageRobotStopFight = "robot-stop-fight";

        public const string StageCharacterActionDeathParameter2 = "character-action-death-parameter2";

        public const string StageCorpseSpawnScheduled = "corpse-spawn-scheduled";

        public const string StageDeadNpcDespawnScheduled = "dead-npc-despawn-scheduled";

        public const string StageCorpseFullUpdate = "corpse-full-update";

        public const string StageRobotSpecialAttackWeaponContext = "robot-special-attack-weapon-context";

        public const string StageRobotAttackStartContext = "robot-attack-start-context";

        public const string StageRobotAttackInfo = "robot-attack-info";

        public static readonly string[] ExpectedPrivateCityReadyInitOrder =
        {
            StagePrivateCitySimpleCharFullUpdateBroadcast,
            StagePrivateCityOrgInfoPacket,
            StagePrivateCitySocialStatus,
            StagePrivateCityClan,
            StagePrivateCityClanLevel,
            StagePrivateCitySocialStatus,
            StagePrivateCitySocialStatus,
            StagePrivateCitySocialStatus,
            StagePrivateCityFullCharacter,
            StagePrivateCityPlayfieldAllTowers,
            StagePrivateCityPlayfieldAllCities
        };

        public static readonly string[] ExpectedCharInPlayEntryOrder =
        {
            StageCharInPlayReceived,
            StageCharInPlayAnnounce,
            StageCharInPlayReady,
            StageStaticDynelSnapshot,
            StageWeaponDefinitions,
            StageTimersEnabled
        };

        public static readonly string[] ExpectedSamePlayfieldVisibilityOrder =
        {
            StageVisibilityJoinerReady,
            StageJoiningCharacterSimpleCharFullUpdateBroadcast,
            StageJoiningCharacterCharInPlayBroadcast,
            StageExistingCharacterSimpleCharFullUpdate,
            StageExistingCharacterCharInPlay
        };

        public static readonly string[] ExpectedCleaningRobotDeathOrder =
        {
            StageAttackerStopFight,
            StageRobotStopFight,
            StageCharacterActionDeathParameter2,
            StageCorpseSpawnScheduled,
            StageDeadNpcDespawnScheduled,
            StageCorpseFullUpdate
        };

        public static readonly string[] ExpectedCleaningRobotNpcAttackOrder =
        {
            StageRobotSpecialAttackWeaponContext,
            StageRobotAttackStartContext,
            StageRobotAttackInfo
        };

        [ThreadStatic]
        private static IPlayfieldLifecycleRecorder currentRecorder;

        [ThreadStatic]
        private static int currentOrder;

        public static PlayfieldLifecycleCapture Capture()
        {
            var capture = new PlayfieldLifecycleCapture(currentRecorder, currentOrder);
            currentRecorder = capture;
            currentOrder = 0;
            return capture;
        }

        public static bool ContainsExpectedOrder(
            IList<PlayfieldLifecycleEvent> events,
            string flow,
            string[] expectedStages,
            out string failure)
        {
            failure = string.Empty;
            if (events == null)
            {
                failure = "No lifecycle events were supplied.";
                return false;
            }

            if (expectedStages == null || expectedStages.Length == 0)
            {
                return true;
            }

            int expectedIndex = 0;
            for (int i = 0; i < events.Count && expectedIndex < expectedStages.Length; i++)
            {
                PlayfieldLifecycleEvent lifecycleEvent = events[i];
                if (lifecycleEvent == null || lifecycleEvent.Flow != flow)
                {
                    continue;
                }

                if (lifecycleEvent.Stage == expectedStages[expectedIndex])
                {
                    expectedIndex++;
                }
            }

            if (expectedIndex == expectedStages.Length)
            {
                return true;
            }

            failure = "Missing lifecycle stage '" + expectedStages[expectedIndex] + "' in flow '" + flow + "'.";
            return false;
        }

        public static void Record(string flow, string stage, string messageType, Identity identity)
        {
            Record(flow, stage, messageType, identity, string.Empty);
        }

        public static void Record(string flow, string stage, string messageType, Identity identity, string detail)
        {
            IPlayfieldLifecycleRecorder recorder = currentRecorder;
            if (recorder == null)
            {
                return;
            }

            int order = Interlocked.Increment(ref currentOrder);
            recorder.Record(new PlayfieldLifecycleEvent(order, flow, stage, messageType, identity, detail));
        }

        internal static void Restore(IPlayfieldLifecycleRecorder recorder, int order)
        {
            currentRecorder = recorder;
            currentOrder = order;
        }
    }
}
