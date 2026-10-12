param([string]$Apk = '')
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$adb = 'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe'
if (-not (Test-Path -LiteralPath $adb)) { throw 'Unity Android platform-tools/adb is missing.' }
if (-not $Apk) { $Apk = Join-Path $project 'Builds/Android/CocktailCoffeeTea-1.0.0-debugsigned-sideload.apk' }
if (-not (Test-Path -LiteralPath $Apk)) { throw "Test APK not found: $Apk" }
$apkPath = (Resolve-Path -LiteralPath $Apk).Path
$devices = @(& $adb devices | Where-Object { $_ -match '^\S+\s+device$' })
if ($devices.Count -ne 1) { throw 'Connect exactly one Android phone with USB debugging authorized, then try again.' }
$serial = ($devices[0] -split '\s+')[0]
& $adb -s $serial install -r $apkPath
if ($LASTEXITCODE -ne 0) { throw 'Installation failed. Existing app/data were not uninstalled. Check the adb error.' }
Write-Output 'Installed test APK. Open CocktailCoffeeTea on the phone.'
