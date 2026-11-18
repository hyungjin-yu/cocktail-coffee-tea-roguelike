from pathlib import Path
import subprocess
r=Path('ArtSource/cafe-title-2026-11-18');p=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b','--script',str(r/'provenance/finish-scene.lua')],capture_output=True,text=True,timeout=300);(r/'verification/native-production.log').write_text(p.stdout+p.stderr,encoding='utf-8');print('Native scene production exit',p.returncode,p.stdout[-400:],p.stderr[-200:])
