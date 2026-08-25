-- Ces tables existent probablement déjà, à vérifier/adapter
CREATE TABLE IF NOT EXISTS loot_table_definitions (
    loot_table_key VARCHAR(255) PRIMARY KEY,
    display_name VARCHAR(255),
    table_type VARCHAR(50),  -- 'GlobalDefault', 'Family', 'EnemyType', 'SpawnOverride', 'Boss', etc.
    roll_mode VARCHAR(50),   -- 'All', 'WeightedOne', 'WeightedMany', 'Independent', 'Guaranteed', 'ObservedSnapshot'
    credits_policy_mode VARCHAR(50),  -- 'None', 'Fixed', 'Range', 'ObservedSet', 'ObservedSamples'
    credits_min INT,
    credits_max INT,
    credits_observed_json JSON,  -- Liste des crédits observés en captures
    quality_policy VARCHAR(255),
    item_pool_unresolved BOOLEAN,
    evidence_json JSON,
    confidence VARCHAR(50),  -- 'ProvenRepository', 'ProvenCapture', 'CommunityDocumented', etc.
    enabled BOOLEAN DEFAULT true,
    playfield_id INT,  -- NEW: null = global, ou PF spécifique
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_playfield (playfield_id),
    INDEX idx_enabled (enabled)
);

CREATE TABLE IF NOT EXISTS loot_roll_groups (
    roll_group_id INT PRIMARY KEY AUTO_INCREMENT,
    loot_table_key VARCHAR(255),
    loot_group_key VARCHAR(255),
    roll_mode VARCHAR(50),
    roll_count INT,
    empty_weight INT,
    drop_chance_basis_points INT,  -- 0-10000
    conditions_json JSON,  -- Conditions applicabilité
    FOREIGN KEY (loot_table_key) REFERENCES loot_table_definitions(loot_table_key) ON DELETE CASCADE,
    UNIQUE KEY uk_group (loot_table_key, loot_group_key)
);

