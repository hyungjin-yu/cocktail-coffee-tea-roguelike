from pathlib import Path
import subprocess
r=Path('ArtSource/cafe-title-2026-11-18');p=r/'provenance/author-scene.lua';s=p.read_text(encoding='utf-8-sig');p.write_text(s,encoding='utf-8');res=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b','--script',str(p)],capture_output=True,text=True,timeout=180);(r/'verification/native-author-draft.log').write_text(res.stdout+res.stderr,encoding='utf-8');print('Native scene draft exit',res.returncode,res.stdout[-600:],res.stderr[-300:])
