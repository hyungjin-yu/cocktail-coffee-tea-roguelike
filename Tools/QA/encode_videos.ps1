# QA: AutoTour video/<clip>/*.jpg(30fps 게임시간 프레임)를 mp4로 묶는다. 모션 클립은 플레이어 중심 2배 확대(nearest)본도 만든다.
param([string]$OutDir, [string]$Dest)
New-Item -ItemType Directory -Force $Dest | Out-Null
Get-ChildItem (Join-Path $OutDir 'video') -Directory | ForEach-Object {
  $clip = $_.Name; $src = Join-Path $_.FullName '%05d.jpg'
  & ffmpeg -hide_banner -loglevel error -y -framerate 30 -i $src -c:v libx264 -pix_fmt yuv420p -crf 18 (Join-Path $Dest "$clip.mp4")
  Copy-Item (Join-Path $_.FullName 'frames.csv') (Join-Path $Dest "$clip.frames.csv")
  if ($clip -like 'motion_*') {
    $rows = Import-Csv (Join-Path $_.FullName 'frames.csv')
    $first = Get-Item (Join-Path $_.FullName '00000.jpg'); Add-Type -AssemblyName System.Drawing
    $img = [System.Drawing.Image]::FromFile($first.FullName); $W = $img.Width; $H = $img.Height; $img.Dispose()
    $sx = ($rows | Measure-Object -Property screenX -Average).Average; $sy = ($rows | Measure-Object -Property screenY -Average).Average
    $cw = [int]($W * 0.3); $ch = [int]($H * 0.45)
    $x = [int][Math]::Max(0, [Math]::Min($W - $cw, $sx - $cw / 2)); $y = [int][Math]::Max(0, [Math]::Min($H - $ch, ($H - $sy) - $ch * 0.6))
    & ffmpeg -hide_banner -loglevel error -y -framerate 30 -i $src -vf "crop=${cw}:${ch}:${x}:${y},scale=iw*2:ih*2:flags=neighbor" -c:v libx264 -pix_fmt yuv420p -crf 16 (Join-Path $Dest "$clip-zoom2x.mp4")
  }
  "$clip encoded"
}
