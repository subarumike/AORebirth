SELECT Id1,Id2,ResultIds,QlRangePercent,DeleteFlag,Skill,SkillPercent,SkillPerBump,MaxBump
FROM tradeskill
WHERE DeleteFlag IN (0,1,2)
LIMIT 20;

SELECT DeleteFlag, COUNT(*) c FROM tradeskill GROUP BY DeleteFlag ORDER BY c DESC;

-- PPE + algorithm style (425 NP only)
SELECT Id1,Id2,ResultIds,QlRangePercent,DeleteFlag,Skill,SkillPercent,MaxBump
FROM tradeskill
WHERE Skill='160' AND SkillPercent='425'
LIMIT 10;

SELECT COUNT(*) FROM tradeskill WHERE Skill='160' AND SkillPercent='425';
