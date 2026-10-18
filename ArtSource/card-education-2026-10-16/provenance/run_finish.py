import subprocess,pathlib
p=pathlib.Path(__file__).resolve().parent
out='C:/Users/user/Documents/Projects/CocktailCoffeeTea/ArtSource/card-education-2026-10-16'
for script,args in [('finish.aseprite.lua',['--script-param','root='+str(p),'--script-param','out='+out]),('verify-preview.aseprite.lua',['--script-param','root='+out])]:
 r=subprocess.run(['C:/Program Files/Aseprite/Aseprite.exe','-b']+args+['--script',str(p/script)],capture_output=True,text=True)
 (p/'aseprite-batch.log').open('a',encoding='utf8').write(script+' exit='+str(r.returncode)+'\n'+r.stdout+r.stderr+'\n')
 print(script,'exit',r.returncode)
subprocess.run(['python',str(p/'verify_manifest.py')],check=True)
