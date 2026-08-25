-- Unified hydration schema. Column names intentionally match C# entity properties.

CREATE TABLE IF NOT EXISTS loot_table_definitions (
    LootTableKey VARCHAR(255) PRIMARY KEY,
    DisplayName VARCHAR(255),
    TableType VARCHAR(50),
    RollMode VARCHAR(50),
    CreditsPolicyMode VARCHAR(50),
    CreditsMin INT,
    CreditsMax INT,
    CreditsObservedJson JSON,
    QualityPolicy VARCHAR(255),
    ItemPoolUnresolved BOOLEAN,
    EvidenceJson JSON,
    Confidence VARCHAR(50),
    Enabled BOOLEAN DEFAULT true,
    PlayfieldId INT,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_playfield (PlayfieldId),
    INDEX idx_enabled (Enabled)
);

CREATE TABLE IF NOT EXISTS loot_roll_groups (
    RollGroupId INT PRIMARY KEY AUTO_INCREMENT,
    LootTableKey VARCHAR(255),
    LootGroupKey VARCHAR(255),
    RollMode VARCHAR(50),
    RollCount INT,
    EmptyWeight INT,
    DropChanceBasisPoints INT,
    ConditionsJson JSON,
    FOREIGN KEY (LootTableKey) REFERENCES loot_table_definitions(LootTableKey) ON DELETE CASCADE,
    UNIQUE KEY uk_group (LootTableKey, LootGroupKey)
);