CREATE TABLE IF NOT EXISTS loot_entries (
    loot_entry_id INT PRIMARY KEY AUTO_INCREMENT,
    roll_group_id INT,
    selection_key VARCHAR(255),
    item_template_id INT,
    high_item_template_id INT,
    fixed_quality INT,
    uses_enemy_level_quality BOOLEAN,
    min_quality INT,
    max_quality INT,
    min_quantity INT,
    max_quantity INT,
    weight INT,
    drop_chance_basis_points INT,
    unique_per_corpse BOOLEAN,
    semantics VARCHAR(50),  -- 'GuaranteedProven', 'ObservedAvailable', etc.
    evidence VARCHAR(255),
    evidence_reference TEXT,
    linkage_evidence TEXT,
    probability_evidence TEXT,
    FOREIGN KEY (roll_group_id) REFERENCES loot_roll_groups(roll_group_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS loot_observed_corpse_snapshots (
    snapshot_id INT PRIMARY KEY AUTO_INCREMENT,
    loot_table_key VARCHAR(255),
    snapshot_key VARCHAR(255),
    credits INT,
    evidence VARCHAR(255),
    selection_probability_evidence VARCHAR(255),
    evidence_reference TEXT,
    FOREIGN KEY (loot_table_key) REFERENCES loot_table_definitions(loot_table_key) ON DELETE CASCADE,
    UNIQUE KEY uk_snapshot (loot_table_key, snapshot_key)
);

CREATE TABLE IF NOT EXISTS loot_snapshot_entries (
    snapshot_entry_id INT PRIMARY KEY AUTO_INCREMENT,
    snapshot_id INT,
    item_template_id INT,
    high_item_template_id INT,
    fixed_quality INT,
    uses_enemy_level_quality BOOLEAN,
    min_quality INT,
    max_quality INT,
    min_quantity INT,
    max_quantity INT,
    semantics VARCHAR(50),
    evidence VARCHAR(255),
    FOREIGN KEY (snapshot_id) REFERENCES loot_observed_corpse_snapshots(snapshot_id) ON DELETE CASCADE
);

-- Assignations : quelle table loot pour quel ennemi/spawn/famille
CREATE TABLE IF NOT EXISTS loot_assignments (
    assignment_key VARCHAR(255) PRIMARY KEY,
    assignment_id INT UNIQUE AUTO_INCREMENT,
    target_type VARCHAR(50),  -- 'Global', 'Family', 'EnemyType', 'Spawn', 'Boss', 'Encounter', 'Dungeon'
    target_key VARCHAR(255),  -- spawn.key, family.key, enemy.type, etc.
    loot_table_key VARCHAR(255),
    playfield_id INT,  -- NEW: null = global, ou PF spécifique
    encounter_key VARCHAR(255),  -- Si type = 'Encounter'
    min_level INT,
    max_level INT,
    priority INT,
    conditions_json JSON,
    evidence TEXT,
    confidence VARCHAR(50),
    enabled BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (loot_table_key) REFERENCES loot_table_definitions(loot_table_key),
    INDEX idx_target (target_type, target_key),
    INDEX idx_playfield (playfield_id),
    INDEX idx_enabled (enabled)
);

-- Configuration globale playfield
CREATE TABLE playfield_configurations (
    playfield_id INT PRIMARY KEY,
    playfield_name VARCHAR(255),
    geometry_resource_id INT,
    content_profile_key VARCHAR(255),  -- Référence stratégie peuplement
    loot_profile_key VARCHAR(255),     -- NEW: référence ensemble tables loot pour ce PF
    is_instanced BOOLEAN DEFAULT false,
    max_instances INT,
    respawn_policy_key VARCHAR(255),
    enabled BOOLEAN DEFAULT true,
    description TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_enabled (enabled)
);

-- Stratégies de contenu
CREATE TABLE playfield_content_profiles (
    profile_key VARCHAR(255) PRIMARY KEY,
    playfield_id INT,
    content_type ENUM('ORDINARY_ENEMY_CATALOG', 'STATIC_DUNGEON', 'MISSION', 'PRIVATE_CITY', 'MIXED'),
    suppress_db_mob_spawns BOOLEAN DEFAULT false,
    loot_table_key_override VARCHAR(255),  -- Tableau loot spécifique si override
    additional_flags JSON,
    description TEXT,
    evidence TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Profils d'ennemis ordinaires (unification)
CREATE TABLE ordinary_enemy_profiles (
    profile_key VARCHAR(255) PRIMARY KEY,
    monster_data INT,
    enemy_name VARCHAR(255),
    family_key VARCHAR(255),
    aggression_mode ENUM('PASSIVE', 'RETALIATE', 'AUTO') DEFAULT 'PASSIVE',
    aggression_radius FLOAT,
    auto_aggro BOOLEAN DEFAULT false,
    social_aggro BOOLEAN DEFAULT false,
    social_aggro_radius FLOAT,
    corpse_profile_key VARCHAR(255),
    evidence_state VARCHAR(255),
    playfield_id INT,  -- nullable pour réutilisabilité cross-playfield
    loot_table_key VARCHAR(255),  -- NEW: loot pour ce profil (peut être overridé par spawn)
    enabled BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_playfield (playfield_id),
    INDEX idx_family (family_key),
    INDEX idx_enabled (enabled)
);

-- Spawns ordinaires
CREATE TABLE ordinary_enemy_spawns (
    spawn_id INT PRIMARY KEY AUTO_INCREMENT,
    playfield_id INT NOT NULL,
    profile_key VARCHAR(255) NOT NULL,
    spawn_key VARCHAR(255) UNIQUE,  -- "subway.spawn.0x794DF1E5"
    position_x FLOAT,
    position_y FLOAT,
    position_z FLOAT,
    orientation_x FLOAT,
    orientation_y FLOAT,
    orientation_z FLOAT,
    orientation_w FLOAT,
    level_definition_key VARCHAR(255),  -- "fixed:10" ou "band:5-15"
    min_level INT,
    max_level INT,
    respawn_seconds FLOAT DEFAULT 240,
    patrol_route_id INT,
    health_damage INT DEFAULT 0,
    use_spawn_as_patrol_start BOOLEAN DEFAULT false,
    loot_table_key_override VARCHAR(255),  -- NEW: override de loot si spawn spécifique
    enabled BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (playfield_id) REFERENCES playfield_configurations(playfield_id),
    FOREIGN KEY (profile_key) REFERENCES ordinary_enemy_profiles(profile_key),
    FOREIGN KEY (loot_table_key_override) REFERENCES loot_table_definitions(loot_table_key),
    INDEX idx_playfield (playfield_id),
    INDEX idx_profile (profile_key),
    INDEX idx_enabled (enabled)
);

-- Routes de patrol
CREATE TABLE npc_patrol_routes (
    route_id INT PRIMARY KEY AUTO_INCREMENT,
    playfield_id INT,
    route_key VARCHAR(255) UNIQUE,
    use_runtime_start BOOLEAN DEFAULT false,
    batch_zero_delay BOOLEAN DEFAULT false,
    created_from_capture_id VARCHAR(255),  -- Audit trail
    description TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (playfield_id) REFERENCES playfield_configurations(playfield_id),
    INDEX idx_playfield (playfield_id)
);

CREATE TABLE npc_patrol_segments (
    segment_id INT PRIMARY KEY AUTO_INCREMENT,
    route_id INT NOT NULL,
    segment_index INT,
    duration_seconds FLOAT,
    start_x FLOAT, start_y FLOAT, start_z FLOAT,
    end_x FLOAT, end_y FLOAT, end_z FLOAT,
    speed_per_second FLOAT,
    animation_key INT,
    FOREIGN KEY (route_id) REFERENCES npc_patrol_routes(route_id) ON DELETE CASCADE,
    INDEX idx_route (route_id),
    INDEX idx_segment_index (segment_index)
);

-- Vendors
CREATE TABLE playfield_vendors (
    vendor_id INT PRIMARY KEY AUTO_INCREMENT,
    playfield_id INT NOT NULL,
    vendor_template_hash VARCHAR(255),
    vendor_template_id INT,
    position_x FLOAT,
    position_y FLOAT,
    position_z FLOAT,
    orientation_x FLOAT,
    orientation_y FLOAT,
    orientation_z FLOAT,
    orientation_w FLOAT,
    name VARCHAR(255),
    sell_modifier FLOAT DEFAULT 1.0,
    buy_modifier FLOAT DEFAULT 1.0,
    enabled BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (playfield_id) REFERENCES playfield_configurations(playfield_id),
    INDEX idx_playfield (playfield_id),
    INDEX idx_enabled (enabled)
);

-- Objets statiques (portes, objets monde)
CREATE TABLE playfield_static_dynels (
    static_dynel_id INT PRIMARY KEY AUTO_INCREMENT,
    playfield_id INT NOT NULL,
    dynel_type VARCHAR(255),  -- "door", "object", "NPC_vendor", etc.
    position_x FLOAT,
    position_y FLOAT,
    position_z FLOAT,
    orientation_x FLOAT,
    orientation_y FLOAT,
    orientation_z FLOAT,
    orientation_w FLOAT,
    mesh_id INT,
    visual_info JSON,
    state_json JSON,  -- initial door state, etc.
    enabled BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (playfield_id) REFERENCES playfield_configurations(playfield_id),
    INDEX idx_playfield (playfield_id),
    INDEX idx_dynel_type (dynel_type)
);