SELECT Id1,Id2,ResultIds,QlRangePercent,DeleteFlag,Skill,SkillPercent,SkillPerBump,MaxBump
FROM tradeskill
WHERE SkillPerBump != '0' AND SkillPerBump != '0,0' AND Skill LIKE '%125%'
LIMIT 15;

SELECT Id1,Id2,ResultIds,QlRangePercent,DeleteFlag,Skill,SkillPercent,SkillPerBump,MaxBump
FROM tradeskill
WHERE MaxBump > 0 AND Skill LIKE '%125%'
LIMIT 15;

-- isotope / neutron high ids
SELECT Id, Name FROM itemnames WHERE Name IN ('Isotope Separator','Neutron Displacer') ORDER BY Id;
