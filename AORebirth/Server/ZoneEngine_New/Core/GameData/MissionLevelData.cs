namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

/// <summary>Reads editable mission-level data. No mission generation or rewards are enabled by loading it.</summary>
public sealed class MissionLevelData
{
    private readonly FrozenDictionary<int, int[]> _rows;
    private MissionLevelData(Dictionary<int, int[]> rows) => _rows = rows.ToFrozenDictionary();
    public int Count => _rows.Count;

    public int Quality(int level, int column)
    {
        if (column < 0 || column > 10) throw new ArgumentOutOfRangeException(nameof(column));
        return _rows[level][column];
    }

    public int Tokens(int level) => _rows[level][11];

    public static MissionLevelData Load(string path)
    {
        using var reader = File.OpenText(path);
        if (reader.ReadLine() != "Level,Q0,Q1,Q2,Q3,Q4,Q5,Q6,Q7,Q8,Q9,Q10,Tokens")
            throw new InvalidDataException("Mission level columns are invalid.");
        var rows = new Dictionary<int, int[]>();
        while (reader.ReadLine() is { } line)
        {
            string[] cells = line.Split(',');
            if (cells.Length != 13) throw new InvalidDataException("Mission level row width is invalid.");
            int[] values = new int[13];
            for (int i = 0; i < cells.Length; i++)
                if (!int.TryParse(cells[i], NumberStyles.None, CultureInfo.InvariantCulture, out values[i]) || values[i] <= 0)
                    throw new InvalidDataException("Mission level values must be positive integers.");
            if (values[0] != rows.Count + 1) throw new InvalidDataException("Mission levels must be unique and consecutive from one.");
            for (int i = 2; i <= 11; i++)
                if (values[i] < values[i - 1]) throw new InvalidDataException("Mission qualities must be ordered.");
            rows.Add(values[0], values[1..]);
        }
        if (rows.Count == 0) throw new InvalidDataException("Mission level data is empty.");
        return new MissionLevelData(rows);
    }
}
