# G30: force-kill a real player mid-run (TerminateProcess), then relaunch the shipping build twice: abandon counted once, checkpoint kept, no run resume.
param([string]$Root = 'C:\UnityScratch\g30\run1', [string]$PvExe = 'C:\UnityScratch\pv\build6\CocktailCoffeeTea.exe',
      [string]$ReleaseExe = 'C:\Users\user\Documents\Projects\CocktailCoffeeTea\Builds\FirstPassPreview-r13\CocktailCoffeeTea.exe')
$ErrorActionPreference = 'Stop'
foreach ($e in $PvExe, $ReleaseExe) { if (-not (Test-Path -LiteralPath $e)) { "LAUNCH FAILED: missing $e"; exit 3 } }
$root = $Root
if (Test-Path $root) { 'exists'; exit 2 }
New-Item -ItemType Directory -Force "$root\data","$root\pv-out","$root\snap" | Out-Null
$save = "$root\data\save.json"
$log = "$root\g30-log.txt"
function L($t) { $line = "{0:o} {1}" -f (Get-Date).ToUniversalTime(), $t; $line; Add-Content $log $line -Encoding utf8 }
function Snap($name) { if (Test-Path $save) { Copy-Item $save "$root\snap\$name.json"; $h=(Get-FileHash $save -Algorithm SHA256).Hash; L "snap $name sha256=$h" } else { L "snap ${name} - no save.json" } }

# 1) 寃利?鍮뚮뱶濡????쒖옉(?낅젰 ??? ?④?) ??activeRun + 泥댄겕?ъ씤??諛섏쁺 ?뺤씤 ??TerminateProcess
$pv = $PvExe
$a = "-batchmode -nographics -cct-data-dir `"$root\data`" -cct-playverify `"$root\pv-out`" -cct-playverify-weapon gun -cct-playverify-runs 1 -logFile `"$root\launch1-player.log`""
$p = Start-Process -FilePath $pv -ArgumentList $a -PassThru -WindowStyle Hidden
L "launch1 PlayVerifier $PvExe PID=$($p.Id)"
$ok = $false
for ($i = 0; $i -lt 240 -and -not $p.HasExited; $i++) {
  Start-Sleep -Milliseconds 500
  try { $j = Get-Content $save -Raw -Encoding utf8 | ConvertFrom-Json } catch { continue }
  if ($j.activeRun -ne $null -and $j.activeRun.checkpointGameSeconds -ge 20 -and ($j.cocktailIngredientsCollected + $j.teaLeavesCollected) -gt 0) { $ok = $true; break }
}
if (-not $ok) { L "activeRun checkpoint not observed (exited=$($p.HasExited))"; if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }; exit 3 }
Snap 'before-kill'
Stop-Process -Id $p.Id -Force -Confirm:$false
$p.WaitForExit()
L "launch1 force-killed (TerminateProcess) exit=$($p.ExitCode)"
Snap 'after-kill'

# 2) ?쇰컲 r13 ?꾨━酉곕줈 ?ъ떎???④?) ??濡쒕뱶 ??以묐떒 ??蹂듦뎄 ??媛뺤젣 醫낅즺(??댄?, activeRun ?놁쓬)
$r13 = $ReleaseExe
foreach ($n in 2, 3) {
  $a = "-batchmode -nographics -cct-data-dir `"$root\data`" -logFile `"$root\launch$n-player.log`""
  $q = Start-Process -FilePath $r13 -ArgumentList $a -PassThru -WindowStyle Hidden
  L "launch$n $ReleaseExe PID=$($q.Id)"
  for ($i = 0; $i -lt 60 -and -not $q.HasExited; $i++) {
    Start-Sleep -Milliseconds 500
    if ((Test-Path "$root\launch$n-player.log") -and (Select-String -Path "$root\launch$n-player.log" -Pattern '\[Save\] save.json' -Quiet)) { break }
  }
  Start-Sleep -Seconds 4
  Stop-Process -Id $q.Id -Force -Confirm:$false; $q.WaitForExit()
  L "launch$n force-killed at title"
  Snap "after-launch$n"
}
L 'done'

