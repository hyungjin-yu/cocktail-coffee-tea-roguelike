# G35 고정 부하 측정 실행(LoadScenario, CCT_QA_LOAD 빌드). -BuildPath는 빌드 폴더 절대 경로(권장), -Build는 C:\UnityScratch\lt 아래 이름.
# 실행 파일이 없거나 프로세스 시작에 실패하면 즉시 exit 3으로 끝낸다(빈 측정을 기다리지 않는다).
param([string]$Name, [string]$Build='build2', [string]$BuildPath='', [int]$Restarts=10, [int]$TimeoutMin = 12)
$ErrorActionPreference = 'Stop'
$root = "C:\UnityScratch\lt\runs\$Name"
if (Test-Path $root) { "run folder exists"; exit 2 }
if ($BuildPath) { $dir = [IO.Path]::GetFullPath($BuildPath) } else { $dir = Join-Path 'C:\UnityScratch\lt' $Build }
$exe = Join-Path $dir 'CocktailCoffeeTea.exe'
if (-not (Test-Path -LiteralPath $exe)) { "LAUNCH FAILED: executable not found: $exe"; exit 3 }
New-Item -ItemType Directory -Force "$root\data","$root\out" | Out-Null
"exe=$exe sha256(runtime)=$((Get-FileHash (Join-Path $dir 'CocktailCoffeeTea_Data\Managed\CCT.Runtime.dll')).Hash)" | Tee-Object "$root\launch.txt"
$a = "-screen-width 1920 -screen-height 1080 -screen-fullscreen 1 -cct-data-dir `"$root\data`" -cct-loadtest `"$root\out`" -cct-loadtest-restarts $Restarts -logFile `"$root\player.log`""
try { $p = Start-Process -FilePath $exe -ArgumentList $a -PassThru } catch { "LAUNCH FAILED: $($_.Exception.Message)"; exit 3 }
if ($null -eq $p -or -not $p.Id) { "LAUNCH FAILED: no process"; exit 3 }
"$Name PID=$($p.Id) start=$(Get-Date -Format HH:mm:ss)"
$csv = "$root\ext-memory.csv"; "utc,working_set_mb,private_mb,cpu_s" | Set-Content $csv -Encoding ascii
$deadline = (Get-Date).AddMinutes($TimeoutMin)
while (-not $p.HasExited -and (Get-Date) -lt $deadline) {
  try { $p.Refresh(); "{0:o},{1:0.0},{2:0.0},{3:0.00}" -f (Get-Date).ToUniversalTime(), ($p.WorkingSet64/1MB), ($p.PrivateMemorySize64/1MB), $p.TotalProcessorTime.TotalSeconds | Add-Content $csv -Encoding ascii } catch {}
  Start-Sleep -Milliseconds 1000
}
if (-not $p.HasExited) { "$Name TIMEOUT, killing own PID=$($p.Id)"; Stop-Process -Id $p.Id -Force -Confirm:$false; exit 4 }
$p.WaitForExit(); "$Name exit=$($p.ExitCode) end=$(Get-Date -Format HH:mm:ss)"
if (-not (Test-Path "$root\out\load-summary.txt")) { "NO SUMMARY: measurement did not complete"; exit 5 }
