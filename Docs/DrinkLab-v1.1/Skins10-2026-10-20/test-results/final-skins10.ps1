$ErrorActionPreference = "Stop"
# 79 skins10 build: run ONLY after all 10 skins imported + final evidence passed. No interactive window (fresh launch is -batchmode -nographics).
$unity = "C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe"
$project = "C:\Users\user\Documents\Projects\CocktailCoffeeTea"
$res = "C:\UnityScratch\r79\results"
New-Item -ItemType Directory -Force $res | Out-Null
$userData = "$env:USERPROFILE\AppData\LocalLow\DefaultCompany\CocktailCoffeeTea"
$supply2 = @("$project\Builds\Playable-2026-10-20-drinklab-v1.1-supply2\CocktailCoffeeTea.exe", "$project\Builds\CocktailCoffeeTea-DrinkLab-v1.1-supply2-2026-10-20-win64.zip")
function PreserveList {
    $items = @()
    if (Test-Path $userData) { $items += Get-ChildItem $userData -File | Where-Object { $_.Name -notmatch '\.log$' } | ForEach-Object { $_.FullName } }
    $testData = "$project\Delivery\DrinkLab-v1.1\TestData"
    if (Test-Path $testData) { $items += Get-ChildItem $testData -Recurse -File | Where-Object { $_.Name -notmatch '\.log$' } | ForEach-Object { $_.FullName } }
    $items += $supply2
    foreach ($i in $items) { if (Test-Path $i) { "$((Get-FileHash $i -Algorithm SHA256).Hash)  $i" } else { "MISSING  $i" } }
}
PreserveList | Out-File "$res\preserve-before.txt" -Encoding utf8

$out = "$project\Builds\Playable-2026-10-20-drinklab-v1.1-skins10"
$p = Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -like "$out*" }; if ($p) { "BUILD EXE RUNNING - abort"; exit 1 }
if (Test-Path $out) { Remove-Item -LiteralPath $out -Recurse -Force -Confirm:$false }
$log = "$res\build_skins10.log"
$argLine = "-batchmode -nographics -quit -projectPath `"$project`" -executeMethod CCT.Editor.BuildRelease.Windows64DrinkLabMvpBatch -cct-preview-out `"$out`" -logFile `"$log`""
$proc = Start-Process -FilePath $unity -ArgumentList $argLine -PassThru -WindowStyle Hidden
$proc.WaitForExit()
"RESULT build_skins10 exit=$($proc.ExitCode) exe=$(Test-Path "$out\CocktailCoffeeTea.exe")"
if ($proc.ExitCode -ne 0 -or -not (Test-Path "$out\CocktailCoffeeTea.exe")) { "BUILD FAILED - stop"; exit 1 }
$zip = "$project\Builds\CocktailCoffeeTea-DrinkLab-v1.1-skins10-2026-10-20-win64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force -Confirm:$false }
$stage = "C:\UnityScratch\r79\zipstage\CocktailCoffeeTea-DrinkLab-v1.1"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force -Confirm:$false }
New-Item -ItemType Directory -Force $stage | Out-Null
robocopy $out $stage /E /XD *DoNotShip* | Out-Null
Compress-Archive -Path "$stage\*" -DestinationPath $zip
$h = (Get-FileHash $zip -Algorithm SHA256).Hash
"$h  $(Split-Path $zip -Leaf)" | Out-File "$zip.sha256" -Encoding ascii
"ZIP $zip $h $((Get-Item $zip).Length)"
"EXE " + (Get-FileHash "$out\CocktailCoffeeTea.exe").Hash
# 79 skin resources in build: count CosmeticSkins strings in resources.assets is not reliable; count imported source PNGs instead.
"SOURCE skin folders: " + ((Get-ChildItem "$project\Assets\_Project\Resources\CosmeticSkins" -Directory | ForEach-Object { "$($_.Name)=$((Get-ChildItem $_.FullName -Filter *.png).Count)" }) -join ", ")

$fresh = "C:\UnityScratch\r79\fresh-skins10"
if (Test-Path $fresh) { Remove-Item $fresh -Recurse -Force -Confirm:$false }
Expand-Archive $zip -DestinationPath $fresh
$data = "C:\UnityScratch\r79\fresh-skins10-data"
if (Test-Path $data) { Remove-Item $data -Recurse -Force -Confirm:$false }
New-Item -ItemType Directory -Force $data | Out-Null
$plog = "C:\UnityScratch\r79\fresh-skins10-player.log"
$gp = Start-Process -FilePath "$fresh\CocktailCoffeeTea.exe" -ArgumentList "-batchmode -nographics -cct-data-dir `"$data`" -logFile `"$plog`"" -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 30
$alive = -not $gp.HasExited
"FRESH pid=$($gp.Id) aliveAfter30s=$alive"
if ($alive) { Stop-Process -Id $gp.Id -Force }
"exceptions=" + (Select-String -Path $plog -Pattern "Exception" | Measure-Object).Count
"commerce lines: " + ((Select-String -Path $plog -Pattern "\[Commerce\]" | ForEach-Object { $_.Line }) -join " | ")
Get-ChildItem $data -Recurse | Select-Object FullName, Length | Format-Table -AutoSize | Out-String

PreserveList | Out-File "$res\preserve-after.txt" -Encoding utf8
$diff = Compare-Object (Get-Content "$res\preserve-before.txt") (Get-Content "$res\preserve-after.txt")
"PRESERVE changed=" + ($diff | Measure-Object).Count
