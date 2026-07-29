using System.Collections.Generic;
using AORebirth.Database.Entities;

namespace ZoneEngine.Core;

public class TradeSkillEntry
{
	public int DeleteFlag;

	public int ID1;

	public int ID2;

	public bool IsImplant;

	public int MaxBump;

	public int MaxXP;

	public int MinTargetQL;

	public int MinXP;

	public int QLRangePercent;

	public int ResultHighId;

	public int ResultLowId;

	public List<TradeSkillSkill> Skills = new List<TradeSkillSkill>();

	public static TradeSkillEntry ConvertFromDB(DBTradeSkill ts)
	{
		TradeSkillEntry tradeSkillEntry = new TradeSkillEntry();
		tradeSkillEntry.ID1 = ts.ID1;
		tradeSkillEntry.ID2 = ts.ID2;
		tradeSkillEntry.IsImplant = ts.IsImplant > 0;
		tradeSkillEntry.MaxBump = ts.MaxBump;
		tradeSkillEntry.MaxXP = ts.MaxXP;
		tradeSkillEntry.MinTargetQL = ts.MinTarget;
		tradeSkillEntry.MinXP = ts.MinXP;
		tradeSkillEntry.ResultLowId = int.Parse(ts.ResultIDS.Split(',')[0].Trim());
		tradeSkillEntry.ResultHighId = int.Parse(ts.ResultIDS.Split(',')[1].Trim());
		tradeSkillEntry.QLRangePercent = ts.QLRangePercent;
		tradeSkillEntry.DeleteFlag = ts.DeleteFlag;
		string[] array = ts.Skill.Split(',');
		string[] array2 = ts.SkillPercent.Split(',');
		string[] array3 = ts.SkillPerBump.Split(',');
		int num = array.Length;
		for (int i = 0; i < num; i++)
		{
			TradeSkillSkill tradeSkillSkill = new TradeSkillSkill();
			tradeSkillSkill.StatId = int.Parse(array[i].Trim());
			tradeSkillSkill.SkillPerBump = int.Parse(array3[i].Trim());
			tradeSkillSkill.Percent = int.Parse(array2[i].Trim());
			tradeSkillEntry.Skills.Add(tradeSkillSkill);
		}
		return tradeSkillEntry;
	}

	internal bool ValidateRange(int sourceQL, int targetQL)
	{
		if (QLRangePercent != 0)
		{
			if (QLRangePercent == 1)
			{
				return sourceQL >= targetQL;
			}
			return ((decimal)targetQL - (decimal)sourceQL) / (decimal)targetQL <= (decimal)QLRangePercent / 100m;
		}
		return true;
	}
}
