param(
    [string]$BuiltDir = "C:\Users\Mike\Documents\Cellao-Clean\CellAO\Built\Debug",
    [int]$UniqueItemId = 283743,
    [int]$NonUniqueItemId = 27350
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BuiltDir)) {
    throw "Built directory not found: $BuiltDir"
}

Set-Location $BuiltDir

$assemblies = @(
    "CellAO.Enums.dll",
    "CellAO.Interfaces.dll",
    "SmokeLounge.AOtomation.Messaging.dll",
    "locales.dll",
    "MsgPack.dll",
    "Utility.dll",
    "CellAO.Core.dll"
)

foreach ($assembly in $assemblies) {
    $path = Join-Path $BuiltDir $assembly
    if (-not (Test-Path $path)) {
        throw "Required assembly not found: $path"
    }

    [Reflection.Assembly]::LoadFrom($path) | Out-Null
}

Add-Type -ReferencedAssemblies `
    (Join-Path $BuiltDir "CellAO.Core.dll"), `
    (Join-Path $BuiltDir "CellAO.Interfaces.dll"), `
    (Join-Path $BuiltDir "SmokeLounge.AOtomation.Messaging.dll") `
    -TypeDefinition @'
using CellAO.Core.Inventory;
using CellAO.Interfaces;
using SmokeLounge.AOtomation.Messaging.GameData;

public class UniqueSmokeContainer : IItemContainer
{
    public UniqueSmokeContainer(int id)
    {
        this.Identity = new Identity { Type = IdentityType.CanbeAffected, Instance = id };
        this.Parent = new Identity { Type = IdentityType.None, Instance = 0 };
    }

    public IInventoryPages BaseInventory { get; set; }
    public Identity Identity { get; private set; }
    public Identity Parent { get; private set; }
    public bool Read() { return true; }
    public bool Write() { return true; }
}
'@

$itemsDat = Join-Path $BuiltDir "items.dat"
if (-not (Test-Path $itemsDat)) {
    throw "items.dat not found: $itemsDat"
}

[CellAO.Core.Items.ItemLoader]::ItemList.Clear()
[CellAO.Core.Items.ItemLoader]::CacheAllItems($itemsDat) | Out-Null

$uniqueTemplate = [CellAO.Core.Items.ItemLoader]::ItemList[$UniqueItemId]
if (-not $uniqueTemplate -or -not $uniqueTemplate.IsUnique()) {
    throw "Smoke unique item id $UniqueItemId is not flagged Unique."
}

$nonUniqueTemplate = [CellAO.Core.Items.ItemLoader]::ItemList[$NonUniqueItemId]
if (-not $nonUniqueTemplate -or $nonUniqueTemplate.IsUnique()) {
    throw "Smoke non-unique item id $NonUniqueItemId is flagged Unique or missing."
}

$owner = New-Object UniqueSmokeContainer(999999)
$inventory = New-Object CellAO.Core.Inventory.PlayerInventory($owner)
$owner.BaseInventory = $inventory

$uniqueA = New-Object CellAO.Core.Items.Item(1, $UniqueItemId, $UniqueItemId)
$uniqueB = New-Object CellAO.Core.Items.Item(1, $UniqueItemId, $UniqueItemId)
$nonUniqueA = New-Object CellAO.Core.Items.Item(1, $NonUniqueItemId, $NonUniqueItemId)
$nonUniqueB = New-Object CellAO.Core.Items.Item(1, $NonUniqueItemId, $NonUniqueItemId)

$r1 = $owner.BaseInventory.TryAdd($uniqueA)
$r2 = $owner.BaseInventory.TryAdd($uniqueB)
$r3 = $owner.BaseInventory.TryAdd($nonUniqueA)
$r4 = $owner.BaseInventory.TryAdd($nonUniqueB)

if ($r1.ToString() -ne "OK") {
    throw "First unique add should be OK, got $r1."
}

if ($r2.ToString() -ne "HaveUniqueAlready") {
    throw "Second unique add should be HaveUniqueAlready, got $r2."
}

if ($r3.ToString() -ne "OK" -or $r4.ToString() -ne "OK") {
    throw "Non-unique duplicate adds should be OK, got $r3 and $r4."
}

$uniqueC = New-Object CellAO.Core.Items.Item(1, $UniqueItemId, $UniqueItemId)
$explicitSlotResult = $owner.BaseInventory.AddToPage(
    $owner.BaseInventory.StandardPage,
    0x50,
    $uniqueC)

if ($explicitSlotResult.ToString() -ne "HaveUniqueAlready") {
    throw "Explicit AddToPage duplicate unique should be HaveUniqueAlready, got $explicitSlotResult."
}

$count = $owner.BaseInventory[$owner.BaseInventory.StandardPage].List().Count
if ($count -ne 3) {
    throw "Expected 3 inventory items after smoke, got $count."
}

Write-Output "RESULT PASS unique1=$r1 unique2=$r2 nonunique1=$r3 nonunique2=$r4 explicitDuplicate=$explicitSlotResult count=$count"
