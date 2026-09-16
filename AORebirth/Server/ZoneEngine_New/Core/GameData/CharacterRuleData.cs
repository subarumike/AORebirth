namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.IO;
using System.Linq;
using System.Text.Json;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Entities;

/// <summary>Immutable, validated resource coefficients and ability weights loaded from editable data.</summary>
public sealed class CharacterRuleData
{
    private sealed record Resource(int[] Base, int[][] LevelByTitleAndProfession, int[] LevelByBreed,
        int[] SkillByBreed, string Skill, int? ProfessionShiftAfter, string? OverrideStat, int? OverrideValue);
    private sealed record Document(int FormatVersion, Dictionary<string, Resource> Vitals,
        double SkillDivisor, string[] AbilityOrder, Dictionary<int, double[]> Skills);
    private readonly FrozenDictionary<string, Resource> _resources;
    private readonly FrozenDictionary<int, double[]> _weights;
    private readonly CharacterStat[] _abilities;
    private readonly double _divisor;
    private static readonly Lazy<CharacterRuleData> Default = new(() => Load(Path.Combine(AppContext.BaseDirectory,"GameData","CharacterRules.json")));
    public static CharacterRuleData Current => Default.Value;

    private CharacterRuleData(Document document)
    {
        _resources=document.Vitals.ToFrozenDictionary(StringComparer.Ordinal);
        _weights=document.Skills.ToFrozenDictionary();
        _abilities=document.AbilityOrder.Select(Enum.Parse<CharacterStat>).ToArray();
        _divisor=document.SkillDivisor;
    }

    public static CharacterRuleData Load(string path)
    {
        var d=RuleDocument.Read<Document>(path);
        if(d.FormatVersion!=1 || d.Vitals==null || d.Vitals.Count==0 || d.Skills==null || d.Skills.Count==0
            || d.AbilityOrder==null || d.AbilityOrder.Length==0 || !double.IsFinite(d.SkillDivisor) || d.SkillDivisor<=0)
            throw new InvalidDataException("Character rules header is invalid.");
        foreach(var r in d.Vitals.Values)
        {
            if(r==null || r.Base==null || r.Base.Length==0 || r.LevelByBreed==null || r.SkillByBreed==null
                || r.LevelByBreed.Length<r.Base.Length || r.SkillByBreed.Length<r.Base.Length
                || r.LevelByBreed.Length!=r.SkillByBreed.Length
                || r.LevelByTitleAndProfession==null || r.LevelByTitleAndProfession.Length==0
                || r.LevelByTitleAndProfession.Any(row=>row==null || row.Length==0)
                || !Enum.TryParse<CharacterStat>(r.Skill,out _)
                || (r.OverrideStat!=null && (!Enum.TryParse<CharacterStat>(r.OverrideStat,out _) || r.OverrideValue is not >0)))
                throw new InvalidDataException("Resource rule is invalid.");
            if(r.LevelByTitleAndProfession.Any(row=>row.Length!=r.LevelByTitleAndProfession[0].Length)
                || r.Base.Any(v=>v<0) || r.SkillByBreed.Any(v=>v<0)
                || r.LevelByTitleAndProfession.Any(row=>row.Any(v=>v<0)))
                throw new InvalidDataException("Resource coefficient dimensions or values are invalid.");
        }
        if(d.AbilityOrder.Any(a=>!Enum.TryParse<CharacterStat>(a,out _)) || d.AbilityOrder.Distinct().Count()!=d.AbilityOrder.Length
            || d.Skills.Any(pair=>!Enum.IsDefined(typeof(CharacterStat),pair.Key) || pair.Value==null
                || pair.Value.Length!=d.AbilityOrder.Length || pair.Value.Any(w=>!double.IsFinite(w) || w<0)))
            throw new InvalidDataException("Skill weights are invalid.");
        return new CharacterRuleData(d);
    }

    public int ComputeVital(string resource,int breed,int profession,int title,int level,int skill)
    {
        var r=_resources[resource];
        int b=Math.Clamp(breed,1,r.Base.Length)-1;
        int[] rates=r.LevelByTitleAndProfession[Math.Clamp(title,1,r.LevelByTitleAndProfession.Length)-1];
        int p=profession-(r.ProfessionShiftAfter is int threshold && profession>threshold ? 1 : 0);
        return checked(r.Base[b]+Math.Max(1,level)*(rates[Math.Clamp(p,1,rates.Length)-1]+r.LevelByBreed[b])
            +Math.Max(1,skill)*r.SkillByBreed[b]);
    }

    public int SkillContribution(IReadOnlyDictionary<CharacterStat,int> stats,CharacterStat skill)
    {
        if(!_weights.TryGetValue((int)skill,out var weights)) throw new InvalidDataException("Skill has no configured weights.");
        double total=0;
        for(int i=0;i<_abilities.Length;i++) total+=weights[i]*stats.GetValueOrDefault(_abilities[i]);
        return checked((int)Math.Floor(total/_divisor));
    }

    public int ComputeVital(string resource,IReadOnlyDictionary<CharacterStat,int> stats)
    {
        var rule=_resources[resource];
        if(rule.OverrideStat is string trigger && stats.GetValueOrDefault(Enum.Parse<CharacterStat>(trigger))>0)
            return rule.OverrideValue!.Value;
        return ComputeVital(resource,stats.GetValueOrDefault(CharacterStat.Breed),stats.GetValueOrDefault(CharacterStat.Profession),
            stats.GetValueOrDefault(CharacterStat.TitleLevel),stats.GetValueOrDefault(CharacterStat.Level),
            stats.GetValueOrDefault(Enum.Parse<CharacterStat>(rule.Skill)));
    }

    public int EffectiveSkill(StatCollection stats,CharacterStat skill)
    {
        var values=stats.GetEntries().ToDictionary(e=>e.Stat,e=>checked(e.Base+e.Bonus));
        return Math.Max(1,checked(stats.GetOrZero(skill)+SkillContribution(values,skill)));
    }

    public bool TryVital(string resource,StatCollection stats,out int value)
    {
        value=0;
        if(stats.GetOrZero(CharacterStat.NPCFamily)>0)return false;
        var r=_resources[resource];
        value=r.OverrideStat is string trigger && stats.GetOrZero(Enum.Parse<CharacterStat>(trigger))>0
            ? r.OverrideValue!.Value
            : ComputeVital(resource,stats.GetOrZero(CharacterStat.Breed),stats.GetOrZero(CharacterStat.Profession),
                stats.GetOrZero(CharacterStat.TitleLevel),stats.GetOrZero(CharacterStat.Level),EffectiveSkill(stats,Enum.Parse<CharacterStat>(r.Skill)));
        return value>0;
    }
}
