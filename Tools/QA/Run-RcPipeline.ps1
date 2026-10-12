param([string]$Steps = 'edit,play,release,pv,load,tour', [string]$Project = 'C:/UnityScratch/CCT', [string]$Evidence = 'C:/UnityScratch/rc-final', [string]$PlayFilter = '')
$ErrorActionPreference = 'Stop'
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe'
New-Item -ItemType Directory -Force $Evidence | Out-Null
foreach ($step in ($Steps -split ',')) {
    $log = Join-Path $Evidence "$step.log"
    $arguments = "-batchmode -projectPath `"$Project`" -logFile `"$log`""
    if ($step -in @('edit','play')) {
        $platform = if ($step -eq 'edit') {'EditMode'} else {'PlayMode'}
        $xml = Join-Path $Evidence "$step.xml"
        $arguments += " -runTests -testPlatform $platform -testResults `"$xml`" -cctTestProgress `"$Evidence/$step-progress.txt`""
        if ($step -eq 'play' -and $PlayFilter) {
            $arguments += " -testFilter `"$PlayFilter`""
            Add-Content "$Evidence/pipeline.log" "PlayMode FILTERED: $PlayFilter"
        }
    } else {
        $method = switch ($step) {
            'integrate' {'CCT.Editor.FirstPassArtIntegrator.IntegrateBatch'}
            'permanent' {'CCT.Editor.PermanentSkillsCreator.CreateBatch'}
            'release' {'CCT.Editor.BuildRelease.Windows64'}
            'pv' {'CCT.Editor.BuildRelease.Windows64ReleasePlayVerifyBatch'}
            'load' {'CCT.Editor.BuildRelease.Windows64ReleaseLoadTestBatch'}
            'tour' {'CCT.Editor.BuildRelease.Windows64ReleaseQaTourBatch'}
            'preview' {'CCT.Editor.BuildRelease.Windows64FirstPassPreviewBatch'}
            'qa-tour' {'CCT.Editor.BuildRelease.Windows64QaTourBatch'}
            'qa-pv' {'CCT.Editor.BuildRelease.Windows64PlayVerifyBatch'}
            'qa-load' {'CCT.Editor.BuildRelease.Windows64LoadTestBatch'}
            default {throw "Unknown step: $step"}
        }
        $arguments += " -quit -executeMethod $method"
        if ($step -in @('pv','load','tour','preview','qa-tour','qa-pv','qa-load')) { $arguments += " -cct-preview-out `"$Evidence/$step`"" }
    }
    $proc = Start-Process -FilePath $unity -ArgumentList $arguments -PassThru -WindowStyle Hidden
    if ($null -eq $proc -or -not $proc.Id) { throw "LAUNCH FAILED $step" }
    Add-Content "$Evidence/pipeline.log" "$(Get-Date -Format o) $step PID=$($proc.Id)"
    if (-not $proc.WaitForExit(40 * 60000)) { Stop-Process -Id $proc.Id -Force; throw "TIMEOUT own process $step" }
    Add-Content "$Evidence/pipeline.log" "$(Get-Date -Format o) $step exit=$($proc.ExitCode)"
    if ($proc.ExitCode -ne 0) { throw "$step failed; inspect $log" }
    if ($step -in @('edit','play')) {
        if (-not (Test-Path -LiteralPath $xml)) { throw "NO XML $step" }
        [xml]$result = Get-Content -LiteralPath $xml -Raw
        $summary = $result.'test-run'
        if ([int]$summary.total -eq 0 -or [int]$summary.failed -ne 0 -or [int]$summary.skipped -ne 0 -or $summary.result -ne 'Passed') {
            throw "TEST FAILED ${step}: total=$($summary.total) failed=$($summary.failed) skipped=$($summary.skipped)"
        }
        Add-Content "$Evidence/pipeline.log" "$step pass=$($summary.passed) total=$($summary.total)"
    }
}
Add-Content "$Evidence/pipeline.log" "$(Get-Date -Format o) DONE $Steps"
