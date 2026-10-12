param([Parameter(Mandatory=$true)][string]$Name, [Parameter(Mandatory=$true)][string]$Weapon,
      [string]$BuildPath='C:/UnityScratch/rc-final/pv', [int]$Runs=3, [string]$Extra='', [int]$TimeoutMin=45)
$ErrorActionPreference='Stop'
$root="C:/UnityScratch/pv/runs/$Name"
if(Test-Path -LiteralPath $root) { throw "Existing isolated run: $root" }
$exe=Join-Path $BuildPath 'CocktailCoffeeTea.exe'
if(-not(Test-Path -LiteralPath $exe)) { throw "LAUNCH FAILED: $exe" }
New-Item -ItemType Directory -Force "$root/data","$root/out" | Out-Null
$hash=(Get-FileHash "$BuildPath/CocktailCoffeeTea_Data/Managed/CCT.Runtime.dll").Hash
Set-Content "$root/launch.txt" "exe=$exe runtimeSHA256=$hash" -Encoding UTF8
$args="-batchmode -nographics -cct-data-dir `"$root/data`" -cct-playverify `"$root/out`" -cct-playverify-weapon $Weapon -cct-playverify-runs $Runs -logFile `"$root/player.log`" $Extra"
$proc=Start-Process -FilePath $exe -ArgumentList $args -PassThru -WindowStyle Hidden
if($null -eq $proc -or -not $proc.Id) { throw 'LAUNCH FAILED: empty PID' }
Set-Content "$root/pid.txt" $proc.Id
Add-Content "$root/launch.txt" "started=$(Get-Date -Format o) PID=$($proc.Id)"
if(-not $proc.WaitForExit($TimeoutMin*60000)) { Stop-Process -Id $proc.Id -Force; throw "TIMEOUT own PID=$($proc.Id)" }
Add-Content "$root/launch.txt" "ended=$(Get-Date -Format o) exit=$($proc.ExitCode)"
if($proc.ExitCode -ne 0) { throw "Player failed: $($proc.ExitCode)" }
if(-not(Test-Path "$root/out/summary.txt")) { throw 'Missing normal-play summary' }
