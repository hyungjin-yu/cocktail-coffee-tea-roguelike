import pathlib,zipfile,hashlib,json,os
root=pathlib.Path.cwd(); ext=pathlib.Path(os.environ['APPDATA'])/'Aseprite/extensions/pixellab'; archive=pathlib.Path('C:/Users/user/Downloads/PixelLab (2).aseprite-extension'); checked=[]
with zipfile.ZipFile(archive) as z:
 for n in z.namelist():
  if n.endswith('.lua'):
   target=ext/pathlib.Path(*pathlib.PurePosixPath(n).parts[1:])
   if not target.exists(): target=ext/pathlib.PurePosixPath(n).name
   checked.append({'file':n,'installedExists':target.exists(),'exactOfficialBytes':target.exists() and target.read_bytes()==z.read(n)})
record={'officialArchive':str(archive),'size':archive.stat().st_size,'sha256':hashlib.sha256(archive.read_bytes()).hexdigest(),'installation':'Aseprite Preferences > Extensions > Add Extension, native confirm 0.4.130 -> 0.4.131, accepted, editor restarted','versionReadFrom':'native installer dialog, not package.json','secretsRead':False,'officialLuaMatches':checked,'nativeGenerationProbe':{'asset':'dl_coffee_latte_glass','canvas':[32,32],'camera':'sidescroller (eye level)','direction':'none','removeBackground':True,'prompt':'One empty tall latte drinking glass, straight tapered sides, thick flat base, no handle, transparent interior and background, simple chunky cafe inventory pixel art, dark brown outline 201D29, ivory and muted silver edge highlights, three tones per material, side view with only a slight oval rim, full object centered, no liquid, no text, no floor, no shadow, no glow, no smooth antialiasing.','status':'native_generation_success_seed_4169501354'}}
p=root/'ArtSource/feedback-art-2026-11-16/provenance/official-install-and-probe.json';p.write_text(json.dumps(record,indent=2),encoding='utf-8');print({'luaFilesCompared':len(checked),'exactMatches':sum(x['exactOfficialBytes'] for x in checked),'evidence':str(p.relative_to(root))})
