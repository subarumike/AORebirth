CREATE TABLE IF NOT EXISTS generatedmissionobservations (
 OwnerId INT NOT NULL, QuestType INT NOT NULL, QuestInstance INT NOT NULL,
 ObservationIdentity VARCHAR(191) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 LivePlayfield INT NOT NULL, ObjectiveType INT NOT NULL, ObjectiveInstance INT NOT NULL,
 ObjectiveTemplateId INT NOT NULL, Interaction INT NOT NULL, ObservedAtUtcTicks BIGINT NOT NULL,
 PRIMARY KEY (OwnerId,QuestType,QuestInstance,ObservationIdentity),
 CONSTRAINT fk_generated_observation_binding FOREIGN KEY (QuestType,QuestInstance) REFERENCES generatedmissionbindings (QuestType,QuestInstance)
) ENGINE=InnoDB;
