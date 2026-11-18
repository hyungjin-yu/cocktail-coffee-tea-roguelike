import pathlib,json
out=pathlib.Path('ArtSource/feedback-art-2026-11-16'); origins=json.loads((out/'origin-inventory.json').read_text(encoding='utf-8'))['assets']
forms=[
'a tall drawstring sack with a split Y-shaped coffee branch emblem, three oval beans at its base',
'a squat cylindrical coffee tin with a bold zigzag mountain emblem and one large bean leaning against it',
'a broad shallow wooden coffee tray, diagonal slat pattern and five large beans',
'a folded triangular-top coffee pouch with one long curved leaf emblem and two slim beans',
'a small round tin with a simple four-petal blossom emblem and three beans to its left',
'a rectangular kraft coffee bag with a stepped crown-like ridge emblem and two beans to its right',
'a narrow tied sack with two outward leaf-shaped handles and two beans below',
'a low wide woven basket full of coffee beans, pronounced curved upright handle',
'a tall rectangular lidded coffee tin, bold hollow diamond emblem and two beans',
'an asymmetric slanted-open coffee pouch, bold crescent emblem and a spilling bean cluster',
'a round glass jar of roasted beans with wide dark lid and one bright square paper label without writing',
'a squat square coffee box with a deep V-shaped emblem and beans in a diagonal line',
'a small teardrop-shaped cloth sack with a double wave emblem and three beans on its left',
'a tapered hexagonal coffee canister with large hollow triangle emblem and two crossed beans',
'a short open burlap sack, wide folded rim, two tall leaf sprigs behind roasted coffee beans',
'a long shallow oval wooden bean scoop, short thick handle on the upper right, clearly visible roasted beans',
'a stout angular rectangular pouch with three large stepped ridges emblem and a pair of beans',
'a wide round coffee tin with a bold sun-ray emblem, two beans on its right',
'a slender rounded paper coffee bag with a large three-leaf clover emblem and one bean',
'a square wooden crate of roasted coffee beans with thick corner braces and diagonal front brace',
'a low ceramic bowl overflowing with roasted beans, a single large forked twig behind',
'a tall open paper cone filled with coffee beans, diagonal folded seam and two beans outside',
'a small straight-sided metal pail of roasted beans with thick swing handle visible to left',
'a broad oval burlap sack with zigzag tied neck pointing left, a large spiral emblem and two beans',
'a slim square glass coffee jar with round screw lid and bold X-shaped paper seal, beans visible inside',
'a wide low coffee pouch with scalloped folded top, bold single arch emblem and three beans',
'a very tall narrow coffee tin with large vertical double-chevron emblem and two beans',
'a short barrel-shaped wooden coffee container with two metal hoops, open top showing roasted beans'
]
assert len(forms)==len(origins)==28
common=' Single centered readable cafe game inventory pixel sprite. Warm cream paper, muted sage or warm coffee brown material, very dark brown 201D29 one-pixel chunky outline, three clear shading tones per material, upper-left light, side view with slight opening ellipse. Transparent background, crisp pixels, complete silhouette, no letters, numbers, logos, flags, glow, floor or cast shadow, no antialiasing. This is decorative game packaging, not a claim about authentic regional packaging.'
specs=[{'id':'dl_coffee_cappuccino_cup','width':32,'height':26,'description':'One empty round deep bowl-shaped ivory ceramic cappuccino cup with small curved handle on right, sitting on a broad low oval saucer, distinct round belly, no liquid, no steam.'+common}, {'id':'dl_foam_layer','width':32,'height':8,'description':'A narrow horizontal seamless strip of milk foam, ivory white three shading tones, tiny clustered bubbles with a gently scalloped top edge, opaque foam band across full width, transparent above and below, no cup, no objects, no text, crisp simple pixel art, horizontal tile.'}, {'id':'ui_lock_badge','width':16,'height':16,'description':'A single centered small closed padlock icon with thick arch-shaped shackle and square golden cream metal body, one dark keyhole, strong dark brown 201D29 one-pixel outline, three shading tones, transparent background, crisp readable cafe game pixel icon, no text, no floor, no glow.'}]
for o,shape in zip(origins,forms): specs.append({'id':o['assetId'],'width':32,'height':32,'ingredientId':o['ingredientId'],'name':o['name'],'description':'Roasted coffee bean ingredient icon: '+shape+'.'+common})
(out/'provenance/batch-native-specs.json').write_text(json.dumps(specs,ensure_ascii=False,indent=2),encoding='utf-8')
rows=[]
for s in specs: rows.append('{id='+json.dumps(s['id'])+',width='+str(s['width'])+',height='+str(s['height'])+',description='+json.dumps(s['description'])+'}')
lua='''-- Aseprite native automation. Uses the installed official PixelLab model and its official archive.
-- No credential access, no custom network client, no source patch, no full-trust request.
local specs={SPECS}
assert(app.isUIAvailable,"GUI runtime required")
closeAllDialogs()
_play_sound_on_complete=false
local index=0
local started=0
local jobId=nil
local activeSprite=nil
local finished=false
local function nextJob()
 index=index+1
 if index>#specs then
  finished=true
  artNativeBatchTimer:stop()
  app.alert("All 31 native PixelLab generation jobs completed; official image archives retained. Final QA still required.")
  return
 end
 closeAllDialogs()
 local s=specs[index]
 activeSprite=Sprite(s.width,s.height,ColorMode.RGB)
 activeSprite.layers[1].name=s.id
 local m=generate_no_style
 for k,v in pairs(m.default_json) do m.current_json[k]=v end
 m.current_json.description=s.description
 m.current_json.negative_description="text, letters, numbers, logo, floor, glow, smooth, blur, antialiasing, top down, isometric, cropped object"
 m.current_json.view="side"
 m.current_json.direction="none"
 m.current_json.no_background=true
 m.current_json.reference_image="No"
 m.current_json.init_image="No"
 m.current_json.color_image="No"
 m.current_json.image_size={width=s.width,height=s.height}
 m.current_json.outline="single color black outline"
 m.current_json.seed="0"
 m.dialog_json.title="Feedback art - "..s.id
 jobId=generation_queue.next_id
 started=os.time()
 m.generate(m)
end
artNativeBatchTimer=Timer{interval=1,ontick=function()
 if finished or not jobId then return end
 local job=nil
 for _,h in ipairs(generation_queue.history) do if h.id==jobId then job=h end end
 if job and (job.status=="completed" or job.status=="completed_with_warning") then
  if dlgLoading then dlgLoading:close() end
  nextJob()
 elseif job and (job.status=="failed" or job.status=="cancelled") then
  finished=true
  artNativeBatchTimer:stop()
  app.alert("Native PixelLab job failed: "..specs[index].id.."; inspect plugin. No ready claim.")
 elseif os.time()-started>300 then
  finished=true
  artNativeBatchTimer:stop()
  app.alert("Native PixelLab timeout: "..specs[index].id.."; inspect plugin. No ready claim.")
 end
end}
artNativeBatchTimer:start()
nextJob()
'''.replace('SPECS',','.join(rows))
(out/'provenance/generate-native-batch.lua').write_text(lua,encoding='utf-8')
print('Prepared native official-model jobs:',len(specs))
