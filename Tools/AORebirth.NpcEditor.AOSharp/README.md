# AOSharp target and read-only observation bridge

The editor supports manual selection without this plugin. The bridge uses the
same .NET Framework 4.8 x86 loader contract as the existing
[NPC Inspect Probe](../aosharp_npc_inspect/README.md). It reads
`Targeting.TargetChar` and `Playfield.ModelIdentity` on `Game.OnUpdate`. It sends
no game packets, changes no targets and needs no editor credentials. The
research snapshot adds fields exposed by the installed AOSharp client API.

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

For research, choose **Official AO — Read Only** in the standalone editor.
Verify the AO session you intend to observe, then select an NPC normally.
No ZoneEngine connection or GM credentials are needed. Use the target bridge
plugin for this mode. The separate NPC Inspect Probe sends a native Inspect
request and is outside this workflow.

The plugin publishes short-lived snapshots in
`%LOCALAPPDATA%\AORebirth\NpcEditor`. The existing `target-*.v1` publication
contains the identity hint used by AORebirth mode. The structured
`research-*.v2.json` publication contains client-exposed fields and bounded
changes observed for the selected NPC. AOSharp reads occur on the existing
update callback at most twice per second. A background timer publishes the
latest sample at most once per second. Publications expire in four seconds.

AORebirth target follow pauses when more than one client is reporting and
checks the spawn generation with the local server. Official mode offers
session selection and pins locks to the selected session and generation.
The bridge does not establish server endpoint provenance. See the
[capability discovery report](../AORebirth.NpcEditor/CAPABILITY_DISCOVERY.md)
for the installed API and observation boundaries.
