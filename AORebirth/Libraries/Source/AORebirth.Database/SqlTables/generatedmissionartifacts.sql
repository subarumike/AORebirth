CREATE TABLE IF NOT EXISTS generatedmissionartifacts (
 OwnerId INT NOT NULL, QuestType INT NOT NULL, QuestInstance INT NOT NULL,
 InstanceId INT NOT NULL, ArtifactRole INT NOT NULL, CreatedAtUtcTicks BIGINT NOT NULL,
 PRIMARY KEY (InstanceId), KEY quest_artifacts (OwnerId,QuestType,QuestInstance),
 CONSTRAINT fk_generated_artifact_binding FOREIGN KEY (QuestType,QuestInstance) REFERENCES generatedmissionbindings (QuestType,QuestInstance)
) ENGINE=InnoDB;
