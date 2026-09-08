CREATE TABLE IF NOT EXISTS generatedmissionsequences (
 SequenceName VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 NextIdentity INT NOT NULL, MaximumIdentity INT NOT NULL,
 PRIMARY KEY (SequenceName)
) ENGINE=InnoDB;
