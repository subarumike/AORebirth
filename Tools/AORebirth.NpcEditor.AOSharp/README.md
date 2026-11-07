# Optional AOSharp target bridge

The editor supports manual selection without this plugin. The bridge uses the
same .NET Framework 4.8 x86 loader contract as the existing
[NPC Inspect Probe](../aosharp_npc_inspect/README.md). It reads
`Targeting.TargetChar` and `Playfield.ModelIdentity` on `Game.OnUpdate`. It sends
no game packets, changes no targets and needs no editor credentials.

From the repository root, build against the directory containing the installed
`AOSharp.Core.dll` and `AOSharp.Common.dll`. Substitute that directory for
`<installed AOSharp directory>`.

```cmd
dotnet build Tools\AORebirth.NpcEditor.AOSharp\AORebirth.NpcEditor.AOSharp.csproj "-p:AOSharpPath=<installed AOSharp directory>"
```

With the client closed, put the built `AORebirth.NpcEditor.AOSharp.dll` in its own
plugin directory. Add that DLL through your existing AOSharp plugin loader.
Leave the installed AOSharp assemblies in place. Connect the WPF editor to the
same local ZoneEngine that the AO client is using, then select **Follow local
AOSharp**. Choose an NPC in AO and wait for the editor to show the spawn. Select
**Lock NPC** to hold that spawn while editing.

The plugin publishes short-lived snapshots in
`%LOCALAPPDATA%\AORebirth\NpcEditor`. Each snapshot contains a version, sample
time and the selected NPC's playfield, identity type and instance. The editor
ignores snapshots more than four seconds old and disables follow resolution
while more than one client is reporting. It asks ZoneEngine for the current
spawn generation before opening any target. The AOSharp API used here does
not identify the connected server, so use follow only with the same local
server selected in both applications. Manual selection remains available
when an AOSharp build is incompatible or the server reports an ambiguous
playfield or target.
