-- Capture 20260720-190432 Personalized Robot Brain chain (existing table rows only).
-- DeleteFlag: 2 = keep source / delete target; 3 = delete both.

INSERT INTO tradeskill (Id1, Id2, MinTarget, ResultIds, QlRangePercent, DeleteFlag, Skill, SkillPercent, SkillPerBump, MaxBump, MinXP, MaxXP, IsImplant)
SELECT 150922, 42619, 0, '150923,150924', 0, 2, '0', '0', '0', 0, 0, 0, 0
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM tradeskill WHERE Id1=150922 AND Id2=42619 LIMIT 1);

INSERT INTO tradeskill (Id1, Id2, MinTarget, ResultIds, QlRangePercent, DeleteFlag, Skill, SkillPercent, SkillPerBump, MaxBump, MinXP, MaxXP, IsImplant)
SELECT 150922, 42620, 0, '150923,150924', 0, 2, '0', '0', '0', 0, 0, 0, 0
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM tradeskill WHERE Id1=150922 AND Id2=42620 LIMIT 1);

INSERT INTO tradeskill (Id1, Id2, MinTarget, ResultIds, QlRangePercent, DeleteFlag, Skill, SkillPercent, SkillPerBump, MaxBump, MinXP, MaxXP, IsImplant)
SELECT 156020, 150923, 0, '156022,156023', 0, 3, '0', '0', '0', 0, 0, 0, 0
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM tradeskill WHERE Id1=156020 AND Id2=150923 LIMIT 1);

INSERT INTO tradeskill (Id1, Id2, MinTarget, ResultIds, QlRangePercent, DeleteFlag, Skill, SkillPercent, SkillPerBump, MaxBump, MinXP, MaxXP, IsImplant)
SELECT 156020, 150924, 0, '156022,156023', 0, 3, '0', '0', '0', 0, 0, 0, 0
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM tradeskill WHERE Id1=156020 AND Id2=150924 LIMIT 1);

INSERT INTO tradeskill (Id1, Id2, MinTarget, ResultIds, QlRangePercent, DeleteFlag, Skill, SkillPercent, SkillPerBump, MaxBump, MinXP, MaxXP, IsImplant)
SELECT 156021, 150923, 0, '156022,156023', 0, 3, '0', '0', '0', 0, 0, 0, 0
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM tradeskill WHERE Id1=156021 AND Id2=150923 LIMIT 1);

INSERT INTO tradeskill (Id1, Id2, MinTarget, ResultIds, QlRangePercent, DeleteFlag, Skill, SkillPercent, SkillPerBump, MaxBump, MinXP, MaxXP, IsImplant)
SELECT 156021, 150924, 0, '156022,156023', 0, 3, '0', '0', '0', 0, 0, 0, 0
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM tradeskill WHERE Id1=156021 AND Id2=150924 LIMIT 1);

INSERT INTO tradeskill (Id1, Id2, MinTarget, ResultIds, QlRangePercent, DeleteFlag, Skill, SkillPercent, SkillPerBump, MaxBump, MinXP, MaxXP, IsImplant)
SELECT 156024, 156022, 0, '156026,156027', 0, 3, '0', '0', '0', 0, 0, 0, 0
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM tradeskill WHERE Id1=156024 AND Id2=156022 LIMIT 1);

INSERT INTO tradeskill (Id1, Id2, MinTarget, ResultIds, QlRangePercent, DeleteFlag, Skill, SkillPercent, SkillPerBump, MaxBump, MinXP, MaxXP, IsImplant)
SELECT 156024, 156023, 0, '156026,156027', 0, 3, '0', '0', '0', 0, 0, 0, 0
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM tradeskill WHERE Id1=156024 AND Id2=156023 LIMIT 1);

INSERT INTO tradeskill (Id1, Id2, MinTarget, ResultIds, QlRangePercent, DeleteFlag, Skill, SkillPercent, SkillPerBump, MaxBump, MinXP, MaxXP, IsImplant)
SELECT 156025, 156022, 0, '156026,156027', 0, 3, '0', '0', '0', 0, 0, 0, 0
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM tradeskill WHERE Id1=156025 AND Id2=156022 LIMIT 1);

INSERT INTO tradeskill (Id1, Id2, MinTarget, ResultIds, QlRangePercent, DeleteFlag, Skill, SkillPercent, SkillPerBump, MaxBump, MinXP, MaxXP, IsImplant)
SELECT 156025, 156023, 0, '156026,156027', 0, 3, '0', '0', '0', 0, 0, 0, 0
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM tradeskill WHERE Id1=156025 AND Id2=156023 LIMIT 1);

SELECT Id1, Id2, ResultIds, DeleteFlag FROM tradeskill
WHERE Id1 IN (150922,156020,156021,156024,156025)
  AND Id2 IN (42619,42620,150923,150924,156022,156023);
