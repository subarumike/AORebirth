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
