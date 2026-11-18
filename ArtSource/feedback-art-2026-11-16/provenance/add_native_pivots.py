from pathlib import Path
p=Path('ArtSource/feedback-art-2026-11-16/provenance/prepare_finish.py');s=p.read_text(encoding='utf-8');needle='spr:saveAs(root.."ready/"..id..".aseprite")';ins='''local slice=spr:newSlice(Rectangle(0,0,im.width,im.height))
 slice.name="delivery_pivot"
 if id:find("^dl_coffee_") then slice.pivot=Point(im.width/2,im.height)
 elseif id=="dl_foam_layer" then slice.pivot=Point(0,im.height)
 else slice.pivot=Point(im.width/2,im.height/2) end
 '''+needle
assert needle in s;s=s.replace(needle,ins);p.write_text(s,encoding='utf-8')
p=Path('ArtSource/feedback-art-2026-11-16/provenance/validate_assets.py');s=p.read_text(encoding='utf-8');s=s.replace("lua='local root='+json.dumps(str(r).replace('\\\\','/')+'/')+'\\n'","lua='local pivots={}\\nlocal root='+json.dumps(str(r).replace('\\\\','/')+'/')+'\\n'")
s=s.replace("s:close() end\\n'","local p=s.slices[1].pivot pivots[#pivots+1]={assetId=\"'+identifier+'\",x=p.x,y=p.y} s:close() end\\n'")
s=s.replace("lua=lua.replace('root+','root..');","lua+='local f=assert(io.open(root+\"verification/source-pivots.json\",\"w\")) f:write(json.encode(pivots)) f:close()\\n'\nlua=lua.replace('root+','root..');")
s=s.replace("checks=[]; images={}","checks=[]; images={};source_pivots={x['assetId']:[x['x'],x['y']] for x in json.loads((verify/'source-pivots.json').read_text(encoding='utf-8'))}")
s=s.replace("'pngSha256':hashlib.sha256", "'nativePivotTopLeftPx':source_pivots[identifier],'nativePivotPass':source_pivots[identifier]==([im.width/2,im.height] if identifier.startswith('dl_coffee_') else [0,im.height] if identifier=='dl_foam_layer' else [im.width/2,im.height/2]),'pngSha256':hashlib.sha256")
s=s.replace("'roundtripPixelsEqual','alphaPolicyPass'","'roundtripPixelsEqual','alphaPolicyPass','nativePivotPass'")
p.write_text(s,encoding='utf-8')
