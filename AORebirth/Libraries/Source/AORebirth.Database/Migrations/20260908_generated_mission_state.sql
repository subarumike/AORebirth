-- Explicit operator-only additive generated-mission storage; no existing rows are removed.
-- Identity ranges are the existing MissionOfferIdentityStore/MissionAcgAllocationService ranges.
-- Historical sidecars are NOT implicitly imported or erased. Review/import before switching an occupied server.

CREATE TABLE IF NOT EXISTS generatedmissionsequences (
 SequenceName VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 NextIdentity INT NOT NULL, MaximumIdentity INT NOT NULL,
 PRIMARY KEY (SequenceName)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS generatedmissionbatches (
 OwnerId INT NOT NULL, OwnerType INT NOT NULL, BatchIdentity VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 RollSeed INT NOT NULL, ResponseNonce INT NOT NULL, Fee INT NOT NULL,
 TerminalType INT NOT NULL, TerminalInstance INT NOT NULL, TerminalPlayfield INT NOT NULL,
 LevelSlider INT NOT NULL, GoodBadSlider INT NOT NULL, OrderChaosSlider INT NOT NULL,
 OpenHiddenSlider INT NOT NULL, PhysicalMysticalSlider INT NOT NULL, HeadOnStealthSlider INT NOT NULL,
 MoneyExperienceSlider INT NOT NULL, OfferedAtUtcTicks BIGINT NOT NULL, ExpiresAtUtcTicks BIGINT NOT NULL,
 CashBefore INT NOT NULL, CashAfter INT NOT NULL,
 PRIMARY KEY (OwnerId,BatchIdentity), UNIQUE KEY batch_identity (BatchIdentity)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS generatedmissionoffers (
 OwnerId INT NOT NULL, BatchIdentity VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 OfferIndex INT NOT NULL, OfferType INT NOT NULL, OfferInstance INT NOT NULL,
 MissionType INT NOT NULL, Quality INT NOT NULL,
 DestinationType INT NOT NULL, DestinationInstance INT NOT NULL, DestinationPlayfield INT NOT NULL,
 DestinationX FLOAT NOT NULL, DestinationY FLOAT NOT NULL, DestinationZ FLOAT NOT NULL,
 EntranceType INT NOT NULL, EntranceInstance INT NOT NULL, EntranceLow INT NOT NULL, EntranceHigh INT NOT NULL,
 CashReward INT NOT NULL, ExperienceReward INT NOT NULL,
 RewardLowId INT NOT NULL, RewardHighId INT NOT NULL, RewardQuality INT NOT NULL, RewardCount INT NOT NULL,
 Title VARCHAR(1024) NOT NULL, Description TEXT NOT NULL,
 FrozenWireBody MEDIUMBLOB NOT NULL, FrozenWireSha256 CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 State INT NOT NULL, OfferedAtUtcTicks BIGINT NOT NULL, ExpiresAtUtcTicks BIGINT NOT NULL, Version BIGINT NOT NULL DEFAULT 1,
 PRIMARY KEY (OfferType,OfferInstance),
 UNIQUE KEY owner_batch_offer (OwnerId,BatchIdentity,OfferIndex),
 KEY owner_state (OwnerId,State),
 CONSTRAINT fk_generated_offer_batch FOREIGN KEY (OwnerId,BatchIdentity) REFERENCES generatedmissionbatches (OwnerId,BatchIdentity)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS generatedmissionbindings (
 OwnerId INT NOT NULL, OfferType INT NOT NULL, OfferInstance INT NOT NULL,
 QuestType INT NOT NULL, QuestInstance INT NOT NULL,
 TeamType INT NOT NULL, TeamInstance INT NOT NULL, KeyInstance INT NOT NULL,
 BundleId VARCHAR(128) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 BundleSha256 CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 BuildingType INT NOT NULL, BuildingInstance INT NOT NULL, LivePlayfield INT NOT NULL, ActivePlayfield INT NULL,
 ObjectiveType INT NOT NULL, ObjectiveInstance INT NOT NULL, ObjectiveTemplateId INT NOT NULL, ObjectiveInteraction INT NOT NULL,
 RequiredCount INT NOT NULL, Progress INT NOT NULL DEFAULT 0, State INT NOT NULL,
 CleanupCheckpoints BIGINT NOT NULL DEFAULT 0,
 TokenProgressPercent INT NULL, TokenClaimLevel INT NULL, TokenClaimSide INT NULL, TokenDisposition INT NULL, TokenCount INT NULL,
 CompletionFrozenAtUtcTicks BIGINT NOT NULL DEFAULT 0,
 AcceptedAtUtcTicks BIGINT NOT NULL, ExpiresAtUtcTicks BIGINT NOT NULL, CompletedAtUtcTicks BIGINT NOT NULL DEFAULT 0,
 UpdatedAtUtcTicks BIGINT NOT NULL, Version BIGINT NOT NULL DEFAULT 1,
 PRIMARY KEY (QuestType,QuestInstance),
 UNIQUE KEY owner_offer (OwnerId,OfferType,OfferInstance),
 UNIQUE KEY active_playfield (ActivePlayfield),
 UNIQUE KEY key_instance (KeyInstance), KEY owner_state (OwnerId,State),
 CONSTRAINT fk_generated_binding_offer FOREIGN KEY (OfferType,OfferInstance) REFERENCES generatedmissionoffers (OfferType,OfferInstance)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS generatedmissionobservations (
 OwnerId INT NOT NULL, QuestType INT NOT NULL, QuestInstance INT NOT NULL,
 ObservationIdentity VARCHAR(191) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 LivePlayfield INT NOT NULL, ObjectiveType INT NOT NULL, ObjectiveInstance INT NOT NULL,
 ObjectiveTemplateId INT NOT NULL, Interaction INT NOT NULL, ObservedAtUtcTicks BIGINT NOT NULL,
 PRIMARY KEY (OwnerId,QuestType,QuestInstance,ObservationIdentity),
 CONSTRAINT fk_generated_observation_binding FOREIGN KEY (QuestType,QuestInstance) REFERENCES generatedmissionbindings (QuestType,QuestInstance)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS generatedmissionartifacts (
 OwnerId INT NOT NULL, QuestType INT NOT NULL, QuestInstance INT NOT NULL,
 InstanceId INT NOT NULL, ArtifactRole INT NOT NULL, CreatedAtUtcTicks BIGINT NOT NULL,
 PRIMARY KEY (InstanceId), KEY quest_artifacts (OwnerId,QuestType,QuestInstance),
 CONSTRAINT fk_generated_artifact_binding FOREIGN KEY (QuestType,QuestInstance) REFERENCES generatedmissionbindings (QuestType,QuestInstance)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS generatedmissionobjects (
 OwnerId INT NOT NULL, QuestType INT NOT NULL, QuestInstance INT NOT NULL,
 RuntimeType INT NOT NULL, RuntimeInstance INT NOT NULL, CapturedType INT NOT NULL, CapturedInstance INT NOT NULL,
 Kind INT NOT NULL, TemplateId INT NOT NULL,
 X FLOAT NOT NULL, Y FLOAT NOT NULL, Z FLOAT NOT NULL,
 HeadingX FLOAT NOT NULL, HeadingY FLOAT NOT NULL, HeadingZ FLOAT NOT NULL, HeadingW FLOAT NOT NULL,
 CurrentHealth INT NULL, MaxHealth INT NULL, Level INT NULL,
 IsDead TINYINT NOT NULL, IsOpen TINYINT NOT NULL, IsLocked TINYINT NOT NULL,
 LootResolved TINYINT NOT NULL, ObjectiveConsumed TINYINT NOT NULL,
 DeathActorId INT NOT NULL, DiedAtUtcTicks BIGINT NOT NULL, CorpseCredits INT NOT NULL,
 CorpseClaimed TINYINT NOT NULL, CorpseExpiresAtUtcTicks BIGINT NOT NULL,
 Version BIGINT NOT NULL, UpdatedAtUtcTicks BIGINT NOT NULL,
 PRIMARY KEY (QuestType,QuestInstance,RuntimeType,RuntimeInstance),
 UNIQUE KEY runtime_identity (RuntimeType,RuntimeInstance),
 KEY owner_quest (OwnerId,QuestType,QuestInstance),
 CONSTRAINT fk_generated_object_binding FOREIGN KEY (QuestType,QuestInstance) REFERENCES generatedmissionbindings (QuestType,QuestInstance)
) ENGINE=InnoDB;

INSERT INTO generatedmissionsequences (SequenceName,NextIdentity,MaximumIdentity) VALUES
 ('offer',1431736320,1442840575), ('quest',1342177280,1358954495), ('playfield',1441792,1507327)
ON DUPLICATE KEY UPDATE SequenceName=VALUES(SequenceName);
