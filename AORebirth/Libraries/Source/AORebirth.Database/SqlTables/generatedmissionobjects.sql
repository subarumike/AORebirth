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
