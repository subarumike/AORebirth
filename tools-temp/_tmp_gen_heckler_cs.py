import json, os, re

base = r'C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260716-071407'
data = json.load(open(os.path.join(base, 'enemy-dossier.json'), encoding='utf-8-sig'))
rows = data.get('enemies') or data.get('Entries') or list(data.values())
if isinstance(rows, dict):
    rows = list(rows.values())

by_id = {}
for r in rows:
    if not isinstance(r, dict):
        continue
    name = r.get('name') or ''
    if 'heckler' not in name.lower():
        continue
    ident = r.get('identity') or ''
    m = re.search(r'([0-9A-Fa-f]{8})', ident)
    if not m:
        continue
    source = int(m.group(1), 16)
    pos = r.get('position') or {}
    by_id[source] = (
        name,
        float(pos['x']), float(pos['y']), float(pos['z']),
        int(r.get('level') or 80),
        int(r.get('maxHealth') or 5733),
        int(r.get('runSpeed') or 285),
    )

lines = []
lines.append('namespace ZoneEngine.Core.Playfields')
lines.append('{')
lines.append('    #region Usings ...')
lines.append('')
lines.append('    using System;')
lines.append('')
lines.append('    #endregion')
lines.append('')
lines.append('    /// <summary>')
lines.append('    /// Capture-backed Heckler spawns for Nascence Core (PF 4312).')
lines.append('    /// Evidence: tools-temp/AOSharpLiveCapture/.../captures/20260716-071407')
lines.append('    /// </summary>')
lines.append('    internal static class NascenceCoreHecklerContentProvider')
lines.append('    {')
lines.append('        internal const int PlayfieldInstance = 4312;')
lines.append('        internal const int MonsterData = 214982;')
lines.append('        internal const int NpcFamily = 171;')
lines.append('        internal const int MonsterScale = 100;')
lines.append('        internal const int VisualFlags = 31;')
lines.append('        internal const double RespawnDelaySeconds = 600.0;')
lines.append('        internal const string CaptureId = "20260716-071407";')
lines.append('        internal const string TemplateHash = "BART";')
lines.append('')
lines.append('        // Combat from fought Heckler of Earth 796C7244')
lines.append('        internal const int MinDamage = 106;')
lines.append('        internal const int MaxDamage = 320;')
lines.append('        internal const int CritDamage = 411;')
lines.append('        internal const double RechargeSeconds = 2.0;')
lines.append('        internal const int SpecialAttackWeaponUnknown = 480;')
lines.append('        internal const int PrimaryWeaponInstance = 1145132106; // DATJ')
lines.append('')
lines.append('        private static readonly NascenceCoreHecklerSpawnDefinition[] Spawns =')
lines.append('            new NascenceCoreHecklerSpawnDefinition[]')
lines.append('            {')
for source in sorted(by_id.keys()):
    name, x, y, z, level, hp, run = by_id[source]
    lines.append(
        '                new NascenceCoreHecklerSpawnDefinition(0x{:08X}, "{}", {}, {:0.6f}f, {:0.6f}f, {:0.6f}f, {}, {}),'.format(
            source, name, level, x, y, z, hp, run)
    )
lines.append('            };')
lines.append('')
lines.append('        internal static NascenceCoreHecklerSpawnDefinition[] GetSpawns()')
lines.append('        {')
lines.append('            return (NascenceCoreHecklerSpawnDefinition[])Spawns.Clone();')
lines.append('        }')
lines.append('    }')
lines.append('')
lines.append('    internal sealed class NascenceCoreHecklerSpawnDefinition')
lines.append('    {')
lines.append('        internal NascenceCoreHecklerSpawnDefinition(')
lines.append('            int sourceIdentity,')
lines.append('            string name,')
lines.append('            int level,')
lines.append('            float x,')
lines.append('            float y,')
lines.append('            float z,')
lines.append('            int health,')
lines.append('            int runSpeed)')
lines.append('        {')
lines.append('            this.SourceIdentity = sourceIdentity;')
lines.append('            this.Name = name;')
lines.append('            this.Level = level;')
lines.append('            this.X = x;')
lines.append('            this.Y = y;')
lines.append('            this.Z = z;')
lines.append('            this.Health = health;')
lines.append('            this.RunSpeed = runSpeed;')
lines.append('        }')
lines.append('')
lines.append('        internal int SourceIdentity { get; private set; }')
lines.append('        internal string Name { get; private set; }')
lines.append('        internal int Level { get; private set; }')
lines.append('        internal float X { get; private set; }')
lines.append('        internal float Y { get; private set; }')
lines.append('        internal float Z { get; private set; }')
lines.append('        internal int Health { get; private set; }')
lines.append('        internal int RunSpeed { get; private set; }')
lines.append('    }')
lines.append('}')

out = r'C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Playfields\NascenceCoreHecklerContentProvider.cs'
open(out, 'w', encoding='utf-8', newline='\r\n').write('\n'.join(lines) + '\n')
print('wrote', out, 'count', len(by_id))
