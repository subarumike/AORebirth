-- Find tool-style recipes (DeleteFlag 1 or 2) with ME skill
SELECT Id1,Id2,ResultIds,QlRangePercent,DeleteFlag,Skill,SkillPercent,SkillPerBump,MaxBump
FROM tradeskill
WHERE DeleteFlag IN (1,2) AND Skill LIKE '%125%'
LIMIT 15;

SELECT Id1,Id2,ResultIds,QlRangePercent,DeleteFlag,Skill,SkillPercent,SkillPerBump,MaxBump
FROM tradeskill
WHERE ResultIds LIKE '14476%' OR ResultIds LIKE '14480%'
LIMIT 20;

DESCRIBE itemnames;
SELECT id,name FROM itemnames WHERE name LIKE 'Carbonrich%' OR name LIKE 'Program Crystal%' OR name LIKE 'Pure Carbon%' OR name LIKE 'Jensen Personal%' LIMIT 30;
