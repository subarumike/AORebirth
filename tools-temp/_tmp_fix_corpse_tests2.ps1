$path = 'AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\PlayfieldLifecycleTraceTests.cs'
$t = Get-Content -Raw $path
$t = $t.Replace('EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30)', 'EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.Zero')
$t = $t.Replace('EmptyCorpseLifetime = TimeSpan.FromSeconds(30)', 'EmptyCorpseLifetime = TimeSpan.Zero')
$t = $t.Replace('RegularLootCorpseLifetime = TimeSpan.FromMinutes(2)', 'RegularLootCorpseLifetime = TimeSpan.FromSeconds(60)')
$t = $t.Replace('public static readonly TimeSpan EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.FromSeconds(30);', 'public static readonly TimeSpan EmptyCorpseCleanupAfterOpenedDelay = TimeSpan.Zero;')
$t = $t.Replace('public static readonly TimeSpan EmptyCorpseLifetime = TimeSpan.FromSeconds(30);', 'public static readonly TimeSpan EmptyCorpseLifetime = TimeSpan.Zero;')
$t = $t.Replace('public static readonly TimeSpan RegularLootCorpseLifetime = TimeSpan.FromMinutes(2);', 'public static readonly TimeSpan RegularLootCorpseLifetime = TimeSpan.FromSeconds(60);')
$t = $t.Replace('Regular loot-bearing corpses must retain two minutes, while every born-empty or fully emptied corpse uses exactly 30 seconds.', 'Regular loot-bearing corpses must retain 60 seconds, while every born-empty or fully emptied corpse despawns immediately.')
Set-Content -Path $path -Value $t -NoNewline
Write-Output 'done'
