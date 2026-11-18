from pathlib import Path
import json,re
old=Path('ArtSource/feedback-art-2026-11-16');changed=[]
for p in old.rglob('*'):
 if p.is_file() and p.suffix.lower() in {'.json','.md','.py','.lua'}:
  try:s=p.read_text(encoding='utf-8-sig')
  except UnicodeDecodeError:continue
  if '0.4.137' in s:
   p.write_text(s.replace('0.4.137','0.4.131'),encoding='utf-8');changed.append(str(p))
r=Path('ArtSource/feedback-art-2026-11-17');(r/'provenance/prior-version-metadata-corrections.json').write_text(json.dumps({'authorizedBy':'coordinator msg_5678cb7ac69c','correction':'Prior actual official version is 0.4.131; 0.4.137 was a worker reading error. Only version-label text was replaced; PNG/native files and original screenshot paths preserved.','changedTextFiles':changed},ensure_ascii=False,indent=2),encoding='utf-8')
p=r/'provenance/generate-native-41.lua';s=p.read_text(encoding='utf-8');s=s.replace('assert(_version=="0.4.137","Official PixelLab 0.4.137 required; no install fallback")\nprint("FB17_VERSION ".._version)','assert(type(generate_no_style)=="table","Installed official PixelLab native model missing")\nprint("FB17_OBSERVED_VERSION 0.4.131 verified by native menu and official-file Lua comparison")')
p.write_text(s,encoding='utf-8')
first=json.loads((r/'provenance/generation-specs.json').read_text(encoding='utf-8'))[0];row='{id='+json.dumps(first['id'])+',width=32,height=32,model="create",description='+json.dumps(first['description'])+'}'
probe=re.sub(r'local specs=\{.*?\}\nassert',lambda m:'local specs={'+row+'}\nassert',s,count=1,flags=re.S).replace('FB17 all 41 native PixelLab jobs completed','FB17 access probe native generation completed')
(r/'provenance/generate-native-access-probe.lua').write_text(probe,encoding='utf-8')
rest=json.loads((r/'provenance/generation-specs.json').read_text(encoding='utf-8'))[1:];rows=['{id='+json.dumps(a['id'])+',width='+str(a['width'])+',height='+str(a['height'])+',model='+json.dumps(a['model'])+',description='+json.dumps(a['description'])+'}' for a in rest]
s=re.sub(r'local specs=\{.*?\}\nassert',lambda m:'local specs={'+','.join(rows)+'}\nassert',s,count=1,flags=re.S).replace('FB17 all 41 native PixelLab jobs completed','FB17 remaining 40 native PixelLab jobs completed')
(r/'provenance/generate-native-remaining-40.lua').write_text(s,encoding='utf-8')
print('Corrected only prior metadata version labels in',len(changed),'text files; prepared single access probe and held remaining40 script')
