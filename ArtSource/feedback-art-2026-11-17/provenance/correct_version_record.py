from pathlib import Path
import json,os
r=Path('ArtSource/feedback-art-2026-11-17');p=r/'provenance/tool-access-current.json';d=json.loads(p.read_text(encoding='utf-8'));d.update({'priorDialogActualVersion':'0.4.131','priorReportVersionError':'The worker misread the pixel-font digit 1 as 7 in the 2026-11-16 installer dialog; it actually says upgrade 0.4.130 -> 0.4.131. Prior 0.4.137 provenance text was incorrect.','currentExePath':'C:/Program Files/Aseprite/Aseprite.exe','currentAsepriteVersion':'1.3.18.6','installedExtensionPath':str(Path(os.environ['APPDATA'])/'Aseprite/extensions/pixellab'),'evidenceCrop':'provenance/prior-version-number.png','sourceComparisonFinding':'116/116 Lua files exactly equal the same official (2) download; no source evidence of a downgrade or different executable.','reinstallPerformedThisTask':False,'needsCoordinatorVersionContractCorrection':True});p.write_text(json.dumps(d,indent=2),encoding='utf-8')
p=r/'TOOL-GATE.md';s=p.read_text(encoding='utf-8-sig');s+='''

## Corrected cause, before any reinstall

The prior preserved installer screenshot was enlarged without altering the original: `provenance/prior-version-number.png` clearly reads **v0.4.131**, not v0.4.137. This worker misread the pixel-font final digit in the 2026-11-16 report. Current menu is also 0.4.131 and all 116 installed Lua files match that very same official (2) archive. Current executable is C:/Program Files/Aseprite/Aseprite.exe v1.3.18.6; extension is APPDATA/Aseprite/extensions/pixellab. There is no observed source change/downgrade to explain; the previous version claim was wrong.

Coordinator authorized one reinstall of the known official file believing it was 0.4.137. It now appears to be the already installed official 0.4.131, so no redundant reinstall was performed. Need corrected version contract (use the verified official file/current 0.4.131 and test actual generation, or obtain an actually different official version). Do not spoof any version or credentials.

Original 36 PNG/native artifacts remain unchanged. Their old provenance strings saying 0.4.137 are inaccurate metadata; this correction does not invalidate the preserved actual Aseprite/official PixelLab generation and pixel checks, but the version label needs an erratum. Do not silently rewrite the old delivery's metadata without coordinator direction.
''';p.write_text(s,encoding='utf-8')
