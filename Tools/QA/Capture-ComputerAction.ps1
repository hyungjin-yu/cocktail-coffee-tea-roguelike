param([Parameter(Mandatory=$true)][int]$GamePid, [Parameter(Mandatory=$true)][string]$Label,
      [ValidateSet('state','key','scan-key','click','drag')][string]$Action='state', [string]$Key='',
      [int]$X=0, [int]$Y=0, [int]$ToX=0, [int]$ToY=0,
      [string]$Evidence='C:/Users/user/Documents/Projects/CocktailCoffeeTea/Delivery/Reports/screenshots/rc-final')
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force $Evidence | Out-Null
$args=@('computer')
if($Action -eq 'scan-key') {
    & "$PSScriptRoot/Send-GameScanKey.ps1" -GamePid $GamePid -Key $Key | Set-Content "$Evidence/$Label-input.json" -Encoding UTF8
}
switch($Action) {
    'state' { $args += @('get-app-state') }
    'scan-key' { $args += @('get-app-state') }
    'key' { $args += @('press-key','--key',$Key) }
    'click' { $args += @('click','--x',"$X",'--y',"$Y") }
    'drag' { $args += @('drag','--from-x',"$X",'--from-y',"$Y",'--to-x',"$ToX",'--to-y',"$ToY") }
}
$args+=@('--app',"pid:$GamePid",'--restore-window','--json')
$raw=& orca @args
$raw | Set-Content "$Evidence/$Label.json" -Encoding UTF8
$r=$raw | ConvertFrom-Json
if(-not $r.ok) { throw "Orca computer failed: $raw" }
if($r.result.screenshot.path) { Copy-Item -LiteralPath $r.result.screenshot.path -Destination "$Evidence/$Label.png" }
[ordered]@{utc=(Get-Date).ToUniversalTime().ToString('o');action=$Action;key=$Key;command=('orca '+($args -join ' '));nativeInput=$(if($Action -eq 'scan-key'){"$Label-input.json"}else{$null});response="$Label.json";screenshot="$Label.png"} | ConvertTo-Json -Compress | Add-Content "$Evidence/actions.jsonl" -Encoding UTF8
$raw
