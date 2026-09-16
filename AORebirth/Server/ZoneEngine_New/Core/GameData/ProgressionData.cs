namespace ZoneEngine_New.Core.GameData;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;

public sealed class ProgressionData
{
    private sealed record Band(int Start,int Title,int IpPerLevel);
    private sealed record Document(int StartingIp,Band[] Bands);
    private readonly Document _data;
    private static readonly Lazy<ProgressionData> Default = new(() => Load(Path.Combine(AppContext.BaseDirectory,"GameData","Progression.json")));
    public static ProgressionData Current => Default.Value;
    private ProgressionData(Document data) => _data=data;
    public static ProgressionData Load(string path)
    {
        var data=RuleDocument.Read<Document>(path);
        if(data.StartingIp<0 || data.Bands==null || data.Bands.Length==0
            || data.Bands.Any(b=>b==null || b.Start<1 || b.Title<1 || b.IpPerLevel<0) || data.Bands[0].Start!=1
            || !data.Bands.Select(b=>b.Start).SequenceEqual(data.Bands.Select(b=>b.Start).Distinct().Order()))
            throw new InvalidDataException("Progression bands are invalid.");
        return new(data);
    }
    public int TitleFor(int level) => _data.Bands.Last(b=>b.Start<=Math.Max(1,level)).Title;
    public int TotalIp(int level)
    {
        if(level<1)return 0;
        long sum=_data.StartingIp;
        for(int i=0;i<_data.Bands.Length;i++)
        {
            int first=Math.Max(2,_data.Bands[i].Start);
            int last=i+1<_data.Bands.Length ? Math.Min(level,_data.Bands[i+1].Start-1) : level;
            if(last>=first)sum+=(long)(last-first+1)*_data.Bands[i].IpPerLevel;
        }
        return checked((int)sum);
    }
}
