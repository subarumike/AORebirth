param(
    [Parameter(Mandatory = $true)][string]$AOSharpPath,
    [Parameter(Mandatory = $true)][string]$PluginPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([IntPtr]::Size -ne 4) { throw 'Run with SysWOW64 WindowsPowerShell (32-bit).' }
$runtime = (Resolve-Path -LiteralPath $AOSharpPath).Path
[void][Reflection.Assembly]::LoadFrom((Join-Path $runtime 'AOSharp.Common.dll'))
$core = [Reflection.Assembly]::LoadFrom((Join-Path $runtime 'AOSharp.Core.dll'))
$plugin = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $PluginPath).Path)
$contract = $core.GetType('AOSharp.Core.IAOPluginEntry', $true)
$entries = @($plugin.GetTypes() | Where-Object { $_.IsClass -and !$_.IsAbstract -and $contract.IsAssignableFrom($_) })
if ($entries.Count -ne 1 -or $entries[0].FullName -ne 'NpcInspectProbe.Main') { throw 'Expected exactly one AOSharp entry point.' }
if ($null -eq $entries[0].GetConstructor([Type[]]@())) { throw 'Missing public default constructor.' }
$entryBase = $core.GetType('AOSharp.Core.AOPluginEntry', $true)
if ($entries[0].BaseType -ne $entryBase) { throw 'Expected the installed AOPluginEntry initialization bridge.' }
$mapping = $entries[0].GetInterfaceMap($contract)
$initVerified = $false
for ($i = 0; $i -lt $mapping.InterfaceMethods.Length; $i++) {
    if ($mapping.InterfaceMethods[$i].Name -eq 'Init') {
        if ($mapping.TargetMethods[$i].DeclaringType -ne $entryBase) { throw 'Init must use the runtime base implementation.' }
        $initVerified = $true
    }
}
if (!$initVerified) { throw 'Installed runtime Init contract was not verified.' }
if ($plugin.GetName().Version.ToString() -ne '1.2.1.0') { throw 'Expected the Mike2022 port version 1.2.1.0.' }
Write-Host "PLUGIN_TYPE_LOAD=PASS entry=$($entries[0].FullName)"
Write-Host 'Metadata/type load only: no instance created, Run not called, no client or native invocation.'
