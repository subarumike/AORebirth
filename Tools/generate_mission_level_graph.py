"""Validate the editable mission-level CSV. This command never emits C#."""
import argparse
import csv
import io
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]

def validate(source):
    rows=list(csv.reader(io.StringIO(source)))
    expected=['Level']+['Q'+str(i) for i in range(11)]+['Tokens']
    if not rows or rows[0]!=expected:raise ValueError('Invalid mission-level columns.')
    if len(rows)<2:raise ValueError('Empty mission-level data.')
    for level,row in enumerate(rows[1:],1):
        if len(row)!=13 or any(not cell.isascii() or not cell.isdecimal() for cell in row):raise ValueError('Invalid mission-level row.')
        values=list(map(int,row))
        if values[0]!=level or min(values)<=0 or values[1:12]!=sorted(values[1:12]):raise ValueError('Invalid mission-level sequence or quality.')
    return len(rows)-1

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source',type=Path,default=ROOT/'AORebirth/GameData/Missions/Source/MissionLevels.csv')
    parser.add_argument('--check',action='store_true',help='Validation-only; always the behavior of this command.')
    args=parser.parse_args()
    validate(args.source.read_text(encoding='utf-8-sig'))
    print('MISSION_LEVEL_EDITABLE_DATA=PASS; CSHARP_GENERATION=DISABLED')
