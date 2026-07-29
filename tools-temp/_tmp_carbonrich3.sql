SELECT Id1,Id2,ResultIds,IsImplant,MaxBump,SkillPerBump FROM tradeskill WHERE ResultIds LIKE '%144799%' OR ResultIds LIKE '%144801%' OR (Id1 BETWEEN 150275 AND 150281) LIMIT 10;
SELECT COUNT(*) AS swapped_style FROM tradeskill WHERE ResultIds='144767,144770';
SELECT Id1,Id2,ResultIds,MaxBump,SkillPerBump,IsImplant FROM tradeskill WHERE MaxBump>0 AND IsImplant=0 LIMIT 10;
