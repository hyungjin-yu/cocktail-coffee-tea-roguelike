# 흰(또는 근접 흰색) 배경 PNG를 투명하게 만드는 도구.
# 단순 임계값이 아니라 "이미지 테두리에서부터 플러드필"로 배경을 찾는다.
# → 배경에 비네팅/노이즈가 있어도 안정적이고, 검은 테두리로 둘러싸인 캐릭터 내부의
#   흰색/회색 디테일(치아, 금속 하이라이트 등)은 배경과 연결되지 않으므로 보존된다.
#
# 사용 예:
#   . .\Tools\Remove-WhiteBackground.ps1
#   Remove-WhiteBackgroundFlood -srcPath "원본.png" -dstPath "결과.png"
#
# 처리 후에는 반드시 결과물을 검증할 것 (Read 도구의 체커보드 미리보기는 실제 투명 여부를
# 보장하지 않는다 — System.Drawing 으로 코너 픽셀 알파값을 직접 찍어보거나,
# 아래 Export-AlphaVisualization 으로 알파 채널 자체를 이미지로 뽑아서 눈으로 확인한다).

Add-Type -AssemblyName System.Drawing

function Remove-WhiteBackgroundFlood {
    param(
        [Parameter(Mandatory)] [string]$srcPath,
        [Parameter(Mandatory)] [string]$dstPath,
        [int]$satMax = 26,      # 이 값 이하면 "무채색(회색/흰색)"으로 간주
        [int]$brightMin = 195   # 이 값 이상이면 "충분히 밝음"으로 간주 (비네팅 감안해 넉넉하게)
    )

    $orig = [System.Drawing.Bitmap]::FromFile($srcPath)
    $w = $orig.Width; $h = $orig.Height
    $wMinus1 = $w - 1
    $hMinus1 = $h - 1
    $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $bmp = $orig.Clone($rect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $orig.Dispose()

    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $data.Stride
    $byteCount = $stride * $h
    $buf = New-Object byte[] $byteCount
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buf, 0, $byteCount)

    $n = $w * $h
    $isBg = New-Object bool[] $n
    $visited = New-Object bool[] $n
    $queue = New-Object int[] $n
    $qHead = 0; $qTail = 0

    function Test-Bgish {
        param($idx)
        $off = $idx * 4
        $bb = $buf[$off]; $gg = $buf[$off + 1]; $rr = $buf[$off + 2]
        $mx = [Math]::Max($rr, [Math]::Max($gg, $bb)); $mn = [Math]::Min($rr, [Math]::Min($gg, $bb))
        return (($mx - $mn) -le $satMax) -and ($mn -ge $brightMin)
    }

    for ($x = 0; $x -lt $w; $x++) {
        foreach ($idx in @($x, ($hMinus1 * $w + $x))) {
            if (-not $visited[$idx] -and (Test-Bgish $idx)) {
                $visited[$idx] = $true; $isBg[$idx] = $true
                $queue[$qTail] = $idx; $qTail++
            }
        }
    }
    for ($y = 0; $y -lt $h; $y++) {
        foreach ($idx in @(($y * $w), ($y * $w + $wMinus1))) {
            if (-not $visited[$idx] -and (Test-Bgish $idx)) {
                $visited[$idx] = $true; $isBg[$idx] = $true
                $queue[$qTail] = $idx; $qTail++
            }
        }
    }

    while ($qHead -lt $qTail) {
        $idx = $queue[$qHead]; $qHead++
        $x = $idx % $w; $y = [int][Math]::Floor($idx / $w)

        if ($x -gt 0) {
            $nIdx = $idx - 1
            if (-not $visited[$nIdx]) { $visited[$nIdx] = $true; if (Test-Bgish $nIdx) { $isBg[$nIdx] = $true; $queue[$qTail] = $nIdx; $qTail++ } }
        }
        if ($x -lt $wMinus1) {
            $nIdx = $idx + 1
            if (-not $visited[$nIdx]) { $visited[$nIdx] = $true; if (Test-Bgish $nIdx) { $isBg[$nIdx] = $true; $queue[$qTail] = $nIdx; $qTail++ } }
        }
        if ($y -gt 0) {
            $nIdx = $idx - $w
            if (-not $visited[$nIdx]) { $visited[$nIdx] = $true; if (Test-Bgish $nIdx) { $isBg[$nIdx] = $true; $queue[$qTail] = $nIdx; $qTail++ } }
        }
        if ($y -lt $hMinus1) {
            $nIdx = $idx + $w
            if (-not $visited[$nIdx]) { $visited[$nIdx] = $true; if (Test-Bgish $nIdx) { $isBg[$nIdx] = $true; $queue[$qTail] = $nIdx; $qTail++ } }
        }
    }

    $bgCount = 0
    for ($idx = 0; $idx -lt $n; $idx++) {
        if ($isBg[$idx]) { $buf[$idx * 4 + 3] = 0; $bgCount++ }
    }

    [System.Runtime.InteropServices.Marshal]::Copy($buf, 0, $data.Scan0, $byteCount)
    $bmp.UnlockBits($data)
    $bmp.Save($dstPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return $bgCount
}

# 결과 검증용: 알파 채널을 흑백 이미지로 뽑아서 Read 도구로 눈으로 확인할 수 있게 한다.
# (흰=불투명, 검=투명. 노이즈/체커 무늬가 보이면 배경 제거가 잘못된 것.)
function Export-AlphaVisualization {
    param([Parameter(Mandatory)] [string]$srcPath, [Parameter(Mandatory)] [string]$dstPath)

    $src = [System.Drawing.Bitmap]::FromFile($srcPath)
    $rect = New-Object System.Drawing.Rectangle(0, 0, $src.Width, $src.Height)
    $data = $src.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $byteCount = $data.Stride * $src.Height
    $buf = New-Object byte[] $byteCount
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buf, 0, $byteCount)
    $src.UnlockBits($data)

    $viz = New-Object System.Drawing.Bitmap $src.Width, $src.Height
    $vdata = $viz.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $vbuf = New-Object byte[] $byteCount
    for ($i = 0; $i -lt $byteCount; $i += 4) {
        $a = $buf[$i + 3]
        $vbuf[$i] = $a; $vbuf[$i + 1] = $a; $vbuf[$i + 2] = $a; $vbuf[$i + 3] = 255
    }
    [System.Runtime.InteropServices.Marshal]::Copy($vbuf, 0, $vdata.Scan0, $byteCount)
    $viz.UnlockBits($vdata)
    $viz.Save($dstPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $src.Dispose(); $viz.Dispose()
}