CREATE TABLE IF NOT EXISTS loot_entries (
    LootEntryId INT PRIMARY KEY AUTO_INCREMENT,
    RollGroupId INT,
    SelectionKey VARCHAR(255),
    ItemTemplateId INT,
    HighItemTemplateId INT,
    FixedQuality INT,
    UsesEnemyLevelQuality BOOLEAN,
    MinQuality INT,
    MaxQuality INT,
    MinQuantity INT,
    MaxQuantity INT,
    Weight INT,
    DropChanceBasisPoints INT,
    UniquePerCorpse BOOLEAN,
    Semantics VARCHAR(50),
    Evidence VARCHAR(255),
    EvidenceReference TEXT,
    LinkageEvidence TEXT,
    ProbabilityEvidence TEXT,
    FOREIGN KEY (RollGroupId) REFERENCES loot_roll_groups(RollGroupId) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS loot_observed_corpse_snapshots (
    SnapshotId INT PRIMARY KEY AUTO_INCREMENT,
    LootTableKey VARCHAR(255),
    SnapshotKey VARCHAR(255),
    Credits INT,
    Evidence VARCHAR(255),
    SelectionProbabilityEvidence VARCHAR(255),
    EvidenceReference TEXT,
    FOREIGN KEY (LootTableKey) REFERENCES loot_table_definitions(LootTableKey) ON DELETE CASCADE,
    UNIQUE KEY uk_snapshot (LootTableKey, SnapshotKey)
);

CREATE TABLE IF NOT EXISTS loot_snapshot_entries (
    SnapshotEntryId INT PRIMARY KEY AUTO_INCREMENT,
    SnapshotId INT,
    ItemTemplateId INT,
    HighItemTemplateId INT,
    FixedQuality INT,
    UsesEnemyLevelQuality BOOLEAN,
    MinQuality INT,
    MaxQuality INT,
    MinQuantity INT,
    MaxQuantity INT,
    Semantics VARCHAR(50),
    Evidence VARCHAR(255),
    FOREIGN KEY (SnapshotId) REFERENCES loot_observed_corpse_snapshots(SnapshotId) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS loot_assignments (
    AssignmentKey VARCHAR(255) PRIMARY KEY,
    AssignmentId INT UNIQUE AUTO_INCREMENT,
    TargetType VARCHAR(50),
    TargetKey VARCHAR(255),
    LootTableKey VARCHAR(255),
    PlayfieldId INT,
    EncounterKey VARCHAR(255),
    MinLevel INT,
    MaxLevel INT,
    Priority INT,
    ConditionsJson JSON,
    Evidence TEXT,
    Confidence VARCHAR(50),
    Enabled BOOLEAN DEFAULT true,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (LootTableKey) REFERENCES loot_table_definitions(LootTableKey),
    INDEX idx_target (TargetType, TargetKey),
    INDEX idx_playfield (PlayfieldId),
    INDEX idx_enabled (Enabled)
);

CREATE TABLE IF NOT EXISTS playfield_configurations (
    PlayfieldId INT PRIMARY KEY,
    PlayfieldName VARCHAR(255),
    GeometryResourceId INT,
    ContentProfileKey VARCHAR(255),
    LootProfileKey VARCHAR(255),
    IsInstanced BOOLEAN DEFAULT false,
    MaxInstances INT,
    RespawnPolicyKey VARCHAR(255),
    Enabled BOOLEAN DEFAULT true,
    Description TEXT,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_enabled (Enabled)
);

CREATE TABLE IF NOT EXISTS playfield_content_profiles (
    ProfileKey VARCHAR(255) PRIMARY KEY,
    PlayfieldId INT,
    ContentType ENUM('ORDINARY_ENEMY_CATALOG', 'STATIC_DUNGEON', 'MISSION', 'PRIVATE_CITY', 'MIXED'),
    SuppressDbMobSpawns BOOLEAN DEFAULT false,
    LootTableKeyOverride VARCHAR(255),
    AdditionalFlags JSON,
    Description TEXT,
    Evidence TEXT,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS ordinary_enemy_profiles (
    ProfileKey VARCHAR(255) PRIMARY KEY,
    MonsterData INT,
    EnemyName VARCHAR(255),
    FamilyKey VARCHAR(255),
    AggressionMode ENUM('PASSIVE', 'RETALIATE', 'AUTO') DEFAULT 'PASSIVE',
    AggressionRadius FLOAT,
    AutoAggro BOOLEAN DEFAULT false,
    SocialAggro BOOLEAN DEFAULT false,
    SocialAggroRadius FLOAT,
    CorpseProfileKey VARCHAR(255),
    EvidenceState VARCHAR(255),
    PlayfieldId INT,
    LootTableKey VARCHAR(255),
    Enabled BOOLEAN DEFAULT true,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_playfield (PlayfieldId),
    INDEX idx_family (FamilyKey),
    INDEX idx_enabled (Enabled)
);

CREATE TABLE IF NOT EXISTS ordinary_enemy_spawns (
    SpawnId INT PRIMARY KEY AUTO_INCREMENT,
    PlayfieldId INT NOT NULL,
    ProfileKey VARCHAR(255) NOT NULL,
    SpawnKey VARCHAR(255) UNIQUE,
    PositionX FLOAT,
    PositionY FLOAT,
    PositionZ FLOAT,
    OrientationX FLOAT,
    OrientationY FLOAT,
    OrientationZ FLOAT,
    OrientationW FLOAT,
    LevelDefinitionKey VARCHAR(255),
    MinLevel INT,
    MaxLevel INT,
    RespawnSeconds FLOAT DEFAULT 240,
    PatrolRouteId INT,
    HealthDamage INT DEFAULT 0,
    UseSpawnAsPatrolStart BOOLEAN DEFAULT false,
    LootTableKeyOverride VARCHAR(255),
    Enabled BOOLEAN DEFAULT true,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (PlayfieldId) REFERENCES playfield_configurations(PlayfieldId),
    FOREIGN KEY (ProfileKey) REFERENCES ordinary_enemy_profiles(ProfileKey),
    FOREIGN KEY (LootTableKeyOverride) REFERENCES loot_table_definitions(LootTableKey),
    INDEX idx_playfield (PlayfieldId),
    INDEX idx_profile (ProfileKey),
    INDEX idx_enabled (Enabled)
);

CREATE TABLE IF NOT EXISTS npc_patrol_routes (
    RouteId INT PRIMARY KEY AUTO_INCREMENT,
    PlayfieldId INT,
    RouteKey VARCHAR(255) UNIQUE,
    UseRuntimeStart BOOLEAN DEFAULT false,
    BatchZeroDelay BOOLEAN DEFAULT false,
    CreatedFromCaptureId VARCHAR(255),
    Description TEXT,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (PlayfieldId) REFERENCES playfield_configurations(PlayfieldId),
    INDEX idx_playfield (PlayfieldId)
);

CREATE TABLE IF NOT EXISTS npc_patrol_segments (
    SegmentId INT PRIMARY KEY AUTO_INCREMENT,
    RouteId INT NOT NULL,
    SegmentIndex INT,
    DurationSeconds FLOAT,
    StartX FLOAT,
    StartY FLOAT,
    StartZ FLOAT,
    EndX FLOAT,
    EndY FLOAT,
    EndZ FLOAT,
    SpeedPerSecond FLOAT,
    AnimationKey INT,
    FOREIGN KEY (RouteId) REFERENCES npc_patrol_routes(RouteId) ON DELETE CASCADE,
    INDEX idx_route (RouteId),
    INDEX idx_segment_index (SegmentIndex)
);

CREATE TABLE IF NOT EXISTS playfield_vendors (
    VendorId INT PRIMARY KEY AUTO_INCREMENT,
    PlayfieldId INT NOT NULL,
    VendorTemplateHash VARCHAR(255),
    VendorTemplateId INT,
    PositionX FLOAT,
    PositionY FLOAT,
    PositionZ FLOAT,
    OrientationX FLOAT,
    OrientationY FLOAT,
    OrientationZ FLOAT,
    OrientationW FLOAT,
    Name VARCHAR(255),
    SellModifier FLOAT DEFAULT 1.0,
    BuyModifier FLOAT DEFAULT 1.0,
    Enabled BOOLEAN DEFAULT true,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (PlayfieldId) REFERENCES playfield_configurations(PlayfieldId),
    INDEX idx_playfield (PlayfieldId),
    INDEX idx_enabled (Enabled)
);

CREATE TABLE IF NOT EXISTS playfield_static_dynels (
    StaticDynelId INT PRIMARY KEY AUTO_INCREMENT,
    PlayfieldId INT NOT NULL,
    DynelType VARCHAR(255),
    PositionX FLOAT,
    PositionY FLOAT,
    PositionZ FLOAT,
    OrientationX FLOAT,
    OrientationY FLOAT,
    OrientationZ FLOAT,
    OrientationW FLOAT,
    MeshId INT,
    VisualInfo JSON,
    StateJson JSON,
    Enabled BOOLEAN DEFAULT true,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (PlayfieldId) REFERENCES playfield_configurations(PlayfieldId),
    INDEX idx_playfield (PlayfieldId),
    INDEX idx_dynel_type (DynelType)
);
